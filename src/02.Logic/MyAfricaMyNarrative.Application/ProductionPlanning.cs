using System.Security.Cryptography;
using System.Text;
using MyAfricaMyNarrative.Domain;

namespace MyAfricaMyNarrative.Application;

public sealed record PlanScene(Guid Id, int Order, string Title, int Seconds, Guid VisualAssetId, MediaKind? VisualKind, string? VisualName, bool IsQuotation);
public sealed record PlanResource(Guid Id, ResourceKind Kind, string Name, string Availability, int? Minutes);
public sealed record PlanVisual(Guid Id, string Name, MediaKind Kind);

/// <summary>Everything a planner may use: the current story and only what the creator has declared as really available.</summary>
public sealed record ProductionPlanContext(StoryRevision Revision, IReadOnlyList<PlanScene> Scenes, IReadOnlyList<PlanResource> Available,
    IReadOnlyList<PlanVisual> Visuals, int? TimeBudgetMinutes, string ResourceHash, string ResourceSnapshot,
    string Audience, string Meaning, string Tone, string ProtectedTerms)
{
    public static ProductionPlanContext? From(Project project)
    {
        var revision = project.StoryRevisions.OrderByDescending(x => x.RevisionNumber).FirstOrDefault();
        if (revision is null || revision.Scenes.Count == 0) return null;
        var assets = project.MediaAssets.Where(x => x.PermissionStatus == PermissionStatus.Granted).ToDictionary(x => x.Id);
        var scenes = revision.Scenes.OrderBy(x => x.Order).Select(x =>
        {
            assets.TryGetValue(x.VisualAssetId, out var visual);
            var seconds = (int)Math.Clamp(Math.Round((x.EndMs - x.StartMs) / 1000.0), 1, 300);
            return new PlanScene(x.Id, x.Order, x.Title, seconds, x.VisualAssetId, visual?.Kind, visual?.DisplayName, x.TextOrigin == TextOrigin.SourceQuotation);
        }).ToList();
        var available = project.Resources.Where(x => x.IsAvailable).OrderBy(x => x.Kind).ThenBy(x => x.CreatedAtUtc)
            .Select(x => new PlanResource(x.Id, x.Kind, x.Name, x.Availability, x.Minutes)).ToList();
        var visuals = assets.Values.Where(x => x.Kind != MediaKind.Audio).OrderBy(x => x.CreatedAtUtc).Select(x => new PlanVisual(x.Id, x.DisplayName, x.Kind)).ToList();
        var timed = available.Where(x => x.Kind == ResourceKind.Time && x.Minutes is not null).ToList();
        int? budget = timed.Count == 0 ? null : timed.Sum(x => x.Minutes!.Value);
        var intent = project.CreatorIntents.SingleOrDefault();
        return new ProductionPlanContext(revision, scenes, available, visuals, budget, HashResources(project.Resources), SnapshotResources(project.Resources),
            intent?.Audience ?? "", intent?.Meaning ?? "", intent?.Tone ?? "", intent?.ProtectedTerms ?? "");
    }

    public static string HashResources(IEnumerable<ResourceItem> resources) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', resources.OrderBy(x => x.Id)
            .Select(x => $"{x.Id}|{(int)x.Kind}|{x.Name}|{x.Minutes}|{x.IsAvailable}"))))).ToLowerInvariant();

    public static string SnapshotResources(IEnumerable<ResourceItem> resources) =>
        string.Join('\n', resources.OrderBy(x => x.Kind).ThenBy(x => x.CreatedAtUtc)
            .Select(x => $"{x.Id}|{x.Kind}|{x.Name.Replace('|', '/')}|{(x.IsAvailable ? "available" : "unavailable")}"));
}

public sealed record ProductionPlanProposal(IReadOnlyList<PlanStepDraft> Steps, string Summary, string Source, bool IsFixture);

/// <summary>Live AI production planning. Implementations must return only IDs supplied in the context.</summary>
public interface IProductionPlanAssistant
{
    Task<ProductionPlanProposal> ProposeAsync(ProductionPlanContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Deterministic planner used without AI. It only rearranges what the creator declared: it never invents people, places or footage,
/// and it fits filming into the declared time by switching lower-priority scenes to existing pictures.
/// </summary>
/// <summary>How many scenes can be filmed new with what was declared. FitScenes is in filming-priority order.</summary>
public sealed record FilmingCapacity(bool CanFilm, int SetupMinutes, int MinutesPerShot, IReadOnlyList<Guid> FitScenes);

public static class PracticePlanner
{
    public const string SourceName = "Practice planner (no AI)";
    public const int SetupMinutesPerLocation = 10;
    public const int PersonShotMinutes = 20;
    public const int DetailShotMinutes = 15;

    /// <summary>
    /// What new filming the declared resources and time allow: set-up once, then one shot per scene,
    /// opening and ending first, then the middle in story order. Shared by the practice planner, the Live prompt and the plan check.
    /// </summary>
    public static FilmingCapacity Capacity(ProductionPlanContext context)
    {
        var people = context.Available.Count(x => x.Kind == ResourceKind.Person);
        var canFilm = context.Available.Any(x => x.Kind == ResourceKind.Equipment) && context.Available.Any(x => x.Kind == ResourceKind.Location);
        var shot = people >= 2 ? PersonShotMinutes : DetailShotMinutes;
        if (!canFilm) return new FilmingCapacity(false, SetupMinutesPerLocation, shot, []);
        var scenes = context.Scenes;
        var priority = scenes.Count <= 2 ? scenes.ToList() : new[] { scenes[0], scenes[^1] }.Concat(scenes.Skip(1).Take(scenes.Count - 2)).ToList();
        var remaining = context.TimeBudgetMinutes is { } budget ? budget - SetupMinutesPerLocation : int.MaxValue;
        var fit = new List<Guid>();
        foreach (var scene in priority)
            if (shot <= remaining) { fit.Add(scene.Id); remaining -= shot; }
        return new FilmingCapacity(true, SetupMinutesPerLocation, shot, fit);
    }

    public static ProductionPlanProposal Plan(ProductionPlanContext context)
    {
        var people = context.Available.Where(x => x.Kind == ResourceKind.Person).ToList();
        var equipment = context.Available.FirstOrDefault(x => x.Kind == ResourceKind.Equipment);
        var location = context.Available.FirstOrDefault(x => x.Kind == ResourceKind.Location);
        var capacity = Capacity(context);
        var canFilm = capacity.CanFilm;
        var budget = context.TimeBudgetMinutes;
        var scenes = context.Scenes;
        var film = capacity.FitScenes.ToHashSet();

        var spare = new Queue<PlanVisual>(context.Visuals.Where(v => scenes.All(s => s.VisualAssetId != v.Id)));
        var steps = new List<PlanStepDraft>();
        var setupAdded = false;
        for (var index = 0; index < scenes.Count; index++)
        {
            var scene = scenes[index];
            var position = index == 0 ? 0 : index == scenes.Count - 1 ? 2 : 1;
            if (film.Contains(scene.Id))
            {
                string what, why; int minutes; List<Guid> ids;
                if (people.Count >= 2)
                {
                    var cam = people[0].Name; var op = people[1].Name;
                    (what, why) = position switch
                    {
                        0 => ($"{op} films a wide shot of {location!.Name} as {cam} settles in, then a steady medium shot of {cam}.", $"Opens “{scene.Title}” by showing where the story happens and who is telling it."),
                        2 => ($"{op} films {cam} at {location!.Name} in one slow, steady medium shot, holding still for a few seconds at the end.", $"Gives “{scene.Title}” a calm closing image to land the story."),
                        _ => index % 2 == 1
                            ? ($"{op} films close-ups of {cam}'s hands and the tools or objects that matter in this part of the story, at {location!.Name}.", $"Shows the detail behind “{scene.Title}” while {cam}'s own voice carries it.")
                            : ($"{op} films {cam} from the side at {location!.Name}, mid-task, in one steady medium shot.", $"Keeps {cam} present on screen during “{scene.Title}”.")
                    };
                    minutes = PersonShotMinutes; ids = [people[0].Id, people[1].Id, location!.Id, equipment!.Id];
                }
                else
                {
                    (what, why) = position switch
                    {
                        0 => ($"Prop {equipment!.Name} steady and record a wide shot of {location!.Name}, the place where the story happens.", $"Sets the scene for “{scene.Title}” without needing anyone behind the camera."),
                        2 => ($"With {equipment!.Name} on a steady surface at {location!.Name}, record a slow detail shot to close on — an object, a doorway, the light.", $"Ends “{scene.Title}” on a quiet image while the narrator's voice finishes."),
                        _ => ($"Prop {equipment!.Name} steady at {location!.Name} and film close-up details: hands at work, tools or objects that matter in this part of the story.", $"Needs no second person: the narrator's voice stays central and the details give “{scene.Title}” texture.")
                    };
                    minutes = DetailShotMinutes; ids = [location!.Id, equipment!.Id];
                    if (people.Count == 1) ids.Add(people[0].Id);
                }
                if (!setupAdded) { what += $" Allow {SetupMinutesPerLocation} min first to set up at {location!.Name}."; minutes += SetupMinutesPerLocation; setupAdded = true; }
                steps.Add(new PlanStepDraft(scene.Id, scene.Title, PlanApproach.FilmNew, $"{what} Aim for about {scene.Seconds} s of usable footage.", scene.Seconds, minutes, why, ids, null));
            }
            else
            {
                var picture = scene.VisualKind is not null ? new PlanVisual(scene.VisualAssetId, scene.VisualName ?? "your picture", scene.VisualKind.Value) : spare.Count > 0 ? spare.Dequeue() : null;
                if (picture is null)
                {
                    steps.Add(new PlanStepDraft(scene.Id, scene.Title, PlanApproach.UseExisting,
                        $"No usable picture is available for “{scene.Title}”. Upload a photo or clip in Record, or declare a phone and a place so it can be filmed.",
                        scene.Seconds, 0, "Flags a real gap instead of pretending footage exists.", [], null));
                    continue;
                }
                var move = picture.Kind == MediaKind.Image ? "a slow push-in" : "a trimmed section";
                steps.Add(new PlanStepDraft(scene.Id, scene.Title, PlanApproach.UseExisting,
                    $"Use “{picture.Name}” with {move} for about {scene.Seconds} s. Nothing new to film.",
                    scene.Seconds, 0,
                    $"Uses material you already have, so “{scene.Title}” costs no filming time.",
                    [], picture.Id));
            }
        }

        var filmed = steps.Count(x => x.Approach == PlanApproach.FilmNew);
        var total = steps.Sum(x => x.ShootMinutes);
        var summary = new StringBuilder();
        if (!canFilm) summary.Append("A phone or camera and a place are not both available, so every scene uses pictures you already have. ");
        else if (filmed == scenes.Count) summary.Append($"Film all {filmed} scene(s) at {location!.Name} with {equipment!.Name}. ");
        else if (filmed == 0) summary.Append("There is not enough time to film anything new, so every scene uses pictures you already have. ");
        else summary.Append($"Film {filmed} of {scenes.Count} scenes at {location!.Name} with {equipment!.Name}; the other {scenes.Count - filmed} use pictures you already have. ");
        if (canFilm && filmed > 0)
            summary.Append(people.Count switch { 0 => "No people are declared, so new footage is of the place and details. ", 1 => $"{people[0].Name} works alone, so new footage is detail and place rather than someone on camera. ", _ => $"{people[1].Name} films {people[0].Name}. " });
        summary.Append(budget is null ? $"About {total} min of filming — add your available time so the plan can fit it." : $"About {total} of your {budget} min, including set-up.");
        if (spare.Count > 0) summary.Append($" Unused pictures you could add as cutaways: {string.Join(", ", spare.Select(x => $"“{x.Name}”"))}.");
        return new ProductionPlanProposal(steps, summary.ToString().Trim(), SourceName, true);
    }
}

public static class ProductionPlanRules
{
    /// <summary>
    /// Checks a proposal against the real context and replaces on-screen seconds with each scene's actual audio length.
    /// Returns the safe steps, or an error explaining why the proposal was refused.
    /// </summary>
    public static (IReadOnlyList<PlanStepDraft>? Steps, string? Error) Validate(ProductionPlanContext context, IReadOnlyList<PlanStepDraft> steps)
    {
        var scenes = context.Scenes.ToDictionary(x => x.Id);
        if (steps.Count != scenes.Count || steps.Select(x => x.SceneId).Distinct().Count() != scenes.Count || steps.Any(x => !scenes.ContainsKey(x.SceneId)))
            return (null, "The plan must cover every current scene exactly once. It referenced a scene that does not exist or skipped one.");
        var available = context.Available.Select(x => x.Id).ToHashSet();
        if (steps.SelectMany(x => x.ResourceIds).Any(x => !available.Contains(x)))
            return (null, "The plan used something that is not declared as available.");
        var visuals = context.Visuals.Select(x => x.Id).ToHashSet();
        if (steps.Any(x => x.ExistingAssetId is { } id && !visuals.Contains(id)))
            return (null, "The plan referenced a picture that is not in this project.");
        if (steps.Any(x => x.Approach == PlanApproach.FilmNew && !x.ResourceIds.Any()))
            return (null, "New filming must say which declared resources it uses.");
        if (steps.Any(x => x.Approach == PlanApproach.UseExisting && x.ShootMinutes != 0))
            return (null, "Using existing material cannot need filming time.");
        if (steps.Any(x => x.ShootMinutes is < 0 or > ProductionPlan.MaxShootMinutesPerStep))
            return (null, "A step's filming time is out of range.");
        var total = steps.Sum(x => x.ShootMinutes);
        if (context.TimeBudgetMinutes is { } budget && total > budget)
            return (null, $"The plan needs {total} min of filming but you have {budget} min.");
        var ordered = context.Scenes.Select(s => steps.Single(x => x.SceneId == s.Id) with { SceneTitle = s.Title, OnScreenSeconds = s.Seconds }).ToList();
        return (ordered, null);
    }

    /// <summary>
    /// A computed note when a plan films fewer scenes than the declared kit, place and time allow, so the creator
    /// can see the trade-off. Returns null when the plan uses the capacity or filming is not possible.
    /// </summary>
    public static string? UnusedCapacityNote(FilmingCapacity capacity, IReadOnlyList<PlanStepDraft> steps)
    {
        if (!capacity.CanFilm || capacity.FitScenes.Count == 0) return null;
        var filmed = steps.Count(x => x.Approach == PlanApproach.FilmNew);
        if (filmed >= capacity.FitScenes.Count) return null;
        return filmed == 0
            ? $"This plan films nothing new, but your kit, place and time fit new footage for up to {capacity.FitScenes.Count} scene(s)."
            : $"This plan films {filmed} scene(s); your kit, place and time fit up to {capacity.FitScenes.Count}.";
    }

    /// <summary>A plain description of what changed since the last approved plan, computed from recorded snapshots (never from AI text).</summary>
    public static string ChangeNote(ProductionPlan? previous, string currentSnapshot, IReadOnlyList<PlanStepDraft> steps)
    {
        if (previous is null) return "First plan for this story.";
        static Dictionary<string, (string Name, bool Available)> Parse(string snapshot) => snapshot.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('|')).Where(p => p.Length >= 4).ToDictionary(p => p[0], p => (p[2], p[3] == "available"));
        var before = Parse(previous.ResourceSnapshot); var after = Parse(currentSnapshot);
        var parts = new List<string>();
        var lost = before.Where(x => x.Value.Available && (!after.TryGetValue(x.Key, out var a) || !a.Available)).Select(x => x.Value.Name).ToList();
        var gained = after.Where(x => x.Value.Available && (!before.TryGetValue(x.Key, out var b) || !b.Available)).Select(x => x.Value.Name).ToList();
        if (lost.Count > 0) parts.Add($"No longer available: {string.Join(", ", lost)}.");
        if (gained.Count > 0) parts.Add($"Now available: {string.Join(", ", gained)}.");
        var oldSteps = previous.Steps.ToDictionary(x => x.SceneTitle, StringComparer.OrdinalIgnoreCase);
        var switchedToExisting = steps.Count(x => x.Approach == PlanApproach.UseExisting && oldSteps.TryGetValue(x.SceneTitle, out var o) && o.Approach == PlanApproach.FilmNew);
        var switchedToFilm = steps.Count(x => x.Approach == PlanApproach.FilmNew && oldSteps.TryGetValue(x.SceneTitle, out var o) && o.Approach == PlanApproach.UseExisting);
        if (switchedToExisting > 0) parts.Add($"{switchedToExisting} scene(s) now use existing pictures instead of new filming.");
        if (switchedToFilm > 0) parts.Add($"{switchedToFilm} scene(s) now get new footage.");
        var minutes = steps.Sum(x => x.ShootMinutes);
        if (minutes != previous.PlannedShootMinutes) parts.Add($"Filming time {previous.PlannedShootMinutes} → {minutes} min.");
        return parts.Count == 0 ? $"Same resources as plan {previous.Version}." : $"Since plan {previous.Version}: " + string.Join(" ", parts);
    }
}
