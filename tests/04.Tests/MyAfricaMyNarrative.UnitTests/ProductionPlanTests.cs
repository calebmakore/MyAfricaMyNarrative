using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;

namespace MyAfricaMyNarrative.UnitTests;

/// <summary>"Make it with what I have": plans use only declared, available resources and respond honestly to changes.</summary>
public sealed class ProductionPlanTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private sealed record Setup(Project Project, ResourceItem Phone, ResourceItem Place, ResourceItem Narrator, ResourceItem Helper, ResourceItem Time, MediaAsset[] Photos);

    /// <summary>The brainstorm scenario: one phone, one place, two people, two hours and five photographs.</summary>
    private static Setup Scenario(int scenes = 4, int minutes = 120)
    {
        var owner = Guid.NewGuid(); var project = new Project(owner, "Grandmother's tailoring", Now);
        var audio = new MediaAsset(project.Id, "a", "voice.weba", "Voice note", new string('a', 64), "audio/webm", MediaKind.Audio, 10, "", Now, 120_000);
        project.MediaAssets.Add(audio);
        var photos = Enumerable.Range(1, 5).Select(i => new MediaAsset(project.Id, $"p{i}", $"p{i}.jpg", $"Family photo {i}", new string((char)('a' + i), 64), "image/jpeg", MediaKind.Image, 10, "", Now.AddSeconds(i), width: 10, height: 10)).ToArray();
        foreach (var photo in photos) project.MediaAssets.Add(photo);
        var drafts = Enumerable.Range(0, scenes).Select(i => new SceneDraft($"Scene {i + 1}", $"Words {i + 1}", TextOrigin.CreatorWritten, audio.Id, photos[i % photos.Length].Id, null, i * 18_000, i * 18_000 + 18_000, "")).ToList();
        project.StoryRevisions.Add(new StoryRevision(project.Id, 1, drafts, Now));
        var phone = new ResourceItem(project.Id, ResourceKind.Equipment, "My phone", "", Now);
        var place = new ResourceItem(project.Id, ResourceKind.Location, "The workshop", "", Now);
        var narrator = new ResourceItem(project.Id, ResourceKind.Person, "Rudo", "", Now);
        var helper = new ResourceItem(project.Id, ResourceKind.Person, "Tendai", "", Now.AddSeconds(1));
        var time = new ResourceItem(project.Id, ResourceKind.Time, "Saturday", "", Now, minutes);
        foreach (var r in new[] { phone, place, narrator, helper, time }) project.Resources.Add(r);
        return new(project, phone, place, narrator, helper, time, photos);
    }

    private static ProductionPlan Approved(Project project, ProductionPlanContext context, IReadOnlyList<PlanStepDraft> steps, int version = 1)
    {
        var plan = new ProductionPlan(project.Id, version, context.Revision.Id, context.Revision.SnapshotHash, context.ResourceHash, context.ResourceSnapshot,
            context.TimeBudgetMinutes, "Plan", "First plan for this story.", PracticePlanner.SourceName, true, steps, Now);
        plan.Approve(Now); project.ProductionPlans.Add(plan); return plan;
    }

    [Fact]
    public void Practice_plan_covers_every_scene_with_real_lengths_and_fits_the_time_budget()
    {
        var s = Scenario();
        var context = ProductionPlanContext.From(s.Project)!;
        var proposal = PracticePlanner.Plan(context);
        var (steps, error) = ProductionPlanRules.Validate(context, proposal.Steps);
        Assert.Null(error);
        Assert.Equal(4, steps!.Count);
        Assert.All(steps, x => Assert.Equal(18, x.OnScreenSeconds));
        Assert.True(steps.Sum(x => x.ShootMinutes) <= 120);
        var filmed = steps.Where(x => x.Approach == PlanApproach.FilmNew).ToList();
        Assert.NotEmpty(filmed);
        Assert.All(filmed, x => Assert.Contains(s.Helper.Id, x.ResourceIds));
        Assert.Contains("Tendai films", filmed[0].Instruction); Assert.Contains("set up", filmed[0].Instruction); Assert.Equal(PracticePlanner.PersonShotMinutes + PracticePlanner.SetupMinutesPerLocation, filmed[0].ShootMinutes);
        Assert.All(steps, x => Assert.False(string.IsNullOrWhiteSpace(x.Purpose)));
    }

    [Fact]
    public void When_the_second_person_drops_out_the_new_plan_never_uses_them_and_says_what_changed()
    {
        var s = Scenario();
        var first = ProductionPlanContext.From(s.Project)!;
        var (firstSteps, _) = ProductionPlanRules.Validate(first, PracticePlanner.Plan(first).Steps);
        var approved = Approved(s.Project, first, firstSteps!);

        s.Helper.SetAvailable(false);
        var second = ProductionPlanContext.From(s.Project)!;
        Assert.NotEqual(first.ResourceHash, second.ResourceHash);
        var proposal = PracticePlanner.Plan(second);
        var (steps, error) = ProductionPlanRules.Validate(second, proposal.Steps);
        Assert.Null(error);
        Assert.DoesNotContain(steps!.SelectMany(x => x.ResourceIds), id => id == s.Helper.Id);
        Assert.Contains(steps!, x => x.Approach == PlanApproach.FilmNew && x.Instruction.Contains("close-up", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Rudo works alone", proposal.Summary);
        var note = ProductionPlanRules.ChangeNote(approved, second.ResourceSnapshot, steps!);
        Assert.Contains("No longer available: Tendai", note);
        Assert.Contains("Since plan 1", note);
    }

    [Fact]
    public void Tight_time_switches_lower_priority_scenes_to_existing_photos_and_keeps_opening_and_ending()
    {
        var s = Scenario(scenes: 4, minutes: 50); // 10 setup + two 20-minute shots
        var context = ProductionPlanContext.From(s.Project)!;
        var steps = PracticePlanner.Plan(context).Steps;
        Assert.Equal(PlanApproach.FilmNew, steps[0].Approach);
        Assert.Equal(PlanApproach.FilmNew, steps[^1].Approach);
        Assert.All(steps.Skip(1).Take(2), x => { Assert.Equal(PlanApproach.UseExisting, x.Approach); Assert.Equal(0, x.ShootMinutes); Assert.NotNull(x.ExistingAssetId); });
    }

    [Fact]
    public void Without_a_phone_and_place_every_scene_uses_existing_material()
    {
        var s = Scenario();
        s.Phone.SetAvailable(false);
        var steps = PracticePlanner.Plan(ProductionPlanContext.From(s.Project)!).Steps;
        Assert.All(steps, x => Assert.Equal(PlanApproach.UseExisting, x.Approach));
    }

    [Fact]
    public void Validation_refuses_unavailable_people_unknown_pictures_missing_scenes_and_over_budget_plans()
    {
        var s = Scenario();
        s.Helper.SetAvailable(false);
        var context = ProductionPlanContext.From(s.Project)!;
        var good = PracticePlanner.Plan(context).Steps.ToList();

        var withHelper = good.Select((x, i) => i == 0 ? x with { Approach = PlanApproach.FilmNew, ShootMinutes = 10, ResourceIds = [s.Helper.Id, s.Phone.Id] } : x).ToList();
        Assert.Contains("not declared as available", ProductionPlanRules.Validate(context, withHelper).Error);

        var unknownPicture = good.Select((x, i) => i == 0 ? x with { Approach = PlanApproach.UseExisting, ShootMinutes = 0, ResourceIds = [], ExistingAssetId = Guid.NewGuid() } : x).ToList();
        Assert.Contains("picture", ProductionPlanRules.Validate(context, unknownPicture).Error);

        Assert.Contains("every current scene", ProductionPlanRules.Validate(context, good.Skip(1).ToList()).Error);
        var invented = good.Select((x, i) => i == 0 ? x with { SceneId = Guid.NewGuid() } : x).ToList();
        Assert.Contains("every current scene", ProductionPlanRules.Validate(context, invented).Error);

        var overBudget = good.Select(x => x with { Approach = PlanApproach.FilmNew, ShootMinutes = 60, ResourceIds = [s.Phone.Id] }).ToList();
        Assert.Contains("you have 120 min", ProductionPlanRules.Validate(context, overBudget).Error);
    }

    [Fact]
    public void Validation_replaces_claimed_lengths_with_the_real_scene_lengths()
    {
        var s = Scenario();
        var context = ProductionPlanContext.From(s.Project)!;
        var claimed = PracticePlanner.Plan(context).Steps.Select(x => x with { OnScreenSeconds = 99, SceneTitle = "renamed" }).ToList();
        var (steps, _) = ProductionPlanRules.Validate(context, claimed);
        Assert.All(steps!, x => Assert.Equal(18, x.OnScreenSeconds));
        Assert.Equal("Scene 1", steps![0].SceneTitle);
    }

    [Fact]
    public void Plan_review_is_one_way_and_existing_material_needs_no_filming_time()
    {
        var s = Scenario();
        var context = ProductionPlanContext.From(s.Project)!;
        var plan = Approved(s.Project, context, PracticePlanner.Plan(context).Steps);
        Assert.Throws<InvalidOperationException>(() => plan.Approve(Now));
        Assert.Throws<InvalidOperationException>(() => plan.Reject(Now));
        var scene = context.Scenes[0];
        Assert.Throws<ArgumentException>(() => new ProductionPlan(s.Project.Id, 2, context.Revision.Id, context.Revision.SnapshotHash, context.ResourceHash, "", null, "x", "", "x", true,
            [new PlanStepDraft(scene.Id, scene.Title, PlanApproach.UseExisting, "Use photo", 18, 30, "Because", [], s.Photos[0].Id)], Now));
    }

    [Fact]
    public void Only_time_resources_carry_minutes()
    {
        var project = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new ResourceItem(project, ResourceKind.Person, "Tendai", "", Now, 60));
        Assert.Throws<ArgumentException>(() => new ResourceItem(project, ResourceKind.Time, "Saturday", "", Now, 1));
        Assert.Equal(120, new ResourceItem(project, ResourceKind.Time, "Saturday", "", Now, 120).Minutes);
    }

    [Fact]
    public void Capacity_films_opening_and_ending_first_and_matches_the_practice_plan()
    {
        var s = Scenario(scenes: 5, minutes: 60);
        var context = ProductionPlanContext.From(s.Project)!;
        var capacity = PracticePlanner.Capacity(context);
        Assert.True(capacity.CanFilm);
        Assert.Equal(PracticePlanner.PersonShotMinutes, capacity.MinutesPerShot);
        // 60 min - 10 set-up = 50: two 20-minute shots fit, opening then ending.
        var scenes = context.Scenes;
        Assert.Equal([scenes[0].Id, scenes[^1].Id], capacity.FitScenes);
        var filmed = PracticePlanner.Plan(context).Steps.Where(x => x.Approach == PlanApproach.FilmNew).Select(x => x.SceneId).ToHashSet();
        Assert.True(filmed.SetEquals(capacity.FitScenes));
        Assert.Null(ProductionPlanRules.UnusedCapacityNote(capacity, PracticePlanner.Plan(context).Steps));
    }

    [Fact]
    public void A_plan_that_films_less_than_fits_gets_a_computed_note_and_no_kit_means_no_note()
    {
        var s = Scenario();
        var context = ProductionPlanContext.From(s.Project)!;
        var allExisting = context.Scenes.Select(x => new PlanStepDraft(x.Id, x.Title, PlanApproach.UseExisting, "Use the photo", x.Seconds, 0, "Costs no time", [], x.VisualAssetId)).ToList();
        var capacity = PracticePlanner.Capacity(context);
        Assert.Equal("This plan films nothing new, but your kit, place and time fit new footage for up to 4 scene(s).", ProductionPlanRules.UnusedCapacityNote(capacity, allExisting));

        s.Project.Resources.Remove(s.Phone);
        var noKit = PracticePlanner.Capacity(ProductionPlanContext.From(s.Project)!);
        Assert.False(noKit.CanFilm); Assert.Empty(noKit.FitScenes);
        Assert.Null(ProductionPlanRules.UnusedCapacityNote(noKit, allExisting));
    }
}
