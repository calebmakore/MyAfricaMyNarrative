using MyAfricaMyNarrative.Domain;

namespace MyAfricaMyNarrative.Application;

/// <summary>
/// Pilot measures computed only from timestamps and records the app already keeps. Nothing is estimated:
/// a measure that has not happened yet is null.
/// </summary>
public sealed record PilotProjectMetrics(
    Guid ProjectId, string Title, string StoryType, DateTimeOffset StartedAtUtc,
    TimeSpan? ToFirstSource, TimeSpan? ToApprovedCut, TimeSpan? ToFinalFilm,
    int StoryVersions, int Scenes, int QuotedScenes, int CreatorWrittenScenes, int AiWordScenes,
    int LiveAiRequests, int AiSuggestionsKept, int AiSuggestionsRejected, int PracticeSuggestions,
    int PlanVersions, bool PlanApproved, int GlossaryApproved, int ContributorsRecorded,
    PilotFeedback? Feedback);

public static class PilotMetrics
{
    public static PilotProjectMetrics Compute(Project project, IReadOnlyList<ExportArtifact> artifacts)
    {
        var start = project.CreatedAtUtc;
        TimeSpan? Since(DateTimeOffset? at) => at is { } t && t >= start ? t - start : null;
        var firstSource = project.MediaAssets.Count == 0 ? (DateTimeOffset?)null : project.MediaAssets.Min(x => x.CreatedAtUtc);
        var firstApproval = project.ReviewApprovals.Count == 0 ? (DateTimeOffset?)null : project.ReviewApprovals.Min(x => x.ApprovedAtUtc);
        var finals = artifacts.Where(x => x.Kind == ArtifactKind.FinalVideo).ToList();
        var firstFinal = finals.Count == 0 ? (DateTimeOffset?)null : finals.Min(x => x.CreatedAtUtc);
        var current = project.StoryRevisions.OrderByDescending(x => x.RevisionNumber).FirstOrDefault();
        var scenes = current?.Scenes.ToList() ?? [];

        var live = project.StudioProposals.Where(x => !x.IsFixture).ToList();
        var livePlans = project.ProductionPlans.Where(x => !x.IsFixture).ToList();
        var kept = live.Count(x => x.Status == ProposalStatus.Applied) + livePlans.Count(x => x.ApprovedOnce)
            + project.TranscriptionProposals.Count(x => x.Status == TranscriptionProposalStatus.Reviewed);
        var rejected = live.Count(x => x.Status == ProposalStatus.Rejected) + livePlans.Count(x => x.Status == PlanStatus.Rejected)
            + project.TranscriptionProposals.Count(x => x.Status == TranscriptionProposalStatus.Rejected);
        var practice = project.StudioProposals.Count(x => x.IsFixture) + project.ProductionPlans.Count(x => x.IsFixture);

        return new PilotProjectMetrics(project.Id, project.Title, project.StoryType.ToString(), start,
            Since(firstSource), Since(firstApproval), Since(firstFinal),
            project.StoryRevisions.Count, scenes.Count,
            scenes.Count(x => x.TextOrigin == TextOrigin.SourceQuotation), scenes.Count(x => x.TextOrigin == TextOrigin.CreatorWritten), scenes.Count(x => x.TextOrigin == TextOrigin.AiSuggested),
            live.Count + livePlans.Count + project.TranscriptionProposals.Count, kept, rejected, practice,
            project.ProductionPlans.Count, project.ProductionPlans.Any(x => x.Status == PlanStatus.Approved),
            project.Glossary.Count(x => x.IsApproved), project.MediaAssets.Count(x => x.SuppliedBy.Length > 0),
            project.PilotFeedback.FirstOrDefault());
    }

    public static string Duration(TimeSpan? value) => value is not { } t ? "Not yet"
        : t.TotalMinutes < 1 ? "under a minute"
        : t.TotalHours < 1 ? $"{(int)t.TotalMinutes} min"
        : t.TotalDays < 1 ? $"{(int)t.TotalHours} h {t.Minutes} min"
        : $"{(int)t.TotalDays} d {t.Hours} h";

    public static IEnumerable<(HelpArea Area, string Label)> HelpAreas =>
    [
        (HelpArea.Recording, "Recording"), (HelpArea.Words, "Checking the words"), (HelpArea.StoryShape, "Shaping the story"),
        (HelpArea.Planning, "Planning the shoot"), (HelpArea.Captions, "Captions & translation"), (HelpArea.Export, "Exporting"), (HelpArea.Technical, "Phone or connection")
    ];
}
