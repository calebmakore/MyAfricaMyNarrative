using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;

namespace MyAfricaMyNarrative.UnitTests;

public sealed class CultureOwnershipTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    /// <summary>Ready for final export: 57 s quoted scene + 5 s credits, approved, reviewed and credited.</summary>
    private static (Project Project, MediaAsset Audio, MediaAsset Visual) Ready(string caption = "Mbuya taught me chinyakare stitching")
    {
        var owner = Guid.NewGuid(); var project = new Project(owner, "Tailoring", Now);
        var audio = new MediaAsset(project.Id, "a", "voice.weba", "Mbuya voice", new string('a', 64), "audio/webm", MediaKind.Audio, 10, "", Now, 120_000);
        var visual = new MediaAsset(project.Id, "v", "home.jpg", "Family home", new string('b', 64), "image/jpeg", MediaKind.Image, 10, "", Now, width: 10, height: 10);
        project.MediaAssets.Add(audio); project.MediaAssets.Add(visual);
        var transcript = new TranscriptRevision(project.Id, audio.Id, "Mbuya taught me chinyakare stitching when I was nine.", "Shona", 0, 60_000, owner, Now);
        project.TranscriptRevisions.Add(transcript);
        var revision = new StoryRevision(project.Id, 1, [new SceneDraft("Mbuya", "Mbuya taught me chinyakare stitching", TextOrigin.SourceQuotation, audio.Id, visual.Id, transcript.Id, 0, 57_000, caption)], Now);
        project.StoryRevisions.Add(revision);
        project.ReviewApprovals.Add(new ReviewApproval(project.Id, revision.Id, revision.SnapshotHash, owner, Now));
        project.LanguageReviews.Add(new LanguageReview(project.Id, revision.Id, revision.SnapshotHash, "Rudo Moyo", "Shona", "Home-language speaker", false, "", owner, Now));
        project.Credits.Add(new CreditEntry(project.Id, "Storyteller", "Rudo Moyo", audio.Id, 1, Now));
        project.CreditsApprovals.Add(new CreditsApproval(project.Id, CreditsRules.ComputeHash(project.Credits), owner, Now));
        return (project, audio, visual);
    }

    [Fact]
    public void Glossary_explains_each_approved_term_once_without_changing_caption_words()
    {
        var p = Guid.NewGuid();
        var approved = new GlossaryEntry(p, "chinyakare", "Shona", "the old, traditional way", Now); approved.Approve(Now);
        var draft = new GlossaryEntry(p, "Mbuya", "Shona", "grandmother", Now);
        var captions = new[] { "Before the market", "Mbuya taught me Chinyakare stitching", "Chinyakare still guides me", "chinyakaren is not the word" };
        var result = GlossaryRules.Annotate(captions, [approved, draft]);
        Assert.Equal("Before the market", result[0]);
        Assert.Equal("Mbuya taught me Chinyakare stitching\nchinyakare: the old, traditional way", result[1]);
        Assert.Equal("Chinyakare still guides me", result[2]);
        Assert.Equal(captions[3], result[3]);
        Assert.DoesNotContain("grandmother", string.Join("|", result));
    }

    [Fact]
    public void Editing_an_explanation_clears_its_approval_and_changes_the_export_hash()
    {
        var (project, _, _) = Ready();
        var before = new ExportReadinessService().Check(project);
        Assert.True(before.CanExportFinal);

        var entry = new GlossaryEntry(project.Id, "chinyakare", "Shona", "the old way", Now);
        project.Glossary.Add(entry);
        var pending = new ExportReadinessService().Check(project);
        Assert.False(pending.CanExportFinal);
        Assert.Contains(pending.Issues, x => x.Contains("Approve the explanation for “chinyakare”"));
        Assert.Equal(before.ExportHash, pending.ExportHash);

        entry.Approve(Now);
        var approved = new ExportReadinessService().Check(project);
        Assert.True(approved.CanExportFinal);
        Assert.NotEqual(before.ExportHash, approved.ExportHash);

        entry.Update("chinyakare", "Shona", "the traditional way", Now);
        Assert.False(entry.IsApproved);
        entry.Update("chinyakare", "Shona", "the traditional way", Now);
        Assert.False(entry.IsApproved);
    }

    [Fact]
    public void Contributor_answers_gate_export_declined_blocks_everything_not_asked_blocks_final()
    {
        var (project, _, visual) = Ready();
        visual.SetContributor("Aunt Chipo", ContributorUse.NotYetAsked, null, Now);
        var notAsked = new ExportReadinessService().Check(project);
        Assert.True(notAsked.CanPreview);
        Assert.False(notAsked.CanExportFinal);
        Assert.Contains(notAsked.Issues, x => x.Contains("Ask Aunt Chipo"));

        visual.SetContributor("Aunt Chipo", ContributorUse.Declined, "Said no on 9 Nov", Now);
        var declined = new ExportReadinessService().Check(project);
        Assert.False(declined.CanPreview);
        Assert.Contains(declined.Issues, x => x.Contains("Aunt Chipo declined"));

        visual.SetContributor("Aunt Chipo", ContributorUse.Agreed, "Agreed by phone", Now);
        Assert.True(new ExportReadinessService().Check(project).CanExportFinal);
        Assert.Equal("From Aunt Chipo · agreed to this use", ContributorRules.Describe(visual));
    }

    [Fact]
    public void Contributor_status_follows_whether_someone_else_supplied_the_source()
    {
        var (_, audio, _) = Ready();
        audio.SetContributor("", ContributorUse.Agreed, null, Now);
        Assert.Equal(ContributorUse.OwnMaterial, audio.ContributorUse);
        audio.SetContributor("Tendai", ContributorUse.OwnMaterial, null, Now);
        Assert.Equal(ContributorUse.NotYetAsked, audio.ContributorUse);
    }

    [Fact]
    public void Version_history_names_added_removed_edited_and_reordered_scenes()
    {
        var p = Guid.NewGuid(); var a = Guid.NewGuid(); var v = Guid.NewGuid(); var v2 = Guid.NewGuid();
        SceneDraft S(string title, string text = "words", Guid? visual = null) => new(title, text, TextOrigin.CreatorWritten, a, visual ?? v, null, 0, 10_000, "");
        var one = new StoryRevision(p, 1, [S("Opening"), S("Middle"), S("End")], Now);
        Assert.Equal(["First version with 3 scene(s)."], RevisionHistory.Describe(null, one));
        var two = new StoryRevision(p, 2, [S("End"), S("Opening", "new words", v2), S("Closing")], Now);
        var changes = RevisionHistory.Describe(one, two);
        Assert.Contains("Added “Closing”.", changes);
        Assert.Contains("Removed “Middle”.", changes);
        Assert.Contains("Edited “Opening”: words, picture.", changes);
        Assert.Contains("Changed the scene order.", changes);
        Assert.Equal(["No visible change."], RevisionHistory.Describe(one, new StoryRevision(p, 3, [S("Opening"), S("Middle"), S("End")], Now)));
    }

    [Fact]
    public void Story_type_and_idea_are_bounded_creator_input()
    {
        var project = new Project(Guid.NewGuid(), "Story", Now);
        project.SetStoryType(StoryType.Humour, Now);
        project.SetIdea("  How my uncle's taxi became a comedy club  ", Now);
        Assert.Equal("How my uncle's taxi became a comedy club", project.Idea);
        Assert.Throws<ArgumentException>(() => project.SetIdea(new string('x', 2001), Now));
        Assert.Throws<ArgumentException>(() => project.SetStoryType((StoryType)99, Now));
    }
}
