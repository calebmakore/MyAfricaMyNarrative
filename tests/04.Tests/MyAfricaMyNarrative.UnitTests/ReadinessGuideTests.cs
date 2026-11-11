using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;

namespace MyAfricaMyNarrative.UnitTests;

/// <summary>Every export checklist item gets a plain title, a next step, a reason and a place to fix it.</summary>
public sealed class ReadinessGuideTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Real_readiness_issues_all_get_plain_guidance_and_blocking_ones_come_first()
    {
        var owner = Guid.NewGuid(); var project = new Project(owner, "Tailoring", Now);
        var audio = new MediaAsset(project.Id, "a", "voice.weba", "Voice", new string('a', 64), "audio/webm", MediaKind.Audio, 10, "", Now, 120_000);
        var visual = new MediaAsset(project.Id, "v", "home.jpg", "Home", new string('b', 64), "image/jpeg", MediaKind.Image, 10, "", Now, width: 10, height: 10);
        project.MediaAssets.Add(audio); project.MediaAssets.Add(visual);
        var transcript = new TranscriptRevision(project.Id, audio.Id, "Mbuya taught me stitching.", "Shona", 0, 60_000, owner, Now);
        project.TranscriptRevisions.Add(transcript);
        project.StoryRevisions.Add(new StoryRevision(project.Id, 1, [
            new SceneDraft("One", "", TextOrigin.CreatorWritten, audio.Id, visual.Id, null, 0, 10_000, "Hello"),
            new SceneDraft("Two", "Words nobody said", TextOrigin.SourceQuotation, audio.Id, visual.Id, transcript.Id, 0, 10_000, ""),
            new SceneDraft("Three", "Quoted", TextOrigin.SourceQuotation, audio.Id, visual.Id, null, 0, 10_000, "")], Now));

        var readiness = new ExportReadinessService().Check(project);
        Assert.False(readiness.CanPreview);
        Assert.True(readiness.BlockingCount >= 2);
        var guide = readiness.Issues.Select(ReadinessGuide.Describe).ToList();

        Assert.All(guide, x => { Assert.False(string.IsNullOrWhiteSpace(x.Why), x.Original); Assert.NotEqual(FixPlace.None, x.Place); Assert.NotEqual(x.Original, x.Title); });
        Assert.Contains(guide, x => x.Title == "Scene 2: the quote doesn’t match what was said" && x.Place == FixPlace.ShapeCut);
        Assert.Contains(guide, x => x.Title == "Scene 3: choose which saved words it quotes");
        Assert.Contains(guide, x => x.Title == "A fluent speaker needs to check the words" && x.Place == FixPlace.ShapeLanguageReview);
        // Blocking issues (approve the cut, the quotes) come before final-only ones (review, credits, length).
        var blocking = guide.Take(readiness.BlockingCount).Select(x => x.Title).ToList();
        Assert.Contains("Approve your cut", blocking);
        Assert.DoesNotContain("A fluent speaker needs to check the words", blocking);
    }
}
