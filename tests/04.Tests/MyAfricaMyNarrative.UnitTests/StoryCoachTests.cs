using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;
using MyAfricaMyNarrative.Infrastructure.Media;

namespace MyAfricaMyNarrative.UnitTests;

public sealed class StoryCoachTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static (Project Project, StoryRevision Revision) Story(int scenes, int secondsEach = 15, bool endOnQuote = false, string localContext = "")
    {
        var owner = Guid.NewGuid(); var project = new Project(owner, "Story", Now);
        var audio = new MediaAsset(project.Id, "a", "v.weba", "Voice", new string('a', 64), "audio/webm", MediaKind.Audio, 10, "", Now, 300_000);
        var visual = new MediaAsset(project.Id, "v", "p.jpg", "Photo", new string('b', 64), "image/jpeg", MediaKind.Image, 10, "", Now, width: 10, height: 10);
        project.MediaAssets.Add(audio); project.MediaAssets.Add(visual);
        var transcript = new TranscriptRevision(project.Id, audio.Id, "Spoken words here", "Shona", 0, 300_000, owner, Now);
        project.TranscriptRevisions.Add(transcript);
        project.CreatorIntents.Add(new CreatorIntent(project.Id, "Young designers", "Style begins at home", "Warm", localContext, "", Now));
        var drafts = Enumerable.Range(0, scenes).Select(i =>
        {
            var quote = endOnQuote && i == scenes - 1;
            return new SceneDraft($"Scene {i + 1}", quote ? "" : $"My words {i + 1}", quote ? TextOrigin.SourceQuotation : TextOrigin.CreatorWritten,
                audio.Id, visual.Id, quote ? transcript.Id : null, i * secondsEach * 1000L, (i + 1) * secondsEach * 1000L, "");
        }).ToList();
        var revision = new StoryRevision(project.Id, 1, drafts, Now);
        project.StoryRevisions.Add(revision);
        return (project, revision);
    }

    [Fact]
    public void Practice_structure_places_every_scene_once_with_opening_turn_and_ending()
    {
        var (project, revision) = Story(5);
        var structure = StoryCoachRules.Practice(StoryCoachRules.BuildRequest(project, revision, shareWords: false));
        var (safe, error) = StoryCoachRules.Validate(structure, revision.Scenes.Select(x => x.Id).ToList());
        Assert.Null(error);
        Assert.Equal([StoryBeat.Hook, StoryBeat.Context, StoryBeat.TurningPoint, StoryBeat.Resolution], safe!.Beats.Select(x => x.Beat));
        Assert.Equal(revision.Scenes.OrderBy(x => x.Order).First().Id, safe.Beats[0].SceneIds.Single());
        Assert.Equal(revision.Scenes.OrderBy(x => x.Order).Last().Id, safe.Beats[^1].SceneIds.Single());
        Assert.Equal(5, StoryCoachRules.Order(safe).Distinct().Count());
        Assert.All(safe.Gaps, x => Assert.Equal("", x.SuggestedNarration));
    }

    [Fact]
    public void Practice_gaps_come_from_known_facts_short_story_missing_context_and_quoted_ending()
    {
        var (project, revision) = Story(3, secondsEach: 10, endOnQuote: true);
        var gaps = StoryCoachRules.Practice(StoryCoachRules.BuildRequest(project, revision, false)).Gaps.Select(x => x.Kind).ToList();
        Assert.Contains(StoryGapKind.TurningPoint, gaps);
        Assert.Contains(StoryGapKind.Context, gaps);
        Assert.Contains(StoryGapKind.Ending, gaps);
        Assert.Contains(StoryGapKind.Length, gaps);

        var (complete, rev2) = Story(5, secondsEach: 14, localContext: "Mutare, a border town");
        Assert.Empty(StoryCoachRules.Practice(StoryCoachRules.BuildRequest(complete, rev2, false)).Gaps);
    }

    [Fact]
    public void Answered_questions_close_their_gap()
    {
        var (project, revision) = Story(3, secondsEach: 20);
        var q = new CreatorQuestion(project.Id, "Turning point", "What changed?", Now); q.RecordAnswer("When Mbuya gave me her machine", Now);
        project.CreatorQuestions.Add(q);
        var gaps = StoryCoachRules.Practice(StoryCoachRules.BuildRequest(project, revision, false)).Gaps;
        Assert.DoesNotContain(gaps, x => x.Kind == StoryGapKind.TurningPoint);
    }

    [Fact]
    public void Words_are_only_included_when_the_creator_shares_them()
    {
        var (project, revision) = Story(2, endOnQuote: true);
        Assert.All(StoryCoachRules.BuildRequest(project, revision, false).Scenes, x => Assert.Null(x.Words));
        var shared = StoryCoachRules.BuildRequest(project, revision, true);
        Assert.True(shared.WordsShared);
        Assert.Equal("Spoken words here", shared.Scenes[^1].Words);
    }

    [Fact]
    public void Validation_refuses_invented_dropped_or_repeated_scenes()
    {
        var (_, revision) = Story(3);
        var ids = revision.Scenes.OrderBy(x => x.Order).Select(x => x.Id).ToList();
        StoryStructure Make(params Guid[] scene) => new([new BeatAssignment(StoryBeat.Hook, scene, "")], [], "x");
        Assert.NotNull(StoryCoachRules.Validate(Make(ids[0], ids[1], Guid.NewGuid()), ids).Error);
        Assert.NotNull(StoryCoachRules.Validate(Make(ids[0], ids[1]), ids).Error);
        Assert.NotNull(StoryCoachRules.Validate(Make(ids[0], ids[1], ids[1]), ids).Error);
        Assert.Null(StoryCoachRules.Validate(Make(ids[2], ids[0], ids[1]), ids).Error);
    }

    [Fact]
    public void Ai_suggested_words_change_the_snapshot_and_are_disclosed_in_credits()
    {
        var (project, revision) = Story(1);
        var draft = revision.Scenes.Single().ToDraft();
        var ai = new StoryRevision(project.Id, 2, [draft with { TextOrigin = TextOrigin.AiSuggested }], Now);
        Assert.NotEqual(revision.SnapshotHash, ai.SnapshotHash);
        var credits = new[] { new CreditEntry(project.Id, "Storyteller", "Rudo", null, 1, Now) };
        Assert.Contains(RenderText.AiWordsDisclosure, RenderText.CreditsCard(credits, aiWords: true));
        Assert.DoesNotContain(RenderText.AiWordsDisclosure, RenderText.CreditsCard(credits));
        Assert.Contains(RenderText.AiWordsDisclosure, RenderText.CreditsCue(credits, aiWords: true));
    }
}
