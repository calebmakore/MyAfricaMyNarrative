using MyAfricaMyNarrative.Domain;

namespace MyAfricaMyNarrative.Application;

public sealed class ExportReadinessService : IExportReadinessService
{
    public ExportReadiness Check(Project project)
    {
        var issues = new List<string>();
        var revision = project.StoryRevisions.OrderByDescending(x => x.RevisionNumber).FirstOrDefault();
        if (revision is null || revision.Scenes.Count == 0)
        { issues.Add("Add at least one source-backed scene in Shape."); return new(false, false, issues, revision, null, 0); }
        var approval = project.ReviewApprovals.OrderByDescending(x => x.ApprovedAtUtc)
            .FirstOrDefault(x => x.StoryRevisionId == revision.Id && x.SnapshotHash == revision.SnapshotHash);
        if (approval is null) issues.Add("Approve the current story revision.");
        var languageReview = project.LanguageReviews.Any(x => x.StoryRevisionId == revision.Id && x.SnapshotHash == revision.SnapshotHash);
        if (!languageReview) issues.Add("Record a fluent language review for the current approved revision.");
        var assets = project.MediaAssets.ToDictionary(x => x.Id);
        foreach (var scene in revision.Scenes)
        {
            if (!assets.TryGetValue(scene.AudioAssetId, out var audio) || audio.PermissionStatus != PermissionStatus.Granted)
                issues.Add($"Scene {scene.Order} needs permitted source audio.");
            if (!assets.TryGetValue(scene.VisualAssetId, out var visual) || visual.PermissionStatus != PermissionStatus.Granted)
                issues.Add($"Scene {scene.Order} needs a permitted visual.");
            if (scene.TranscriptRevisionId is Guid transcriptId && project.TranscriptRevisions.All(x => x.Id != transcriptId))
                issues.Add($"Scene {scene.Order} references a missing reviewed transcript.");
        }
        var duration = revision.Scenes.Sum(x => x.EndMs - x.StartMs);
        if (duration is < 60_000 or > 90_000) issues.Add("Final film duration must be 60–90 seconds; a shorter cut can only be a labelled preview.");
        var blockingPreview = issues.Any(x => !x.StartsWith("Final film duration", StringComparison.Ordinal) && !x.StartsWith("Record a fluent language review", StringComparison.Ordinal));
        return new(!blockingPreview, !blockingPreview && languageReview && duration is >= 60_000 and <= 90_000, issues, revision, approval, duration);
    }
}
