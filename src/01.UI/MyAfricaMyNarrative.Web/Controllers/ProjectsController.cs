using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;
using MyAfricaMyNarrative.Web.Models;

namespace MyAfricaMyNarrative.Web.Controllers;

[Authorize]
public sealed class ProjectsController(IProjectRepository projects, ICurrentUser currentUser, IPrivateMediaStorage storage, IExportReadinessService readiness) : Controller
{
    private Guid UserId => currentUser.UserId ?? throw new InvalidOperationException("An authenticated user ID is required.");
    public async Task<IActionResult> Index(CancellationToken cancellationToken) => View(await projects.ListAsync(UserId, cancellationToken));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProjectEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View("Index", await projects.ListAsync(UserId, cancellationToken));
        var project = new Project(UserId, model.Title, DateTimeOffset.UtcNow);
        if (Enum.IsDefined(model.StoryType)) project.SetStoryType(model.StoryType, DateTimeOffset.UtcNow);
        project.SetIdea(model.Idea, DateTimeOffset.UtcNow);
        await projects.AddAsync(project, cancellationToken);
        await projects.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Details), new { id = project.Id });
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, cancellationToken);
        return project is null ? NotFound() : View(await RecordModel(project, cancellationToken));
    }

    private async Task<RecordViewModel> RecordModel(Project project, CancellationToken cancellationToken)
    {
        var artifacts = await projects.ListOwnedArtifactsAsync(project.Id, UserId, cancellationToken);
        return new RecordViewModel(project, StudioProgressCalculator.Calculate(project, readiness.Check(project), artifacts));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBasics(Guid id, StoryBasicsViewModel model, string? returnTo, CancellationToken cancellationToken)
    {
        var project = await projects.FindOwnedAsync(id, UserId, cancellationToken); if (project is null) return NotFound();
        if (!ModelState.IsValid || !Enum.IsDefined(model.StoryType)) { TempData["Error"] = "Choose a story type and keep the idea under 2000 characters."; }
        else { project.SetStoryType(model.StoryType, DateTimeOffset.UtcNow); project.SetIdea(model.Idea, DateTimeOffset.UtcNow); await projects.SaveChangesAsync(cancellationToken); TempData["Notice"] = "Story basics saved."; }
        return returnTo == "shape" ? RedirectToAction("Shape", "Studio", new { id }, "intention") : RedirectToAction(nameof(Details), null, new { id }, "basics");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Rename(Guid id, ProjectEditViewModel model, CancellationToken cancellationToken)
    {
        var project = await projects.FindOwnedAsync(id, UserId, cancellationToken);
        if (project is null) return NotFound();
        if (!ModelState.IsValid) { TempData["Error"] = "Enter a title between 1 and 120 characters."; return RedirectToAction(nameof(Details), new { id }); }
        project.Rename(model.Title, DateTimeOffset.UtcNow);
        try { await projects.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { TempData["Error"] = "This project changed in another tab. Reload and try again."; }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var project = await projects.FindOwnedAsync(id, UserId, cancellationToken);
        if (project is null) return NotFound();
        project.SetArchived(!project.IsArchived, DateTimeOffset.UtcNow);
        await projects.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var project = await projects.FindOwnedAsync(id, UserId, cancellationToken);
        if (project is null) return NotFound();
        foreach (var asset in project.MediaAssets) await storage.DeleteAsync(asset.StorageKey, cancellationToken);
        await projects.RemoveAsync(project, cancellationToken);
        await projects.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }
}
