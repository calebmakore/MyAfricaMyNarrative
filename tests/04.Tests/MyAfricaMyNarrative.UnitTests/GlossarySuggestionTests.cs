using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;

namespace MyAfricaMyNarrative.UnitTests;

/// <summary>AI can find words to explain, but only words really in the captions, and its explanations stay drafts the creator checks.</summary>
public sealed class GlossarySuggestionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    private static GlossarySuggestionRequest Request(params string[] existing) => new(
        [new GlossaryCaption("Mbuya at the machine", "Mbuya taught me chinyakare stitching.", ""),
         new GlossaryCaption("Street", "Then the street came in.", "Ipapo mugwagwa wakapinda.")],
        "Chinyakare means the old, traditional way.", ["chinyakare", "roora"], existing, "Shona");

    [Fact]
    public void Only_terms_found_in_captions_survive_with_caption_spelling_and_scene()
    {
        var safe = GlossarySuggestionRules.Validate(Request(), [
            new("Chinyakare", "Shona", "The old, traditional way of doing things", ExplanationBasis.CreatorContext, ""),
            new("sadza", "Shona", "Stiff maize porridge", ExplanationBasis.CommonMeaning, ""),         // not in any caption
            new("mugwagwa", "Shona", "Road or street", ExplanationBasis.CommonMeaning, ""),            // in the translation
            new("chinyakare", "Shona", "Duplicate", ExplanationBasis.CreatorContext, "")]);
        Assert.Equal(["Chinyakare", "mugwagwa"], safe.Select(x => x.Term));
        Assert.Equal("Mbuya at the machine", safe[0].SceneTitle);
        Assert.Equal("Street", safe[1].SceneTitle);
    }

    [Fact]
    public void Unknown_basis_or_overlong_explanations_are_blanked_and_existing_terms_skipped()
    {
        var safe = GlossarySuggestionRules.Validate(Request("Mbuya"), [
            new("Mbuya", "Shona", "Grandmother", ExplanationBasis.CommonMeaning, ""),
            new("chinyakare", "Shona", "A guess", ExplanationBasis.Unknown, ""),
            new("mugwagwa", "Shona", new string('x', 161), ExplanationBasis.CommonMeaning, "")]);
        Assert.Equal(["chinyakare", "mugwagwa"], safe.Select(x => x.Term));
        Assert.All(safe, x => { Assert.Equal("", x.Explanation); Assert.Equal(ExplanationBasis.Unknown, x.Basis); });
    }

    [Fact]
    public void Practice_suggests_protected_terms_in_captions_without_inventing_explanations()
    {
        var request = Request();
        var safe = GlossarySuggestionRules.Validate(request, GlossarySuggestionRules.Practice(request));
        var only = Assert.Single(safe);
        Assert.Equal("chinyakare", only.Term);   // "roora" is protected but not in a caption
        Assert.Equal("", only.Explanation);
    }

    [Fact]
    public void Ai_draft_flag_clears_when_the_creator_rewrites_the_explanation_and_marks_the_hash()
    {
        var project = Guid.NewGuid();
        var entry = new GlossaryEntry(project, "chinyakare", "Shona", "The old way", Now, aiDrafted: true);
        Assert.True(entry.AiDrafted);
        entry.Approve(Now);
        var aiHash = GlossaryRules.Hash([entry]);
        entry.Update("chinyakare", "Shona", "The old way", Now);      // no change keeps the draft flag and approval
        Assert.True(entry.AiDrafted); Assert.True(entry.IsApproved);
        entry.Update("chinyakare", "Shona", "How our grandmothers did it", Now);
        Assert.False(entry.AiDrafted); Assert.False(entry.IsApproved);

        var written = new GlossaryEntry(project, "chinyakare", "Shona", "The old way", Now); written.Approve(Now);
        Assert.NotEqual(aiHash, GlossaryRules.Hash([written]));
    }
}
