using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;
using MyAfricaMyNarrative.Web.Models;

namespace MyAfricaMyNarrative.Web.Controllers;

[Authorize]
public sealed class ProjectsController(IProjectRepository projects, ICurrentUser currentUser, IPrivateMediaStorage storage) : Controller
{
    private Guid UserId => currentUser.UserId ?? throw new InvalidOperationException("An authenticated user ID is required.");
    public async Task<IActionResult> Index(CancellationToken cancellationToken) => View(await projects.ListAsync(UserId, cancellationToken));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProjectEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View("Index", await projects.ListAsync(UserId, cancellationToken));
        var project = new Project(UserId, model.Title, DateTimeOffset.UtcNow);
        await projects.AddAsync(project, cancellationToken);
        await projects.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Details), new { id = project.Id });
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var project = await projects.FindOwnedAsync(id, UserId, cancellationToken);
        return project is null ? NotFound() : View(project);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Rename(Guid id, ProjectEditViewModel model, CancellationToken cancellationToken)
    {
        var project = await projects.FindOwnedAsync(id, UserId, cancellationToken);
        if (project is null) return NotFound();
        if (!ModelState.IsValid) return View("Details", project);
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
