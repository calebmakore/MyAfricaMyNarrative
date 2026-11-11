using System.Text.RegularExpressions;

namespace MyAfricaMyNarrative.Application;

/// <summary>Where a checklist item is fixed. The view turns this into a link.</summary>
public enum FixPlace { None, ShapeCut, ShapeAddScene, ShapeLanguageReview, ShapeGlossary, Sources, MakeShots, Credits }

/// <summary>
/// A plain-language version of one export readiness issue: a short title, what to do, why it matters, and where to fix it.
/// The underlying rule message stays the source of truth; this only explains it.
/// </summary>
public sealed record ReadinessItem(string Title, string WhatToDo, string Why, FixPlace Place, string FixLabel, string Original);

public static partial class ReadinessGuide
{
    public static ReadinessItem Describe(string issue)
    {
        var scene = SceneNumber().Match(issue) is { Success: true } m ? $"Scene {m.Groups[1].Value}" : "A scene";
        ReadinessItem Item(string title, string todo, string why, FixPlace place, string label) => new(title, todo, why, place, label, issue);

        if (issue.Contains("quoted words do not match"))
            return Item($"{scene}: the quote doesn’t match what was said",
                $"In Shape, open {scene.ToLowerInvariant()} under “Your cut” and choose “Edit this scene”. Either change the words so they match the saved words exactly, or set “Whose words are these?” to “Written by me”.",
                "Words marked “From my recording” are shown as a real quote from the speaker, so they must match the checked words word for word. Rephrasing is fine — just mark it as written by you so nobody is misquoted.",
                FixPlace.ShapeCut, "Fix the scene");
        if (issue.Contains("must link to saved words"))
            return Item($"{scene}: choose which saved words it quotes",
                $"In Shape, edit {scene.ToLowerInvariant()} and pick the right entry in “Saved words to quote”. If the words are yours rather than a recording, switch to “Written by me”. No saved words yet? Check the words in step 2 of Shape first.",
                "A quote has to point at words you listened to and checked, so the film can prove who said what and when.",
                FixPlace.ShapeCut, "Fix the scene");
        if (issue.Contains("must sit inside the reviewed transcript range"))
            return Item($"{scene}: the clip runs past the checked words",
                $"In Shape, edit {scene.ToLowerInvariant()} and set its start and end inside the part of the recording you checked, or check a longer part in step 2.",
                "Only checked parts of a recording can be quoted, so every second on screen matches words you confirmed.",
                FixPlace.ShapeCut, "Fix the scene");
        if (issue.Contains("different recording than the scene audio") || issue.Contains("missing reviewed transcript") || issue.Contains("reviewed transcript does not exist"))
            return Item($"{scene}: its saved words don’t belong to its recording",
                $"In Shape, edit {scene.ToLowerInvariant()} and choose saved words from the same recording as its sound, or mark the words as written by you.",
                "The quote and the sound must come from the same recording so the words on screen are the words being heard.",
                FixPlace.ShapeCut, "Fix the scene");
        if (issue.Contains("fluent language review"))
            return Item("A fluent speaker needs to check the words",
                "In Shape, open “Language review” and enter who checked the captions and translation, and how they know the language. You can do it yourself if you speak it fluently.",
                "Your film shows words in local languages. A named, fluent person must confirm they are right — the app never claims language quality on its own. Any change to the story needs a new review.",
                FixPlace.ShapeLanguageReview, "Do the review");
        if (issue.Contains("Approve the current story revision"))
            return Item("Approve your cut",
                "In Shape, look over “Your cut” and press “Approve this version”.",
                "Approving tells the studio this exact version is the one you want. If you change anything later, you approve again.",
                FixPlace.ShapeCut, "Approve the cut");
        if (issue.Contains("needs permitted source audio") || issue.Contains("needs a permitted visual"))
            return Item($"{scene}: a recording or picture can’t be used",
                $"On the story page, check the sources used by {scene.ToLowerInvariant()}: give permission for them, or swap in another one in Shape.",
                "Only material you have marked as allowed can go into a film.",
                FixPlace.Sources, "Check sources");
        if (issue.Contains("declined"))
            return Item($"{scene}: someone said no to their material",
                $"Replace that material in {scene.ToLowerInvariant()}, or remove the scene.",
                "The person who supplied it declined, so it can’t appear in any film — not even a preview.",
                FixPlace.ShapeCut, "Fix the scene");
        if (issue.StartsWith("Ask "))
            return Item("Ask a contributor before the final film", issue,
                "Material from someone else needs their answer before it goes into a finished film. Previews are fine while you wait.",
                FixPlace.Sources, "Record their answer");
        if (issue.Contains("drops protected expression"))
            return Item($"{scene}: a protected word is missing from the caption",
                $"In Shape, edit {scene.ToLowerInvariant()} and put the protected word back into the caption or translation exactly as it was spoken.",
                "You marked these words to stay exactly as spoken. If they disappear from what’s on screen, their meaning can be lost.",
                FixPlace.ShapeCut, "Fix the caption");
        if (issue.Contains("planned footage missing"))
            return Item($"{scene}: a planned shot hasn’t been filmed",
                (Quoted(issue) is { } shot ? $"Planned shot: “{shot.TrimEnd('.', ' ')}” — " : "") + "In Make, mark it as filmed once you have it, or drop it from the shot list.",
                "Your shot list says this footage is still to come, so the final film would be incomplete.", FixPlace.MakeShots, "Open shot list");
        if (issue.StartsWith("Add credits"))
            return Item("Add the credits", "In Credits above, add the storyteller and anyone who appears or supplied material, then approve them.",
                "Everyone who helped is named on a 5-second card at the end of the film.", FixPlace.Credits, "Go to credits");
        if (issue.Contains("Approve the current credits"))
            return Item("Approve the credits", "Check the names above are spelled right, then press “Approve credits”.",
                "Approval covers these exact names. Changing any credit means approving again.", FixPlace.Credits, "Go to credits");
        if (issue.Contains("duration"))
            return Item("Make the film 60–90 seconds long", "In Shape, shorten or remove scenes if it’s too long, or add a scene if it’s too short. The bar below shows the length.",
                "The final film is a short 60–90 second story including the 5-second credits card. Any length works for a preview.", FixPlace.ShapeCut, "Adjust scenes");
        if (issue.StartsWith("Approve the explanation"))
            return Item(Quoted(issue) is { } term ? $"Approve the explanation for “{term}”" : "Approve a word explanation",
                "In Shape, open “Keep your words, explain them once” and approve or remove the explanation.",
                "The word appears in a caption, so its explanation would be shown on screen — it needs your approval first.", FixPlace.ShapeGlossary, "Open glossary");
        if (issue.Contains("source-backed scene"))
            return Item("Add your first scene", "In Shape, build a scene from a recording and a picture.", "A film is made of scenes.", FixPlace.ShapeAddScene, "Add a scene");
        return Item(issue, "", "", issue.StartsWith("Scene") ? FixPlace.ShapeCut : FixPlace.None, "Fix in Shape");
    }

    /// <summary>The first “…” quoted value in a rule message (a term or a shot description), if any.</summary>
    private static string? Quoted(string issue) => QuotedValue().Match(issue) is { Success: true } m ? m.Groups[1].Value : null;

    [GeneratedRegex(@"^Scene (\d+)")] private static partial Regex SceneNumber();
    [GeneratedRegex(@"“([^”]+)”")] private static partial Regex QuotedValue();
}
