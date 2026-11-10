using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;
using MyAfricaMyNarrative.Infrastructure.Persistence;

namespace MyAfricaMyNarrative.Infrastructure.Media;

public sealed class FfmpegExportJobHandler(ApplicationDbContext db, IPrivateMediaStorage storage,
    IOptions<MediaOptions> options, IExportReadinessService readiness, IMediaProbe probe,
    IDbContextFactory<ApplicationDbContext> dbFactory) : IExportJobHandler
{
    private sealed record Payload(Guid StoryRevisionId, bool Final);

    public async Task<IReadOnlyList<ExportArtifact>> RenderAsync(ClaimedJob claim, CancellationToken ct)
    {
        using var renderCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var cancellationMonitor = MonitorCancellationAsync(claim.Id, renderCancellation, ct);
        ct = renderCancellation.Token;
        try
        {
        var payload = JsonSerializer.Deserialize<Payload>(claim.PayloadJson) ?? throw new InvalidDataException("Invalid export payload.");
        var project = await db.Projects.AsSplitQuery().Include(x => x.MediaAssets).Include(x => x.TranscriptRevisions)
            .Include(x => x.StoryRevisions).ThenInclude(x => x.Scenes).Include(x => x.ReviewApprovals)
            .Include(x => x.Resources).SingleAsync(x => x.Id == claim.ProjectId && x.OwnerUserId == claim.OwnerUserId, ct);
        var check = readiness.Check(project);
        if (check.Revision?.Id != payload.StoryRevisionId || check.Approval?.SnapshotHash != check.Revision.SnapshotHash ||
            (payload.Final ? !check.CanExportFinal : !check.CanPreview)) throw new InvalidOperationException("The approved export snapshot is no longer ready.");

        var revision = check.Revision;
        var temp = Path.Combine(Path.GetTempPath(), "man-export-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
        try
        {
            var width = payload.Final ? 1080 : 540; var height = payload.Final ? 1920 : 960;
            var segments = new List<string>(); var srt = new StringBuilder(); long timeline = 0; var index = 0;
            foreach (var scene in revision.Scenes.OrderBy(x => x.Order))
            {
                index++; var audio = project.MediaAssets.Single(x => x.Id == scene.AudioAssetId && x.PermissionStatus == PermissionStatus.Granted);
                var visual = project.MediaAssets.Single(x => x.Id == scene.VisualAssetId && x.PermissionStatus == PermissionStatus.Granted);
                var audioPath = await CopyToTemp(audio.StorageKey, temp, $"audio-{index}", ct);
                var visualPath = await CopyToTemp(visual.StorageKey, temp, $"visual-{index}", ct);
                var segment = Path.Combine(temp, $"segment-{index:D3}.mp4"); var seconds = (scene.EndMs - scene.StartMs) / 1000d;
                var args = new List<string> { "-y" };
                if (visual.Kind == MediaKind.Image) args.AddRange(["-loop", "1"]); else args.AddRange(["-stream_loop", "-1"]);
                args.AddRange(["-i", visualPath, "-ss", (scene.StartMs / 1000d).ToString("0.###", CultureInfo.InvariantCulture), "-i", audioPath,
                    "-t", seconds.ToString("0.###", CultureInfo.InvariantCulture), "-vf", $"scale={width}:{height}:force_original_aspect_ratio=decrease,pad={width}:{height}:(ow-iw)/2:(oh-ih)/2,fps=30",
                    "-map", "0:v:0", "-map", "1:a:0", "-c:v", "libx264", "-preset", "veryfast", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "128k", segment]);
                await BoundedProcessRunner.RunAsync(options.Value.FfmpegPath, args, TimeSpan.FromMinutes(5), ct); segments.Add(segment);
                srt.AppendLine(index.ToString(CultureInfo.InvariantCulture)).AppendLine($"{SrtTime(timeline)} --> {SrtTime(timeline + scene.EndMs - scene.StartMs)}")
                    .AppendLine(string.IsNullOrWhiteSpace(scene.Caption) ? scene.Text : scene.Caption).AppendLine(); timeline += scene.EndMs - scene.StartMs;
            }
            var list = Path.Combine(temp, "segments.txt"); await File.WriteAllLinesAsync(list, segments.Select(x => $"file '{x.Replace("'", "'\\''")}'"), ct);
            var video = Path.Combine(temp, payload.Final ? "final.mp4" : "preview.mp4");
            await BoundedProcessRunner.RunAsync(options.Value.FfmpegPath, ["-y", "-f", "concat", "-safe", "0", "-i", list, "-c", "copy", "-movflags", "+faststart", video], TimeSpan.FromMinutes(5), ct);
            var captions = Path.Combine(temp, "captions.srt"); await File.WriteAllTextAsync(captions, srt.ToString(), Encoding.UTF8, ct);
            await using (var verification = File.OpenRead(video))
            {
                var actual = await probe.ProbeAsync(verification, ct);
                if (!actual.HasAudio || !actual.HasVideo || actual.Width != width || actual.Height != height || Math.Abs(actual.DurationMs - timeline) > 1000)
                    throw new InvalidDataException("Rendered output failed stream, dimension or duration verification.");
            }
            await using var videoStream = File.OpenRead(video); var storedVideo = await storage.WriteOriginalAsync(videoStream, 500L * 1024 * 1024, ct);
            await using var captionStream = File.OpenRead(captions); var storedCaptions = await storage.WriteOriginalAsync(captionStream, 2L * 1024 * 1024, ct);
            var now = DateTimeOffset.UtcNow;
            return [new ExportArtifact(project.Id, claim.Id, revision.Id, revision.SnapshotHash, payload.Final ? ArtifactKind.FinalVideo : ArtifactKind.PreviewVideo, storedVideo.Key, storedVideo.Sha256, storedVideo.Length, now),
                new ExportArtifact(project.Id, claim.Id, revision.Id, revision.SnapshotHash, ArtifactKind.Captions, storedCaptions.Key, storedCaptions.Sha256, storedCaptions.Length, now)];
        }
        finally { try { Directory.Delete(temp, true); } catch { } }
        }
        finally
        {
            renderCancellation.Cancel();
            try { await cancellationMonitor; } catch (OperationCanceledException) { }
        }
    }

    private async Task MonitorCancellationAsync(Guid jobId, CancellationTokenSource renderCancellation, CancellationToken hostToken)
    {
        while (!renderCancellation.IsCancellationRequested && !hostToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), hostToken);
            await using var monitorDb = await dbFactory.CreateDbContextAsync(hostToken);
            var state = await monitorDb.Jobs.AsNoTracking().Where(x => x.Id == jobId).Select(x => x.State).SingleOrDefaultAsync(hostToken);
            if (state == JobState.Cancelled) { renderCancellation.Cancel(); return; }
        }
    }

    private async Task<string> CopyToTemp(string key, string folder, string name, CancellationToken ct)
    { var path = Path.Combine(folder, name + ".source"); await using var source = await storage.OpenReadAsync(key, ct); await using var target = File.Create(path); await source.CopyToAsync(target, ct); return path; }
    private static string SrtTime(long ms) => TimeSpan.FromMilliseconds(ms).ToString(@"hh\:mm\:ss\,fff", CultureInfo.InvariantCulture);
}
