using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;
using MyAfricaMyNarrative.Infrastructure.Media;
using MyAfricaMyNarrative.Web.Models;

namespace MyAfricaMyNarrative.Web.Controllers;

[Authorize]
public sealed class StudioController(IProjectRepository projects, ICurrentUser currentUser,
    IExportReadinessService readiness, IPrivateMediaStorage storage, IOptions<AiOptions> aiOptions, IStoryAssistant storyAssistant) : Controller
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
    public async Task<IActionResult> SaveLanguageReview(Guid id, LanguageReviewViewModel model, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var revision = Current(project); var approval = revision is null ? null : project.ReviewApprovals.FirstOrDefault(x => x.StoryRevisionId == revision.Id && x.SnapshotHash == revision.SnapshotHash);
        if (revision is null || approval is null) { TempData["Error"] = "Approve the current story before recording language review."; return RedirectToAction(nameof(Shape), new { id }); }
        if (!ModelState.IsValid) { TempData["Error"] = "Enter the real reviewer, language, fluency basis and confirm the attestation."; return RedirectToAction(nameof(Shape), new { id }); }
        if (project.LanguageReviews.Any(x => x.StoryRevisionId == revision.Id && x.SnapshotHash == revision.SnapshotHash))
        { TempData["Notice"] = "This exact revision already has a recorded language review."; return RedirectToAction(nameof(Shape), new { id }); }
        project.LanguageReviews.Add(new LanguageReview(id, revision.Id, revision.SnapshotHash, model.ReviewerName, model.Language, model.FluencyBasis, model.IsSelfReview, model.Notes, UserId, DateTimeOffset.UtcNow));
        await projects.SaveChangesAsync(ct); TempData["Notice"] = "Language review recorded for this exact story revision.";
        return RedirectToAction(nameof(Shape), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveIntent(Guid id, CreatorIntentViewModel model, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        if (!ModelState.IsValid) { TempData["Error"] = "Creator intention fields exceed their allowed length."; return RedirectToAction(nameof(Shape), new { id }); }
        var intent = project.CreatorIntents.SingleOrDefault();
        if (intent is null) project.CreatorIntents.Add(new CreatorIntent(id, model.Audience, model.Meaning, model.Tone, model.LocalContext, model.ProtectedTerms, DateTimeOffset.UtcNow));
        else intent.Update(model.Audience, model.Meaning, model.Tone, model.LocalContext, model.ProtectedTerms, DateTimeOffset.UtcNow);
        await projects.SaveChangesAsync(ct); TempData["Notice"] = "Creator intention saved."; return RedirectToAction(nameof(Shape), new { id, fragment = "intention" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateStoryFixture(Guid id, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var revision = Current(project); if (revision is null || revision.Scenes.Count < 2) { TempData["Error"] = "Add at least two scenes before requesting a comparison."; return RedirectToAction(nameof(Shape), new { id }); }
        Guid[] order; string summary; var fixture = aiOptions.Value.Mode != "Live";
        if (fixture)
        {
            order = revision.Scenes.OrderByDescending(x => x.Order).Select(x => x.Id).ToArray();
            summary = "Fixture suggestion: reverse the current scene order for comparison. All wording, source links, timing, captions and protected expressions remain unchanged.";
        }
        else
        {
            if (project.StudioProposals.Count(x => !x.IsFixture) >= aiOptions.Value.MaxCallsPerProject) { TempData["Error"] = "This project reached its bounded Live AI call limit."; return RedirectToAction(nameof(Shape), new { id }); }
            var intent = project.CreatorIntents.SingleOrDefault();
            try
            {
                var result = await storyAssistant.ProposeOrderAsync(new StoryAssistantRequest(
                    revision.Scenes.OrderBy(x => x.Order).Select(x => new StoryAssistantScene(x.Id, x.Order, x.Title)).ToList(),
                    intent?.Audience ?? "", intent?.Meaning ?? "", intent?.Tone ?? "", intent?.LocalContext ?? "", intent?.ProtectedTerms ?? ""), ct);
                order = result.SceneOrder.ToArray(); summary = $"Live {result.Provider} proposal ({result.Model}): {result.Summary}";
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException)
            { TempData["Error"] = exception.Message; return RedirectToAction(nameof(Shape), new { id }); }
        }
        var known = revision.Scenes.Select(x => x.Id).Order().ToArray();
        if (order.Length != known.Length || !order.Order().SequenceEqual(known)) { TempData["Error"] = "The assistant proposal changed or invented scene IDs and was rejected safely."; return RedirectToAction(nameof(Shape), new { id }); }
        project.StudioProposals.Add(new StudioProposal(id, ProposalKind.StoryOrder, revision.SnapshotHash, summary, JsonSerializer.Serialize(order), DateTimeOffset.UtcNow, fixture));
        await projects.SaveChangesAsync(ct); TempData["Notice"] = fixture ? "A clearly labelled fixture proposal is ready to compare." : "A bounded Live proposal is ready to compare."; return RedirectToAction(nameof(Shape), new { id, fragment = "proposals" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewProposal(Guid id, Guid proposalId, bool apply, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var proposal = project.StudioProposals.SingleOrDefault(x => x.Id == proposalId); if (proposal is null) return NotFound();
        if (proposal.Status != ProposalStatus.Pending) { TempData["Error"] = "That proposal was already reviewed."; return RedirectToAction(proposal.Kind == ProposalKind.StoryOrder ? nameof(Shape) : nameof(Make), new { id }); }
        if (apply && proposal.Kind == ProposalKind.StoryOrder)
        {
            var current = Current(project); if (current is null || current.SnapshotHash != proposal.BaselineHash) { TempData["Error"] = "The story changed. Generate a fresh proposal instead of applying a stale one."; return RedirectToAction(nameof(Shape), new { id }); }
            var ids = JsonSerializer.Deserialize<Guid[]>(proposal.PayloadJson) ?? []; var scenes = current.Scenes.ToDictionary(x => x.Id);
            if (ids.Length != scenes.Count || ids.Any(x => !scenes.ContainsKey(x))) { TempData["Error"] = "The fixture proposal references an invalid scene and was rejected safely."; return RedirectToAction(nameof(Shape), new { id }); }
            var drafts = ids.Select(sceneId => { var x = scenes[sceneId]; return new SceneDraft(x.Title, x.Text, x.TextOrigin, x.AudioAssetId, x.VisualAssetId, x.TranscriptRevisionId, x.StartMs, x.EndMs, x.Caption); });
            project.StoryRevisions.Add(new StoryRevision(id, NextRevision(project), drafts, DateTimeOffset.UtcNow));
        }
        if (apply && proposal.Kind == ProposalKind.ResourcePlan)
        {
            var ids = JsonSerializer.Deserialize<Guid[]>(proposal.PayloadJson) ?? [];
            if (ids.Any(resourceId => project.Resources.All(x => x.Id != resourceId || !x.IsAvailable))) { TempData["Error"] = "Resources changed. Generate a fresh plan instead of applying a stale one."; return RedirectToAction(nameof(Make), new { id }); }
        }
        proposal.Review(apply, DateTimeOffset.UtcNow); await projects.SaveChangesAsync(ct);
        TempData["Notice"] = apply ? "Proposal applied and recorded." : "Proposal rejected. Your current work was preserved.";
        return RedirectToAction(proposal.Kind == ProposalKind.StoryOrder ? nameof(Shape) : nameof(Make), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateResourceFixture(Guid id, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var available = project.Resources.Where(x => x.IsAvailable).OrderBy(x => x.Kind).ThenBy(x => x.Name).ToList();
        if (available.Count == 0) { TempData["Error"] = "Declare at least one available real resource before generating a plan."; return RedirectToAction(nameof(Make), new { id }); }
        var baseline = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', project.Resources.OrderBy(x => x.Id).Select(x => $"{x.Id}:{x.IsAvailable}"))))).ToLowerInvariant();
        var summary = $"Fixture plan using only currently available resources: {string.Join(", ", available.Select(x => x.Name))}. Existing uploaded visuals remain the only completed coverage; this proposal does not pretend new footage exists.";
        project.StudioProposals.Add(new StudioProposal(id, ProposalKind.ResourcePlan, baseline, summary, JsonSerializer.Serialize(available.Select(x => x.Id)), DateTimeOffset.UtcNow));
        await projects.SaveChangesAsync(ct); TempData["Notice"] = "A clearly labelled resource fixture is ready to compare."; return RedirectToAction(nameof(Make), new { id, fragment = "plan-proposals" });
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
        var existing = await projects.FindOwnedJobForUpdateAsync(id, UserId, kind, targetHash, ct);
        if (existing is not null)
        {
            if (existing.State is JobState.Queued or JobState.Running or JobState.Succeeded)
            { TempData["Notice"] = "That exact revision is already queued or rendered."; return RedirectToAction(nameof(Export), new { id }); }
            existing.Retry(DateTimeOffset.UtcNow); await projects.SaveChangesAsync(ct);
            TempData["Notice"] = "The failed export was queued again with a fresh bounded attempt set.";
            return RedirectToAction(nameof(Export), new { id });
        }
        var payload = JsonSerializer.Serialize(new { StoryRevisionId = check.Revision.Id, Final = final });
        await projects.AddJobAsync(new Job(id, UserId, kind, targetHash, payload, DateTimeOffset.UtcNow), ct); await projects.SaveChangesAsync(ct);
        TempData["Notice"] = "Export queued durably. Start the Worker to render it.";
        return RedirectToAction(nameof(Export), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelJob(Guid id, Guid jobId, CancellationToken ct)
    {
        var project = await projects.FindOwnedAsync(id, UserId, ct); if (project is null) return NotFound();
        var job = await projects.FindOwnedJobForUpdateAsync(jobId, UserId, ct);
        if (job is null || job.ProjectId != id) return NotFound();
        job.Cancel(DateTimeOffset.UtcNow); await projects.SaveChangesAsync(ct);
        TempData["Notice"] = "Export cancelled. An in-flight attempt cannot publish artifacts and its temporary files are cleaned.";
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

    [HttpGet]
    public async Task<IActionResult> PreservationArchive(Guid id, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var check = readiness.Check(project); var revision = check.Revision;
        if (revision is null || check.Approval?.SnapshotHash != revision.SnapshotHash) return Conflict("Approve the current story before creating an archive.");
        var artifacts = (await projects.ListOwnedArtifactsAsync(id, UserId, ct)).Where(x => x.StoryRevisionId == revision.Id && x.SnapshotHash == revision.SnapshotHash).ToList();
        var film = artifacts.FirstOrDefault(x => x.Kind is ArtifactKind.FinalVideo or ArtifactKind.PreviewVideo);
        if (film is null) return Conflict("Render the approved story before creating an archive.");
        var path = Path.Combine(Path.GetTempPath(), $"mamn-archive-{Guid.NewGuid():N}.zip");
        await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.Asynchronous))
            await PreservationArchiveBuilder.BuildAsync(output, project, revision, check.Approval, artifacts, storage, ct);
        return File(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose), "application/zip", $"{project.Title}-preservation.zip");
    }

    private static StoryRevision? Current(Project p) => p.StoryRevisions.OrderByDescending(x => x.RevisionNumber).FirstOrDefault();
    private static int NextRevision(Project p) => (p.StoryRevisions.Count == 0 ? 0 : p.StoryRevisions.Max(x => x.RevisionNumber)) + 1;
    private static List<SceneDraft> CurrentDrafts(Project p) => Current(p)?.Scenes.OrderBy(x => x.Order)
        .Select(x => new SceneDraft(x.Title, x.Text, x.TextOrigin, x.AudioAssetId, x.VisualAssetId, x.TranscriptRevisionId, x.StartMs, x.EndMs, x.Caption)).ToList() ?? [];
}
