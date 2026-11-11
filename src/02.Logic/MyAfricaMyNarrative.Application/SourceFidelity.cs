using System.Text.RegularExpressions;
using MyAfricaMyNarrative.Domain;

namespace MyAfricaMyNarrative.Application;

/// <summary>
/// Source-integrity rules for scenes: a recorded quotation must point at a reviewed transcript of the same audio,
/// stay inside that reviewed range, and repeat words that were actually reviewed. Protected expressions spoken in
/// the source must survive into the on-screen caption.
/// </summary>
public static partial class SourceFidelity
{
    public static string? ValidateScene(SceneDraft draft, TranscriptRevision? transcript)
    {
        if (draft.TranscriptRevisionId is not null && (transcript is null || transcript.Id != draft.TranscriptRevisionId))
            return "The selected reviewed transcript does not exist in this project.";
        if (transcript is not null && transcript.AssetId != draft.AudioAssetId)
            return "The reviewed transcript belongs to a different recording than the scene audio.";
        if (draft.TextOrigin != TextOrigin.SourceQuotation) return null;
        if (transcript is null)
            return "Words marked “From my recording” must link to saved words from that recording. Choose them in “Saved words to quote”, or mark the words as “Written by me”.";
        if (draft.StartMs < transcript.StartMs || draft.EndMs > transcript.EndMs)
            return $"The scene range must sit inside the reviewed transcript range ({transcript.StartMs}–{transcript.EndMs} ms).";
        var quoted = Normalize(draft.Text);
        // Empty scene text means the scene quotes the reviewed range itself; nothing new is attributed to the speaker.
        if (quoted.Length > 0 && !Normalize(transcript.Text).Contains(quoted, StringComparison.Ordinal))
            return "The quoted words do not match the saved words from the recording. Make them match exactly, or mark the words as “Written by me”.";
        return null;
    }

    public static IReadOnlyList<string> ParseProtectedTerms(string? value) =>
        string.IsNullOrWhiteSpace(value) ? [] :
        TermSeparator().Split(value).Select(x => x.Trim().Trim('"', '“', '”', '\'')).Where(x => x.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Protected terms that occur in the reviewed source words but are missing from the caption shown on screen.</summary>
    /// <summary>
    /// The source words a scene actually puts on screen: its quoted text when it quotes part of a reviewed transcript,
    /// otherwise the whole reviewed range. Protected terms are only required where they are spoken in that part.
    /// </summary>
    public static string SpokenInScene(SceneDraft draft, TranscriptRevision transcript) =>
        draft.TextOrigin == TextOrigin.SourceQuotation && Normalize(draft.Text).Length > 0 ? draft.Text : transcript.Text;

    public static IReadOnlyList<string> MissingProtectedTerms(IEnumerable<string> terms, string sourceText, string caption) =>
        terms.Where(term => sourceText.Contains(term, StringComparison.OrdinalIgnoreCase) && !caption.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();

    public static string Normalize(string value) => Whitespace().Replace(value.Replace('’', '\'').Replace('‘', '\''), " ").Trim();

    [GeneratedRegex(@"[\r\n,;·|]+")] private static partial Regex TermSeparator();
    [GeneratedRegex(@"\s+")] private static partial Regex Whitespace();
}
