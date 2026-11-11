using MyAfricaMyNarrative.Domain;

namespace MyAfricaMyNarrative.Application;

/// <summary>Where a suggested explanation came from, so the creator knows how much to check it.</summary>
public enum ExplanationBasis { CreatorContext, CommonMeaning, Unknown }

/// <summary>
/// A proposed glossary entry. The term is exactly as it appears in a caption; the explanation is a draft the creator
/// edits or approves (empty when the meaning is not known — never guessed).
/// </summary>
public sealed record GlossarySuggestion(string Term, string Language, string Explanation, ExplanationBasis Basis, string SceneTitle);

public sealed record GlossaryCaption(string SceneTitle, string Caption, string Translation);

public sealed record GlossarySuggestionRequest(IReadOnlyList<GlossaryCaption> Captions, string LocalContext, IReadOnlyList<string> ProtectedTerms,
    IReadOnlyList<string> ExistingTerms, string TranslationLanguage);

public sealed record GlossarySuggestionResult(IReadOnlyList<GlossarySuggestion> Suggestions, string Provider, string Model);

public interface IGlossaryAssistant
{
    Task<GlossarySuggestionResult> SuggestAsync(GlossarySuggestionRequest request, CancellationToken ct);
}

public static class GlossarySuggestionRules
{
    public const int MaxSuggestions = 8;
    public const int MaxScenes = 30;
    public const string PracticeSource = "Practice finder (no AI)";

    public static GlossarySuggestionRequest BuildRequest(Project project, StoryRevision revision)
    {
        var intent = project.CreatorIntents.SingleOrDefault();
        return new GlossarySuggestionRequest(
            revision.Scenes.OrderBy(x => x.Order).Take(MaxScenes).Select(x => new GlossaryCaption(x.Title, Bound(x.EffectiveCaption, 2000), Bound(x.Translation, 2000))).ToList(),
            Bound(intent?.LocalContext ?? "", 1500),
            SourceFidelity.ParseProtectedTerms(intent?.ProtectedTerms),
            project.Glossary.Select(x => x.Term).ToList(),
            intent?.TranslationLanguage ?? "");
    }

    /// <summary>No AI: the creator's own protected terms that appear in a caption and have no explanation yet. Explanations stay empty for the creator to write.</summary>
    public static IReadOnlyList<GlossarySuggestion> Practice(GlossarySuggestionRequest request) =>
        request.ProtectedTerms.Select(term => new GlossarySuggestion(term, "", "", ExplanationBasis.Unknown, "")).ToList();

    /// <summary>
    /// Keeps only suggestions whose term appears in a real caption or translation, is not already explained, and is not repeated.
    /// Uses the caption's own spelling, records the first scene it appears in, and blanks explanations that are too long or have no basis.
    /// </summary>
    public static IReadOnlyList<GlossarySuggestion> Validate(GlossarySuggestionRequest request, IEnumerable<GlossarySuggestion> suggestions)
    {
        var seen = new HashSet<string>(request.ExistingTerms.Select(x => x.Trim()), StringComparer.OrdinalIgnoreCase);
        var result = new List<GlossarySuggestion>();
        foreach (var raw in suggestions)
        {
            var term = (raw.Term ?? "").Trim();
            if (term.Length is < 2 or > 80 || !seen.Add(term)) continue;
            var scene = request.Captions.FirstOrDefault(c => GlossaryRules.Appears(c.Caption, term) || GlossaryRules.Appears(c.Translation, term));
            if (scene is null) continue;
            var explanation = (raw.Explanation ?? "").Trim();
            var basis = raw.Basis;
            if (basis == ExplanationBasis.Unknown || explanation.Length is < 2 or > 160) { explanation = ""; basis = ExplanationBasis.Unknown; }
            var language = (raw.Language ?? "").Trim();
            result.Add(new GlossarySuggestion(term, language.Length > 40 ? language[..40] : language, explanation, basis, scene.SceneTitle));
            if (result.Count == MaxSuggestions) break;
        }
        return result;
    }

    public static string BasisLabel(ExplanationBasis basis) => basis switch
    {
        ExplanationBasis.CreatorContext => "Drafted from your local context note",
        ExplanationBasis.CommonMeaning => "General meaning — check it fits your story",
        _ => "Meaning not known — write it in your words"
    };

    private static string Bound(string value, int max) => value.Length > max ? value[..max] : value;
}
