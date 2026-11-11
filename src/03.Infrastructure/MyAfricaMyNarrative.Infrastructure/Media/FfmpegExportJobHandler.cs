using Microsoft.Extensions.DependencyInjection;
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
    IDbContextFactory<ApplicationDbContext> dbFactory, IServiceScopeFactory scopes) : IExportJobHandler
{
    private sealed record Payload(Guid StoryRevisionId, bool Final, string? ExportHash = null);

    /// <summary>Reports a real step through its own scope so it never shares the render's database context.</summary>
    private async Task Progress(ClaimedJob claim, int step, int total, string note, CancellationToken ct)
    {
        try { using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<IJobStore>().ReportProgressAsync(claim, step, total, note, ct); }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { /* progress is informational only */ }
    }

    public async Task<IReadOnlyList<ExportArtifact>> RenderAsync(ClaimedJob claim, CancellationToken ct)
    {
        using var renderCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var cancellationMonitor = MonitorCancellationAsync(claim.Id, renderCancellation, ct);
        ct = renderCancellation.Token;
        try
        {
        var payload = JsonSerializer.Deserialize<Payload>(claim.PayloadJson) ?? throw new InvalidDataException("Invalid export payload.");
        // Load exactly the graph the Web used to decide readiness (language reviews, credits, shots included).
        var project = await db.Projects.IncludeStudio().SingleAsync(x => x.Id == claim.ProjectId && x.OwnerUserId == claim.OwnerUserId, ct);
        var check = readiness.Check(project);
        var expectedHash = payload.ExportHash ?? check.Revision?.SnapshotHash;
        if (check.Revision?.Id != payload.StoryRevisionId || check.Approval?.SnapshotHash != check.Revision.SnapshotHash ||
            expectedHash != check.ExportHash || (payload.Final ? !check.CanExportFinal : !check.CanPreview))
            throw new InvalidOperationException("The approved export snapshot is no longer ready: " + (check.Issues.Count == 0 ? "the story or credits changed after this export was queued." : string.Join(" ", check.Issues)));

        var revision = check.Revision;
        var credits = CreditsRules.Ordered(project.Credits);
        var temp = Path.Combine(Path.GetTempPath(), "man-export-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
        try
        {
            var width = payload.Final ? 1080 : 540; var height = payload.Final ? 1920 : 960;
            var burnCaptions = options.Value.BurnInCaptions;
            const string font = "caption-font.ttf";
            if (burnCaptions || credits.Count > 0) File.Copy(ResolveFontFile(), Path.Combine(temp, font));
            var segments = new List<string>(); var srt = new StringBuilder(); var translatedSrt = new StringBuilder();
            long timeline = 0; var index = 0; var translatedCues = 0;
            var orderedScenes = revision.Scenes.OrderBy(x => x.Order).ToList();
            // Steps: each scene, the credits card (if any), joining, then checking the finished file.
            var totalSteps = orderedScenes.Count + (credits.Count > 0 ? 1 : 0) + 2; var step = 0;
            // Approved glossary explanations are added under the first caption where each expression appears; caption words are unchanged.
            var annotated = GlossaryRules.Annotate(orderedScenes.Select(x => x.EffectiveCaption.Trim()).ToList(), project.Glossary);
            foreach (var scene in orderedScenes)
            {
                index++;
                await Progress(claim, step++, totalSteps, $"Making scene {index} of {orderedScenes.Count}: {(scene.Title.Length > 60 ? scene.Title[..60] + "…" : scene.Title)}", ct);
                var audio = project.MediaAssets.Single(x => x.Id == scene.AudioAssetId && x.PermissionStatus == PermissionStatus.Granted);
                var visual = project.MediaAssets.Single(x => x.Id == scene.VisualAssetId && x.PermissionStatus == PermissionStatus.Granted);
                var audioPath = await CopyToTemp(audio.StorageKey, temp, $"audio-{index}", ct);
                var visualPath = await CopyToTemp(visual.StorageKey, temp, $"visual-{index}", ct);
                var segment = $"segment-{index:D3}.mp4";
                var caption = annotated[index - 1]; string? captionFile = null;
                if (burnCaptions && caption.Length > 0)
                {
                    captionFile = $"caption-{index:D3}.txt";
                    await File.WriteAllTextAsync(Path.Combine(temp, captionFile), RenderText.Wrap(caption), new UTF8Encoding(false), ct);
                }
                var args = RenderText.SceneSegmentArgs(Path.GetFileName(visualPath), visual.Kind == MediaKind.Image, Path.GetFileName(audioPath),
                    scene.StartMs, scene.EndMs, width, height, captionFile, font, segment, visualCarriesTheSound: visual.Id == audio.Id && visual.Kind == MediaKind.Video);
                await BoundedProcessRunner.RunAsync(options.Value.FfmpegPath, args, TimeSpan.FromMinutes(5), ct, temp); segments.Add(segment);
                var end = timeline + scene.EndMs - scene.StartMs;
                RenderText.AppendCue(srt, index, timeline, end, caption);
                if (scene.Translation.Length > 0) RenderText.AppendCue(translatedSrt, ++translatedCues, timeline, end, scene.Translation);
                timeline = end;
            }
            var aiWords = revision.Scenes.Any(x => x.TextOrigin == TextOrigin.AiSuggested) || GlossaryRules.Approved(project.Glossary).Any(x => x.AiDrafted);
            if (credits.Count > 0)
            {
                await Progress(claim, step++, totalSteps, "Adding the credits card", ct);
                await File.WriteAllTextAsync(Path.Combine(temp, "credits.txt"), RenderText.CreditsCard(credits, aiWords), new UTF8Encoding(false), ct);
                await BoundedProcessRunner.RunAsync(options.Value.FfmpegPath,
                    RenderText.CreditsCardArgs(credits.Count, width, height, "credits.txt", font, "segment-credits.mp4"), TimeSpan.FromMinutes(2), ct, temp);
                segments.Add("segment-credits.mp4");
                var end = timeline + CreditsRules.CardDurationMs;
                RenderText.AppendCue(srt, index + 1, timeline, end, RenderText.CreditsCue(credits, aiWords));
                if (translatedCues > 0) RenderText.AppendCue(translatedSrt, ++translatedCues, timeline, end, RenderText.CreditsCue(credits, aiWords));
                timeline = end;
            }
            var list = Path.Combine(temp, "segments.txt"); await File.WriteAllLinesAsync(list, segments.Select(x => $"file '{x}'"), ct);
            await Progress(claim, step++, totalSteps, "Joining the scenes into one film", ct);
            var videoName = payload.Final ? "final.mp4" : "preview.mp4"; var video = Path.Combine(temp, videoName);
            await BoundedProcessRunner.RunAsync(options.Value.FfmpegPath, RenderText.ConcatArgs("segments.txt", videoName), TimeSpan.FromMinutes(5), ct, temp);
            var captions = Path.Combine(temp, "captions.srt"); await File.WriteAllTextAsync(captions, srt.ToString(), new UTF8Encoding(false), ct);
            await Progress(claim, step++, totalSteps, "Checking the finished film", ct);
            await using (var verification = File.OpenRead(video))
            {
                var actual = await probe.ProbeAsync(verification, ct);
                if (!actual.HasAudio || !actual.HasVideo || actual.Width != width || actual.Height != height || Math.Abs(actual.DurationMs - timeline) > 1000)
                    throw new InvalidDataException("Rendered output failed stream, dimension or duration verification.");
            }
            var now = DateTimeOffset.UtcNow; var artifacts = new List<ExportArtifact>();
            async Task Publish(string path, ArtifactKind kind, long maxBytes)
            {
                await using var stream = File.OpenRead(path); var stored = await storage.WriteOriginalAsync(stream, maxBytes, ct);
                artifacts.Add(new ExportArtifact(project.Id, claim.Id, revision.Id, revision.SnapshotHash, kind, stored.Key, stored.Sha256, stored.Length, now, check.CreditsHash));
            }
            await Publish(video, payload.Final ? ArtifactKind.FinalVideo : ArtifactKind.PreviewVideo, 500L * 1024 * 1024);
            await Publish(captions, ArtifactKind.Captions, 2L * 1024 * 1024);
            if (translatedCues > 0)
            {
                var translated = Path.Combine(temp, "captions.translated.srt");
                await File.WriteAllTextAsync(translated, translatedSrt.ToString(), new UTF8Encoding(false), ct);
                await Publish(translated, ArtifactKind.TranslatedCaptions, 2L * 1024 * 1024);
            }
            return artifacts;
        }
        finally { try { Directory.Delete(temp, true); } catch { } }
        }
        finally
        {
            renderCancellation.Cancel();
            try { await cancellationMonitor; } catch (OperationCanceledException) { }
        }
    }

    private string ResolveFontFile()
    {
        var configured = options.Value.CaptionFontFile;
        var path = string.IsNullOrWhiteSpace(configured) ? Path.Combine(AppContext.BaseDirectory, "Fonts", "Jost-SemiBold.ttf") : Path.GetFullPath(configured);
        if (!File.Exists(path))
            throw new InvalidOperationException($"Caption font not found at {Path.GetFileName(path)}. Rebuild so the bundled font is copied, or set Media:CaptionFontFile to a TTF/OTF file.");
        return path;
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
}
