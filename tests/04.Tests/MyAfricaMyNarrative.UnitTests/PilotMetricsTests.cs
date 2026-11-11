using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;

namespace MyAfricaMyNarrative.UnitTests;

public sealed class PilotMetricsTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Measures_come_only_from_recorded_timestamps_and_stay_null_until_they_happen()
    {
        var owner = Guid.NewGuid(); var project = new Project(owner, "Pilot story", Start);
        var empty = PilotMetrics.Compute(project, []);
        Assert.Null(empty.ToFirstSource); Assert.Null(empty.ToApprovedCut); Assert.Null(empty.ToFinalFilm);
        Assert.Equal("Not yet", PilotMetrics.Duration(empty.ToApprovedCut));

        var audio = new MediaAsset(project.Id, "a", "v.weba", "Voice", new string('a', 64), "audio/webm", MediaKind.Audio, 10, "", Start.AddMinutes(4), 60_000);
        project.MediaAssets.Add(audio);
        var revision = new StoryRevision(project.Id, 1, [new SceneDraft("One", "My words", TextOrigin.CreatorWritten, audio.Id, audio.Id, null, 0, 10_000, ""),
            new SceneDraft("Two", "AI words", TextOrigin.AiSuggested, audio.Id, audio.Id, null, 0, 10_000, "")], Start.AddMinutes(20));
        project.StoryRevisions.Add(revision);
        project.ReviewApprovals.Add(new ReviewApproval(project.Id, revision.Id, revision.SnapshotHash, owner, Start.AddMinutes(47)));
        var applied = new StudioProposal(project.Id, ProposalKind.StoryStructure, revision.SnapshotHash, "x", "{}", Start, isFixture: false); applied.Review(true, Start);
        var rejected = new StudioProposal(project.Id, ProposalKind.StoryOrder, revision.SnapshotHash, "x", "[]", Start, isFixture: false); rejected.Review(false, Start);
        project.StudioProposals.Add(applied); project.StudioProposals.Add(rejected);
        project.StudioProposals.Add(new StudioProposal(project.Id, ProposalKind.StoryOrder, revision.SnapshotHash, "x", "[]", Start, isFixture: true));
        var final = new ExportArtifact(project.Id, Guid.NewGuid(), revision.Id, revision.SnapshotHash, ArtifactKind.FinalVideo, "k", new string('c', 64), 1, Start.AddHours(2).AddMinutes(5));

        var m = PilotMetrics.Compute(project, [final]);
        Assert.Equal(TimeSpan.FromMinutes(4), m.ToFirstSource);
        Assert.Equal(TimeSpan.FromMinutes(47), m.ToApprovedCut);
        Assert.Equal("47 min", PilotMetrics.Duration(m.ToApprovedCut));
        Assert.Equal("2 h 5 min", PilotMetrics.Duration(m.ToFinalFilm));
        Assert.Equal((1, 1, 0), (m.CreatorWrittenScenes, m.AiWordScenes, m.QuotedScenes));
        Assert.Equal((2, 1, 1, 1), (m.LiveAiRequests, m.AiSuggestionsKept, m.AiSuggestionsRejected, m.PracticeSuggestions));
    }

    [Fact]
    public void Feedback_is_bounded_and_one_per_project_can_be_updated()
    {
        var feedback = new PilotFeedback(Guid.NewGuid(), IntentMatch.Mostly, HelpArea.Planning | HelpArea.Captions, "Needed help with Shona captions", "", Start);
        Assert.True(feedback.NeededHelp.HasFlag(HelpArea.Captions));
        feedback.Update(IntentMatch.Fully, HelpArea.None, null, "Proud of it", Start.AddDays(1));
        Assert.Equal(IntentMatch.Fully, feedback.IntentMatch);
        Assert.Throws<ArgumentException>(() => feedback.Update((IntentMatch)9, HelpArea.None, null, null, Start));
        Assert.Throws<ArgumentException>(() => feedback.Update(IntentMatch.Fully, (HelpArea)1024, null, null, Start));
        Assert.Throws<ArgumentException>(() => feedback.Update(IntentMatch.Fully, HelpArea.None, new string('x', 501), null, Start));
    }

    [Fact]
    public void A_superseded_plan_still_counts_as_kept_if_it_was_approved()
    {
        var p = Guid.NewGuid(); var r = Guid.NewGuid(); var h = new string('a', 64);
        PlanStepDraft Step() => new(Guid.NewGuid(), "Scene", PlanApproach.UseExisting, "Use the photo", 10, 0, "Costs no time", [], null);
        var approved = new ProductionPlan(p, 1, r, h, h, "", null, "s", "", "Live OpenAI (m)", false, [Step()], Start);
        approved.Approve(Start); approved.Supersede(Start);
        var never = new ProductionPlan(p, 2, r, h, h, "", null, "s", "", "Live OpenAI (m)", false, [Step()], Start);
        never.Supersede(Start);
        Assert.True(approved.ApprovedOnce);
        Assert.False(never.ApprovedOnce);
    }
}
