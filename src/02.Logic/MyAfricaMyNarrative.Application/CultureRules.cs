using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MyAfricaMyNarrative.Domain;

namespace MyAfricaMyNarrative.Application;

/// <summary>Local expressions keep their original wording; an approved, creator-written explanation is shown the first time each appears.</summary>
public static class GlossaryRules
{
    public const int MaxEntries = 30;

    public static IReadOnlyList<GlossaryEntry> Approved(IEnumerable<GlossaryEntry> entries) =>
        entries.Where(x => x.IsApproved).OrderBy(x => x.Term, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id).ToList();

    /// <summary>Empty when nothing is approved, so films without a glossary keep their earlier export hash.</summary>
    public static string Hash(IEnumerable<GlossaryEntry> entries)
    {
        var approved = Approved(entries);
        if (approved.Count == 0) return string.Empty;
        var canonical = string.Join('\n', approved.Select(x => $"{x.Term}|{x.Language}|{x.Explanation}" + (x.AiDrafted ? "|ai" : "")));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    public static bool Appears(string caption, string term) =>
        Regex.IsMatch(caption, $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(term)}(?![\p{{L}}\p{{N}}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));

    /// <summary>
    /// Returns each caption with "term: explanation" lines added under the first caption where each approved term appears.
    /// The caption words themselves are never changed.
    /// </summary>
    public static IReadOnlyList<string> Annotate(IReadOnlyList<string> captions, IEnumerable<GlossaryEntry> entries)
    {
        var pending = Approved(entries).ToList();
        var result = new List<string>(captions.Count);
        foreach (var caption in captions)
        {
            var notes = pending.Where(x => Appears(caption, x.Term)).ToList();
            foreach (var note in notes) pending.Remove(note);
            result.Add(notes.Count == 0 ? caption : caption + "\n" + string.Join('\n', notes.Select(x => $"{x.Term}: {x.Explanation}")));
        }
        return result;
    }
}

public static class ContributorRules
{
    public static string Describe(MediaAsset asset) => asset.ContributorUse switch
    {
        ContributorUse.OwnMaterial => "Your own material",
        ContributorUse.NotYetAsked => $"From {asset.SuppliedBy} · not asked yet",
        ContributorUse.Agreed => $"From {asset.SuppliedBy} · agreed to this use",
        _ => $"From {asset.SuppliedBy} · declined"
    };
}

/// <summary>Plain-language differences between two story versions, computed from the immutable revisions.</summary>
public static class RevisionHistory
{
    public static IReadOnlyList<string> Describe(StoryRevision? previous, StoryRevision current)
    {
        if (previous is null) return [$"First version with {current.Scenes.Count} scene(s)."];
        var before = previous.Scenes.OrderBy(x => x.Order).ToList();
        var after = current.Scenes.OrderBy(x => x.Order).ToList();
        var changes = new List<string>();
        var beforeTitles = before.Select(x => x.Title).ToList();
        var afterTitles = after.Select(x => x.Title).ToList();
        foreach (var added in after.Where(x => !beforeTitles.Contains(x.Title, StringComparer.OrdinalIgnoreCase))) changes.Add($"Added “{added.Title}”.");
        foreach (var removed in before.Where(x => !afterTitles.Contains(x.Title, StringComparer.OrdinalIgnoreCase))) changes.Add($"Removed “{removed.Title}”.");
        foreach (var scene in after)
        {
            var old = before.FirstOrDefault(x => string.Equals(x.Title, scene.Title, StringComparison.OrdinalIgnoreCase));
            if (old is null) continue;
            var what = new List<string>();
            if (old.Text != scene.Text) what.Add("words");
            if (old.TextOrigin != scene.TextOrigin) what.Add("whose words");
            if (old.Caption != scene.Caption) what.Add("caption");
            if (old.Translation != scene.Translation) what.Add("translation");
            if (old.StartMs != scene.StartMs || old.EndMs != scene.EndMs || old.AudioAssetId != scene.AudioAssetId) what.Add("voice timing");
            if (old.VisualAssetId != scene.VisualAssetId || old.MorePictures != scene.MorePictures) what.Add(scene.MorePictureIds.Count > 0 ? "pictures" : "picture");
            if (old.KeepClipSound != scene.KeepClipSound || old.ClipStartMs != scene.ClipStartMs) what.Add("clip sound and start");
            if (what.Count > 0) changes.Add($"Edited “{scene.Title}”: {string.Join(", ", what)}.");
        }
        var common = afterTitles.Where(t => beforeTitles.Contains(t, StringComparer.OrdinalIgnoreCase)).ToList();
        var commonBefore = beforeTitles.Where(t => common.Contains(t, StringComparer.OrdinalIgnoreCase)).ToList();
        if (!common.SequenceEqual(commonBefore, StringComparer.OrdinalIgnoreCase)) changes.Add("Changed the scene order.");
        return changes.Count == 0 ? ["No visible change."] : changes;
    }
}
