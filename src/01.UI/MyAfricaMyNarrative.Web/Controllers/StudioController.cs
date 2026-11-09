using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;
using MyAfricaMyNarrative.Web.Models;

namespace MyAfricaMyNarrative.Web.Controllers;

[Authorize]
public sealed class StudioController(IProjectRepository projects, ICurrentUser currentUser,
    IExportReadinessService readiness, IPrivateMediaStorage storage, IOptions<AiOptions> aiOptions) : Controller
{
    private Guid UserId => currentUser.UserId ?? throw new InvalidOperationException("An authenticated user ID is required.");

    [HttpGet]
    public Task<IActionResult> Shape(Guid id, CancellationToken ct) => StudioView(id, "Shape", ct);
    [HttpGet]
    public Task<IActionResult> Make(Guid id, CancellationToken ct) => StudioView(id, "Make", ct);
    [HttpGet]
    public Task<IActionResult> Export(Guid id, CancellationToken ct) => StudioView(id, "Export", ct);

    private async Task<IActionResult> StudioView(Guid id, string view, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct);
        if (project is null) return NotFound();
        var current = project.StoryRevisions.OrderByDescending(x => x.RevisionNumber).FirstOrDefault();
        return View(view, new StudioViewModel(project, current, readiness.Check(project),
            await projects.ListOwnedJobsAsync(id, UserId, ct), await projects.ListOwnedArtifactsAsync(id, UserId, ct), aiOptions.Value.Mode));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTranscript(Guid id, TranscriptEditViewModel model, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct);
        if (project is null) return NotFound();
        var asset = project.MediaAssets.SingleOrDefault(x => x.Id == model.AssetId && x.PermissionStatus == PermissionStatus.Granted && x.Kind == MediaKind.Audio);
        if (!ModelState.IsValid || asset is null || model.EndMs <= model.StartMs)
        { TempData["Error"] = "Choose permitted audio, reviewed text, language and a valid playback range."; return RedirectToAction(nameof(Shape), new { id }); }
        project.TranscriptRevisions.Add(new TranscriptRevision(id, asset.Id, model.Text, model.Language, model.StartMs, model.EndMs, UserId, DateTimeOffset.UtcNow));
        await projects.SaveChangesAsync(ct);
        TempData["Notice"] = "Reviewed transcript revision saved. The original recording was not changed.";
        return RedirectToAction(nameof(Shape), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddScene(Guid id, SceneEditViewModel model, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct);
        if (project is null) return NotFound();
        var audio = project.MediaAssets.SingleOrDefault(x => x.Id == model.AudioAssetId && x.Kind == MediaKind.Audio && x.PermissionStatus == PermissionStatus.Granted);
        var visual = project.MediaAssets.SingleOrDefault(x => x.Id == model.VisualAssetId && x.Kind != MediaKind.Audio && x.PermissionStatus == PermissionStatus.Granted);
        var transcript = model.TranscriptRevisionId is null ? null : project.TranscriptRevisions.SingleOrDefault(x => x.Id == model.TranscriptRevisionId && x.AssetId == model.AudioAssetId);
        if (!ModelState.IsValid || audio is null || visual is null || model.EndMs <= model.StartMs || (model.TranscriptRevisionId is not null && transcript is null))
        { TempData["Error"] = "Each scene needs permitted audio, a real visual and a valid reviewed source range."; return RedirectToAction(nameof(Shape), new { id }); }
        var drafts = CurrentDrafts(project);
        drafts.Add(new SceneDraft(model.Title, model.Text, model.TextOrigin, audio.Id, visual.Id, transcript?.Id, model.StartMs, model.EndMs, model.Caption));
        project.StoryRevisions.Add(new StoryRevision(id, NextRevision(project), drafts, DateTimeOffset.UtcNow));
        await projects.SaveChangesAsync(ct);
        TempData["Notice"] = "A new immutable story revision was saved.";
        return RedirectToAction(nameof(Shape), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveScene(Guid id, Guid sceneId, int direction, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var drafts = CurrentDrafts(project); var current = Current(project); var index = current?.Scenes.OrderBy(x => x.Order).ToList().FindIndex(x => x.Id == sceneId) ?? -1;
        var target = index + Math.Sign(direction);
        if (index >= 0 && target >= 0 && target < drafts.Count) { (drafts[index], drafts[target]) = (drafts[target], drafts[index]); project.StoryRevisions.Add(new StoryRevision(id, NextRevision(project), drafts, DateTimeOffset.UtcNow)); await projects.SaveChangesAsync(ct); }
        return RedirectToAction(nameof(Shape), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var revision = Current(project); if (revision is null || revision.Scenes.Count == 0) { TempData["Error"] = "Add a scene before approval."; return RedirectToAction(nameof(Shape), new { id }); }
        if (project.ReviewApprovals.All(x => x.StoryRevisionId != revision.Id || x.SnapshotHash != revision.SnapshotHash))
            project.ReviewApprovals.Add(new ReviewApproval(id, revision.Id, revision.SnapshotHash, UserId, DateTimeOffset.UtcNow));
        await projects.SaveChangesAsync(ct); TempData["Notice"] = $"Story revision {revision.RevisionNumber} approved.";
        return RedirectToAction(nameof(Shape), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddResource(Guid id, ResourceEditViewModel model, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        if (!ModelState.IsValid) { TempData["Error"] = "Enter a resource name and availability."; return RedirectToAction(nameof(Make), new { id }); }
        project.Resources.Add(new ResourceItem(id, model.Kind, model.Name, model.Availability, DateTimeOffset.UtcNow)); await projects.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Make), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleResource(Guid id, Guid resourceId, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var resource = project.Resources.SingleOrDefault(x => x.Id == resourceId); if (resource is null) return NotFound();
        resource.SetAvailable(!resource.IsAvailable); await projects.SaveChangesAsync(ct); return RedirectToAction(nameof(Make), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> QueueExport(Guid id, bool final, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var check = readiness.Check(project);
        if ((final && !check.CanExportFinal) || (!final && !check.CanPreview) || check.Revision is null)
        { TempData["Error"] = "Export is not ready. Complete the checklist first."; return RedirectToAction(nameof(Export), new { id }); }
        var kind = final ? "RenderFinal" : "RenderPreview";
        var targetHash = check.Revision.SnapshotHash;
        if ((await projects.ListOwnedJobsAsync(id, UserId, ct)).Any(x => x.Kind == kind && x.TargetHash == targetHash && x.State is JobState.Queued or JobState.Running or JobState.Succeeded))
        { TempData["Notice"] = "That exact revision is already queued or rendered."; return RedirectToAction(nameof(Export), new { id }); }
        var payload = JsonSerializer.Serialize(new { StoryRevisionId = check.Revision.Id, Final = final });
        await projects.AddJobAsync(new Job(id, UserId, kind, targetHash, payload, DateTimeOffset.UtcNow), ct); await projects.SaveChangesAsync(ct);
        TempData["Notice"] = "Export queued durably. Start the Worker to render it.";
        return RedirectToAction(nameof(Export), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var artifact = await projects.FindOwnedArtifactAsync(id, UserId, ct); if (artifact is null) return NotFound();
        var project = await projects.FindOwnedStudioAsync(artifact.ProjectId, UserId, ct); if (project is null) return NotFound();
        var check = readiness.Check(project);
        if (check.Revision?.Id != artifact.StoryRevisionId || check.Approval?.SnapshotHash != artifact.SnapshotHash) return Conflict("This output is stale because the approved story changed.");
        return File(await storage.OpenReadAsync(artifact.StorageKey, ct), artifact.Kind == ArtifactKind.Captions ? "application/x-subrip" : "video/mp4", enableRangeProcessing: true);
    }

    private static StoryRevision? Current(Project p) => p.StoryRevisions.OrderByDescending(x => x.RevisionNumber).FirstOrDefault();
    private static int NextRevision(Project p) => (p.StoryRevisions.Count == 0 ? 0 : p.StoryRevisions.Max(x => x.RevisionNumber)) + 1;
    private static List<SceneDraft> CurrentDrafts(Project p) => Current(p)?.Scenes.OrderBy(x => x.Order)
        .Select(x => new SceneDraft(x.Title, x.Text, x.TextOrigin, x.AudioAssetId, x.VisualAssetId, x.TranscriptRevisionId, x.StartMs, x.EndMs, x.Caption)).ToList() ?? [];
}
