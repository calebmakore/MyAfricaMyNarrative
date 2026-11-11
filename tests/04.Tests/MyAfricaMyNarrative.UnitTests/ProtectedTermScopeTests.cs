using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;

namespace MyAfricaMyNarrative.UnitTests;

/// <summary>A protected expression must survive in scenes that speak it, and is not demanded from scenes that quote other lines.</summary>
public sealed class ProtectedTermScopeTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Only_scenes_that_quote_the_protected_words_must_keep_them_on_screen()
    {
        var owner = Guid.NewGuid(); var project = new Project(owner, "Poem", Now);
        var audio = new MediaAsset(project.Id, "a", "v.wav", "Voice", new string('a', 64), "audio/wav", MediaKind.Audio, 10, "", Now, 120_000);
        var art = new MediaAsset(project.Id, "p", "p.png", "Art", new string('b', 64), "image/png", MediaKind.Image, 10, "", Now, width: 10, height: 10);
        project.MediaAssets.Add(audio); project.MediaAssets.Add(art);
        var transcript = new TranscriptRevision(project.Id, audio.Id, "I dream for an Africa. I am calling out all the sons and daughters of the soil.", "English", 0, 60_000, owner, Now);
        project.TranscriptRevisions.Add(transcript);
        project.CreatorIntents.Add(new CreatorIntent(project.Id, "", "", "", "", "sons and daughters of the soil", Now));
        project.StoryRevisions.Add(new StoryRevision(project.Id, 1, [
            new SceneDraft("Dream", "I dream for an Africa.", TextOrigin.SourceQuotation, audio.Id, art.Id, transcript.Id, 0, 5_000, ""),
            new SceneDraft("Call", "I am calling out all the sons and daughters of the soil.", TextOrigin.SourceQuotation, audio.Id, art.Id, transcript.Id, 5_000, 10_000, "I am calling out everyone.")], Now));

        var issues = new ExportReadinessService().Check(project).Issues.Where(x => x.Contains("protected expression")).ToList();

        var only = Assert.Single(issues);
        Assert.StartsWith("Scene 2 caption drops", only);   // scene 1 never says the phrase, so it is not flagged
    }

    [Fact]
    public void A_scene_quoting_the_whole_reviewed_range_is_checked_against_all_of_it()
    {
        var transcript = new TranscriptRevision(Guid.NewGuid(), Guid.NewGuid(), "the sons and daughters of the soil", "English", 0, 5_000, Guid.NewGuid(), Now);
        var wholeRange = new SceneDraft("All", "", TextOrigin.SourceQuotation, transcript.AssetId, Guid.NewGuid(), transcript.Id, 0, 5_000, "");
        var written = new SceneDraft("Mine", "My own words", TextOrigin.CreatorWritten, transcript.AssetId, Guid.NewGuid(), transcript.Id, 0, 5_000, "");
        Assert.Equal(transcript.Text, SourceFidelity.SpokenInScene(wholeRange, transcript));
        Assert.Equal(transcript.Text, SourceFidelity.SpokenInScene(written, transcript));
    }
}
