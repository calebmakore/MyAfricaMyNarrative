using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;
using MyAfricaMyNarrative.Web.Models;

namespace MyAfricaMyNarrative.Web.Controllers;

/// <summary>Credits (Export), production plans and the scene-linked shot list (Make). Every action is owner-scoped.</summary>
[Authorize]
public sealed class ProductionController(IProjectRepository projects, ICurrentUser currentUser, IProductionPlanAssistant planner, Microsoft.Extensions.Options.IOptions<AiOptions> aiOptions) : Controller
{
    private Guid UserId => currentUser.UserId ?? throw new InvalidOperationException("An authenticated user ID is required.");

    private IActionResult ToExport(Guid id) => RedirectToAction("Export", "Studio", new { id }, "credits");
    private IActionResult ToMake(Guid id) => RedirectToAction("Make", "Studio", new { id }, "shot-list");

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCredit(Guid id, CreditEditViewModel model, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        if (!ModelState.IsValid) { TempData["Error"] = "Enter a role (up to 80 characters) and a name (up to 120 characters)."; return ToExport(id); }
        if (project.Credits.Count >= CreditsRules.MaxCredits) { TempData["Error"] = $"A film can list at most {CreditsRules.MaxCredits} credits."; return ToExport(id); }
        if (!ValidSource(project, model.SourceAssetId)) { TempData["Error"] = "Link the credit to one of this project's sources, or leave it unlinked."; return ToExport(id); }
        var order = project.Credits.Count == 0 ? 1 : project.Credits.Max(x => x.Order) + 1;
        project.Credits.Add(new CreditEntry(id, model.Role, model.Name, model.SourceAssetId, order, DateTimeOffset.UtcNow));
        await projects.SaveChangesAsync(ct);
        TempData["Notice"] = "Credit added. Approve the credits again before the final export.";
        return ToExport(id);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCredit(Guid id, Guid creditId, CreditEditViewModel model, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var credit = project.Credits.SingleOrDefault(x => x.Id == creditId); if (credit is null) return NotFound();
        if (!ModelState.IsValid || !ValidSource(project, model.SourceAssetId)) { TempData["Error"] = "Enter a valid role, name, order and optional source."; return ToExport(id); }
        credit.Update(model.Role, model.Name, model.SourceAssetId, model.Order);
        await projects.SaveChangesAsync(ct);
        TempData["Notice"] = "Credit updated. Approve the credits again before the final export.";
        return ToExport(id);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCredit(Guid id, Guid creditId, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var credit = project.Credits.SingleOrDefault(x => x.Id == creditId); if (credit is null) return NotFound();
        project.Credits.Remove(credit);
        await projects.SaveChangesAsync(ct);
        TempData["Notice"] = "Credit removed.";
        return ToExport(id);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveCredits(Guid id, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        if (project.Credits.Count == 0) { TempData["Error"] = "Add at least one credit before approving."; return ToExport(id); }
        var hash = CreditsRules.ComputeHash(project.Credits);
        if (project.CreditsApprovals.All(x => x.CreditsHash != hash))
            project.CreditsApprovals.Add(new CreditsApproval(id, hash, UserId, DateTimeOffset.UtcNow));
        await projects.SaveChangesAsync(ct);
        TempData["Notice"] = "These exact credits are approved for export.";
        return ToExport(id);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddShot(Guid id, ShotEditViewModel model, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        if (!ModelState.IsValid) { TempData["Error"] = "Choose a scene and describe the shot in 3–500 characters."; return ToMake(id); }
        var current = project.StoryRevisions.OrderByDescending(x => x.RevisionNumber).FirstOrDefault();
        if (current is null || current.Scenes.All(x => !string.Equals(x.Title, model.SceneTitle.Trim(), StringComparison.OrdinalIgnoreCase)))
        { TempData["Error"] = "Shots must belong to a scene in the current story."; return ToMake(id); }
        var declared = project.Resources.Where(x => x.IsAvailable).Select(x => x.Id).ToHashSet();
        if (model.ResourceIds.Any(x => !declared.Contains(x)))
        { TempData["Error"] = "A shot can only use resources you declared as available."; return ToMake(id); }
        project.Shots.Add(new ShotPlanItem(id, model.SceneTitle, model.Description, model.ResourceIds, false, DateTimeOffset.UtcNow));
        await projects.SaveChangesAsync(ct);
        TempData["Notice"] = "Shot added to the plan. It stays missing until you capture and upload it, or drop it.";
        return ToMake(id);
    }

    private IActionResult ToPlan(Guid id) => RedirectToAction("Make", "Studio", new { id }, "plan");

    /// <summary>
    /// "Make it with what I have": builds a versioned plan from the current story and only the resources marked available.
    /// Live uses the configured model; the practice planner is a labelled, deterministic alternative. A Live failure never falls back silently.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GeneratePlan(Guid id, bool live, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var context = ProductionPlanContext.From(project);
        if (context is null) { TempData["Error"] = "Shape the story into scenes before planning production."; return ToPlan(id); }
        if (context.Available.Count == 0 && context.Visuals.Count == 0) { TempData["Error"] = "Declare what you have, or upload a picture, before planning."; return ToPlan(id); }
        if (project.ProductionPlans.Any(x => x.Status == PlanStatus.Proposed && x.StoryHash == context.Revision.SnapshotHash && x.ResourceHash == context.ResourceHash))
        { TempData["Error"] = "A plan for exactly these resources is already waiting for your review."; return ToPlan(id); }

        ProductionPlanProposal proposal;
        if (live)
        {
            if (aiOptions.Value.Mode != "Live") { TempData["Error"] = "Live AI is not configured. Use the practice planner, or set Ai:Mode, Ai:ApiKey and Ai:StoryModel in User Secrets."; return ToPlan(id); }
            if (LiveCalls(project) >= aiOptions.Value.MaxCallsPerProject) { TempData["Error"] = "This project reached its bounded Live AI call limit."; return ToPlan(id); }
            try { proposal = await planner.ProposeAsync(context, ct); }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException or System.Text.Json.JsonException)
            {
                // Recorded so the bounded call count includes failed calls too.
                var failed = new StudioProposal(id, ProposalKind.ResourcePlan, context.ResourceHash, "Live plan request failed: " + exception.Message, "[]", DateTimeOffset.UtcNow, false);
                failed.Review(false, DateTimeOffset.UtcNow);
                project.StudioProposals.Add(failed);
                await projects.SaveChangesAsync(ct);
                TempData["Error"] = AiServiceException.Describe(exception) + " Nothing was changed."; return ToPlan(id);
            }
        }
        else proposal = PracticePlanner.Plan(context);

        var (steps, error) = ProductionPlanRules.Validate(context, proposal.Steps);
        if (steps is null) { TempData["Error"] = (proposal.IsFixture ? "" : "The AI plan was refused safely: ") + error; return ToPlan(id); }
        var previous = project.ProductionPlans.Where(x => x.Status == PlanStatus.Approved).OrderByDescending(x => x.Version).FirstOrDefault();
        foreach (var stale in project.ProductionPlans.Where(x => x.Status == PlanStatus.Proposed)) stale.Supersede(DateTimeOffset.UtcNow);
        var version = project.ProductionPlans.Count == 0 ? 1 : project.ProductionPlans.Max(x => x.Version) + 1;
        try
        {
            project.ProductionPlans.Add(new ProductionPlan(id, version, context.Revision.Id, context.Revision.SnapshotHash, context.ResourceHash, context.ResourceSnapshot,
                context.TimeBudgetMinutes, proposal.Summary, PlanNote(ProductionPlanRules.ChangeNote(previous, context.ResourceSnapshot, steps), ProductionPlanRules.UnusedCapacityNote(PracticePlanner.Capacity(context), steps)), proposal.Source, proposal.IsFixture, steps, DateTimeOffset.UtcNow));
        }
        catch (ArgumentException exception) { TempData["Error"] = "The plan was refused safely: " + exception.Message; return ToPlan(id); }
        await projects.SaveChangesAsync(ct);
        TempData["Notice"] = previous is null ? $"Plan {version} is ready. Review it scene by scene, then approve or reject it." : $"Plan {version} is ready beside plan {previous.Version}. Compare them, then approve or keep the old one.";
        return ToPlan(id);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewPlan(Guid id, Guid planId, bool approve, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var plan = project.ProductionPlans.SingleOrDefault(x => x.Id == planId); if (plan is null) return NotFound();
        if (plan.Status != PlanStatus.Proposed) { TempData["Error"] = "That plan was already reviewed."; return ToPlan(id); }
        var now = DateTimeOffset.UtcNow;
        if (!approve) { plan.Reject(now); await projects.SaveChangesAsync(ct); TempData["Notice"] = "Plan rejected. Your approved plan and shot list are unchanged."; return ToPlan(id); }
        var current = project.StoryRevisions.OrderByDescending(x => x.RevisionNumber).FirstOrDefault();
        if (current is null || current.SnapshotHash != plan.StoryHash || ProductionPlanContext.HashResources(project.Resources) != plan.ResourceHash)
        { TempData["Error"] = "Your story or resources changed since this plan was made. Make a fresh plan instead of approving a stale one."; return ToPlan(id); }

        foreach (var old in project.ProductionPlans.Where(x => x.Status == PlanStatus.Approved)) old.Supersede(now);
        plan.Approve(now);
        // Earlier plan-made shots that were never filmed give way to the new plan; captured and hand-written shots are kept.
        foreach (var shot in project.Shots.Where(x => x.IsSuggestion && x.Status == ShotStatus.Planned)) shot.Drop(now);
        foreach (var step in plan.Steps.Where(x => x.Approach == PlanApproach.FilmNew).OrderBy(x => x.Order))
        {
            var description = step.Instruction.Length > 500 ? step.Instruction[..500] : step.Instruction;
            project.Shots.Add(new ShotPlanItem(id, step.SceneTitle, description, step.ResourceIds, true, now));
        }
        await projects.SaveChangesAsync(ct);
        var filming = plan.Steps.Count(x => x.Approach == PlanApproach.FilmNew);
        TempData["Notice"] = $"Plan {plan.Version} approved. {filming} shot(s) are on your shot list" + (filming < plan.Steps.Count ? "; the other scenes use pictures you already have." : ".");
        return ToPlan(id);
    }

    internal static int LiveCalls(Project project) => project.TranscriptionProposals.Count + project.StudioProposals.Count(x => !x.IsFixture) + project.ProductionPlans.Count(x => !x.IsFixture);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CaptureShot(Guid id, Guid shotId, Guid assetId, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var shot = project.Shots.SingleOrDefault(x => x.Id == shotId); if (shot is null) return NotFound();
        var asset = project.MediaAssets.SingleOrDefault(x => x.Id == assetId && x.Kind != MediaKind.Audio && x.PermissionStatus == PermissionStatus.Granted);
        if (asset is null) { TempData["Error"] = "Choose an uploaded, permitted image or video for this shot. Upload new footage in Record first."; return ToMake(id); }
        shot.MarkCaptured(asset.Id, DateTimeOffset.UtcNow);
        await projects.SaveChangesAsync(ct);
        TempData["Notice"] = $"Shot marked captured with “{asset.DisplayName}”. Use it as the scene visual in Shape if it should appear in the film.";
        return ToMake(id);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetShotStatus(Guid id, Guid shotId, bool drop, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var shot = project.Shots.SingleOrDefault(x => x.Id == shotId); if (shot is null) return NotFound();
        if (drop) shot.Drop(DateTimeOffset.UtcNow); else shot.Reopen(DateTimeOffset.UtcNow);
        await projects.SaveChangesAsync(ct);
        TempData["Notice"] = drop ? "Shot dropped. The film will use the visuals you already have." : "Shot reopened as planned footage.";
        return ToMake(id);
    }

    private static bool ValidSource(Project project, Guid? assetId) => assetId is null || assetId == Guid.Empty || project.MediaAssets.Any(x => x.Id == assetId);

    private static string PlanNote(string change, string? unused) => unused is null ? change : $"{change} {unused}";
}
