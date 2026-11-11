using MyAfricaMyNarrative.Domain;

namespace MyAfricaMyNarrative.Application;

public enum StudioStage { Record, Shape, Make, Export }

/// <summary>One concrete thing the creator can do, with the stage and page anchor where it is done.</summary>
public sealed record StudioTask(StudioStage Stage, string Anchor, string Title, string Hint, bool Done);

public sealed record StudioProgress(IReadOnlyList<StudioTask> Tasks, StudioTask? Next, bool FinalReady)
{
    public bool IsStageComplete(StudioStage stage) => Tasks.Where(x => x.Stage == stage).All(x => x.Done);
    public int CompletedCount => Tasks.Count(x => x.Done);
    public int Percent => Tasks.Count == 0 ? 0 : (int)Math.Round(100d * CompletedCount / Tasks.Count);
}

/// <summary>
/// Turns the real project state into an ordered checklist so a first-time creator always sees the next useful step.
/// Every item is derived from stored data; nothing is assumed complete.
/// </summary>
public static class StudioProgressCalculator
{
    public static StudioProgress Calculate(Project project, ExportReadiness readiness, IReadOnlyList<ExportArtifact> artifacts)
    {
        var permitted = project.MediaAssets.Where(x => x.PermissionStatus == PermissionStatus.Granted).ToList();
        var revision = readiness.Revision;
        var hasScenes = revision is not null && revision.Scenes.Count > 0;
        var approved = readiness.Approval is not null;
        var languageReviewed = revision is not null && project.LanguageReviews.Any(x => x.StoryRevisionId == revision.Id && x.SnapshotHash == revision.SnapshotHash);
        var intent = project.CreatorIntents.SingleOrDefault();
        var hasIntent = intent is not null && (intent.Meaning.Length > 0 || intent.Audience.Length > 0);
        var missingShots = hasScenes && project.Shots.Any(s => s.Status == ShotStatus.Planned && revision!.Scenes.Any(sc => s.MatchesScene(sc.Title)));
        var finalRendered = revision is not null && artifacts.Any(x => x.Kind == ArtifactKind.FinalVideo && x.StoryRevisionId == revision.Id
            && x.SnapshotHash == revision.SnapshotHash && (x.CreditsHash.Length == 0 || x.CreditsHash == readiness.CreditsHash));

        StudioTask[] tasks =
        [
            new(StudioStage.Record, "add-source", "Add your voice", "Record a voice note or upload an interview.", permitted.Any(x => x.CanProvideSound)),
            new(StudioStage.Record, "add-source", "Add a picture", "Upload a photo or short video that belongs with the story.", permitted.Any(x => x.Kind != MediaKind.Audio)),
            new(StudioStage.Shape, "intention", "Say what the story is for", "Who it is for and the meaning it must keep.", hasIntent),
            new(StudioStage.Shape, "transcript", "Check the words", "Listen and type what was actually said.", project.TranscriptRevisions.Count > 0),
            new(StudioStage.Shape, "add-scene", "Build your first scene", "Pair a stretch of your voice with a picture.", hasScenes),
            new(StudioStage.Shape, "current-cut", "Approve your cut", "Review the scenes in order and approve them.", approved),
            new(StudioStage.Shape, "language-review", "Record a language review", "A fluent speaker confirms the captions.", languageReviewed),
            new(StudioStage.Make, "resources", "List what you have", "Phone, people, places and time you really have.", project.Resources.Count > 0),
            new(StudioStage.Make, "shot-list", "Finish planned shots", "Capture or drop every planned shot.", hasScenes && !missingShots),
            new(StudioStage.Export, "credits", "Approve credits", "Name the storyteller and contributors.", readiness.CreditsApproved),
            new(StudioStage.Export, "render", "Make the final film", readiness.CanExportFinal ? "Everything is ready — render your film." : "Clear the checklist, then render.", finalRendered)
        ];
        return new(tasks, tasks.FirstOrDefault(x => !x.Done), readiness.CanExportFinal);
    }
}
