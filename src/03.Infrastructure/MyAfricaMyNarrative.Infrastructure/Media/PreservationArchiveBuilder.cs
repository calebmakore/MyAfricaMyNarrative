using System.IO.Compression;
using System.Text;
using System.Text.Json;
using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;

namespace MyAfricaMyNarrative.Infrastructure.Media;

public static class PreservationArchiveBuilder
{
    public static async Task BuildAsync(Stream output, Project project, StoryRevision revision, ReviewApproval approval,
        IReadOnlyList<ExportArtifact> artifacts, IPrivateMediaStorage storage, CancellationToken cancellationToken)
    {
        var film = artifacts.First(x => x.Kind is ArtifactKind.FinalVideo or ArtifactKind.PreviewVideo);
        var checksums = new List<string>();
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);

        async Task AddStored(string entryName, string key, string expectedSha)
        {
            var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
            await using var target = entry.Open();
            await using var source = await storage.OpenReadAsync(key, cancellationToken);
            await source.CopyToAsync(target, cancellationToken);
            checksums.Add($"{expectedSha}  {entryName}");
        }

        var referencedIds = revision.Scenes.SelectMany(x => new[] { x.AudioAssetId, x.VisualAssetId }).Distinct().ToHashSet();
        foreach (var asset in project.MediaAssets.Where(x => referencedIds.Contains(x.Id) && x.PermissionStatus == PermissionStatus.Granted))
            await AddStored($"originals/{asset.Id:N}{Path.GetExtension(asset.OriginalName)}", asset.StorageKey, asset.Sha256);
        await AddStored("film/approved.mp4", film.StorageKey, film.Sha256);
        var captions = artifacts.FirstOrDefault(x => x.Kind == ArtifactKind.Captions);
        if (captions is not null) await AddStored("captions/approved.srt", captions.StorageKey, captions.Sha256);

        var metadata = JsonSerializer.Serialize(new
        {
            SchemaVersion = "1.0",
            Project = new { project.Id, project.Title },
            Revision = new { revision.Id, revision.RevisionNumber, revision.SnapshotHash, approval.ApprovedAtUtc },
            LanguageReview = project.LanguageReviews.Where(x => x.StoryRevisionId == revision.Id && x.SnapshotHash == revision.SnapshotHash)
                .Select(x => new { x.ReviewerName, x.Language, x.FluencyBasis, x.IsSelfReview, x.Notes, x.AttestedAtUtc }),
            Scenes = revision.Scenes.OrderBy(x => x.Order).Select(x => new { x.Order, x.Title, x.Text, TextOrigin = x.TextOrigin.ToString(), x.AudioAssetId, x.VisualAssetId, x.TranscriptRevisionId, x.StartMs, x.EndMs, x.Caption }),
            Sources = project.MediaAssets.Where(x => referencedIds.Contains(x.Id)).Select(x => new { x.Id, x.DisplayName, x.OriginalName, Kind = x.Kind.ToString(), x.Sha256, Permission = x.PermissionStatus.ToString(), x.PermissionDeclaration, x.Attribution }),
            Transcripts = project.TranscriptRevisions.Where(x => revision.Scenes.Any(s => s.TranscriptRevisionId == x.Id)).Select(x => new { x.Id, x.AssetId, x.Language, x.Text, x.StartMs, x.EndMs, x.ReviewerUserId, x.ReviewedAtUtc })
        }, new JsonSerializerOptions { WriteIndented = true });
        await WriteText(zip, "metadata.json", metadata);
        await WriteText(zip, "README.txt", "My Africa, My Narrative preservation archive\nYour story. Your voice. Our Africa.\nFiles are tied to metadata.json. Owner attestation is not independent permission verification.");
        await WriteText(zip, "checksums.sha256", string.Join("\n", checksums) + "\n");
    }

    private static async Task WriteText(ZipArchive zip, string name, string value)
    {
        var entry = zip.CreateEntry(name);
        await using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        await writer.WriteAsync(value);
    }
}
