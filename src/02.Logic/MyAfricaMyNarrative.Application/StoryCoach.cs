using MyAfricaMyNarrative.Domain;

namespace MyAfricaMyNarrative.Application;

public enum StoryBeat { Hook, Context, TurningPoint, Resolution }
public enum StoryGapKind { TurningPoint, Context, Ending, Length, Other }

public sealed record BeatAssignment(StoryBeat Beat, IReadOnlyList<Guid> SceneIds, string Note);
/// <summary>A gap in the story. <see cref="SuggestedNarration"/> is AI-written text and must be shown as a suggestion the creator can accept, edit or ignore.</summary>
public sealed record StoryGap(StoryGapKind Kind, string Question, string SuggestedNarration);
public sealed record StoryStructure(IReadOnlyList<BeatAssignment> Beats, IReadOnlyList<StoryGap> Gaps, string Summary);

public sealed record StoryCoachScene(Guid Id, int Order, string Title, int Seconds, string Origin, string? Words);
/// <summary>
/// What the coach may read. <see cref="StoryCoachScene.Words"/> is only filled when the creator explicitly agrees to share
/// their reviewed words for that request; otherwise only titles, lengths and the creator's own intention are sent.
/// </summary>
public sealed record StoryCoachRequest(IReadOnlyList<StoryCoachScene> Scenes, string Audience, string Meaning, string Tone,
    string LocalContext, string ProtectedTerms, string CreatorAnswers, bool WordsShared, string StoryType = "", string Idea = "");

public sealed record StoryCoachResult(StoryStructure Structure, string Provider, string Model);

public interface IStoryCoach
{
    Task<StoryCoachResult> ProposeStructureAsync(StoryCoachRequest request, CancellationToken cancellationToken);
}

public static class StoryCoachRules
{
    public const string PracticeSource = "Practice coach (no AI)";

    public static StoryCoachRequest BuildRequest(Project project, StoryRevision revision, bool shareWords)
    {
        var intent = project.CreatorIntents.SingleOrDefault();
        var answers = string.Join("\n", project.CreatorQuestions.Where(x => x.AnsweredAtUtc is not null).Select(x => $"{x.Field}: {x.Answer}"));
        var transcripts = project.TranscriptRevisions.ToDictionary(x => x.Id);
        var scenes = revision.Scenes.OrderBy(x => x.Order).Select(x =>
        {
            string? words = null;
            if (shareWords)
                words = x.Text.Length > 0 ? x.Text : x.TranscriptRevisionId is { } t && transcripts.TryGetValue(t, out var tr) ? tr.Text : x.EffectiveCaption;
            if (words is { Length: > 1200 }) words = words[..1200];
            return new StoryCoachScene(x.Id, x.Order, x.Title, (int)Math.Max(1, Math.Round((x.EndMs - x.StartMs) / 1000.0)), x.TextOrigin.ToString(), words);
        }).ToList();
        return new StoryCoachRequest(scenes, intent?.Audience ?? "", intent?.Meaning ?? "", intent?.Tone ?? "", intent?.LocalContext ?? "", intent?.ProtectedTerms ?? "", answers, shareWords,
            project.StoryType == Domain.StoryType.Unspecified ? "" : project.StoryType.ToString(), project.Idea);
    }

    /// <summary>
    /// A deterministic structure: opening hook, context, turning point and resolution by position, and gaps found from facts the app knows
    /// (scene count, total length, answered context, how the story ends). It never writes narration.
    /// </summary>
    public static StoryStructure Practice(StoryCoachRequest request)
    {
        var scenes = request.Scenes;
        var beats = new List<BeatAssignment>();
        if (scenes.Count == 1) beats.Add(new(StoryBeat.Hook, [scenes[0].Id], "Your only scene opens and closes the story."));
        else
        {
            var turnIndex = scenes.Count >= 4 ? (int)Math.Round((scenes.Count - 1) * 2 / 3.0) : -1;
            if (turnIndex == scenes.Count - 1) turnIndex--;
            beats.Add(new(StoryBeat.Hook, [scenes[0].Id], "Opens the story: the first seconds decide whether people keep watching."));
            var context = scenes.Skip(1).Take(scenes.Count - 2).Where((_, i) => i + 1 != turnIndex).Select(x => x.Id).ToList();
            if (context.Count > 0) beats.Add(new(StoryBeat.Context, context, "Gives viewers what they need to follow and respect the story."));
            if (turnIndex > 0) beats.Add(new(StoryBeat.TurningPoint, [scenes[turnIndex].Id], "Where something changes. Check that this scene really carries the change."));
            beats.Add(new(StoryBeat.Resolution, [scenes[^1].Id], "Closes the story and leaves the meaning you chose."));
        }

        var gaps = new List<StoryGap>();
        var answered = request.CreatorAnswers;
        if (scenes.Count < 4 && !answered.Contains("Turning point:", StringComparison.OrdinalIgnoreCase))
            gaps.Add(new(StoryGapKind.TurningPoint, "What was the moment things changed — for you, the person or the place in this story?", ""));
        if (string.IsNullOrWhiteSpace(request.LocalContext) && !answered.Contains("Context:", StringComparison.OrdinalIgnoreCase))
            gaps.Add(new(StoryGapKind.Context, "What would someone from outside need to know about this place, practice or expression to understand it fairly?", ""));
        if (scenes.Count > 0 && scenes[^1].Origin == nameof(TextOrigin.SourceQuotation) && !answered.Contains("Ending:", StringComparison.OrdinalIgnoreCase))
            gaps.Add(new(StoryGapKind.Ending, "Your story ends on a quote. What do you want viewers to carry away — would a closing line in your own words help?", ""));
        var seconds = scenes.Sum(x => x.Seconds) + (int)(CreditsRules.CardDurationMs / 1000);
        if (seconds < 60) gaps.Add(new(StoryGapKind.Length, $"With credits the film is about {seconds} s. What else belongs in it to reach at least 60 s?", ""));
        else if (seconds > 90) gaps.Add(new(StoryGapKind.Length, $"With credits the film is about {seconds} s. Which scene could be shorter to stay within 90 s?", ""));

        var summary = gaps.Count == 0 ? "The structure holds: an opening, context, a turn and an ending. Listen through once more before approving."
            : $"{gaps.Count} thing(s) to look at before you approve. Answer the questions in your own words — only your answers become facts in the story.";
        return new StoryStructure(beats, gaps, summary);
    }

    /// <summary>Rejects structures that invent, drop or repeat scenes, and bounds all text.</summary>
    public static (StoryStructure? Structure, string? Error) Validate(StoryStructure structure, IReadOnlyList<Guid> sceneIds)
    {
        var all = structure.Beats.SelectMany(x => x.SceneIds).ToList();
        if (all.Count != sceneIds.Count || all.Distinct().Count() != all.Count || all.Any(x => !sceneIds.Contains(x)))
            return (null, "The structure must place every current scene exactly once and cannot invent scenes.");
        if (structure.Gaps.Count > 6) return (null, "Too many gaps were suggested.");
        static string Bound(string value, int max) { value = value.Trim(); return value.Length > max ? value[..max] : value; }
        var beats = structure.Beats.Where(x => x.SceneIds.Count > 0).OrderBy(x => x.Beat)
            .Select(x => x with { Note = Bound(x.Note, 300) }).ToList();
        var gaps = structure.Gaps.Where(x => x.Question.Trim().Length >= 5)
            .Select(x => x with { Question = Bound(x.Question, 280), SuggestedNarration = Bound(x.SuggestedNarration, 600) }).ToList();
        return (new StoryStructure(beats, gaps, Bound(structure.Summary, 1000)), null);
    }

    /// <summary>Scene order implied by the beats: hook, context, turning point, resolution.</summary>
    public static IReadOnlyList<Guid> Order(StoryStructure structure) => structure.Beats.OrderBy(x => x.Beat).SelectMany(x => x.SceneIds).ToList();

    public static string FieldFor(StoryGapKind kind) => kind switch
    {
        StoryGapKind.TurningPoint => "Turning point", StoryGapKind.Context => "Context", StoryGapKind.Ending => "Ending", StoryGapKind.Length => "Length", _ => "Story"
    };

    public static string Label(StoryBeat beat) => beat switch
    {
        StoryBeat.Hook => "Opening", StoryBeat.Context => "Context", StoryBeat.TurningPoint => "Turning point", _ => "Ending"
    };
}
