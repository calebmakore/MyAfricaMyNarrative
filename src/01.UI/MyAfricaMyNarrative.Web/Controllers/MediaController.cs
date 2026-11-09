using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;
using MyAfricaMyNarrative.Web.Models;

namespace MyAfricaMyNarrative.Web.Controllers;

[Authorize]
public sealed class MediaController(IProjectRepository projects, ICurrentUser currentUser, IPrivateMediaStorage storage,
    IMediaInspector inspector, IMediaProbe probe, IOptions<MediaOptions> options) : Controller
{
    private Guid UserId => currentUser.UserId ?? throw new InvalidOperationException("An authenticated user ID is required.");

    [HttpPost, ValidateAntiForgeryToken, RequestFormLimits(MultipartBodyLengthLimit = 20 * 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid projectId, UploadViewModel model, CancellationToken cancellationToken)
    {
        var project = await projects.FindOwnedAsync(projectId, UserId, cancellationToken);
        if (project is null) return NotFound();
        if (!ModelState.IsValid || model.File is null) { TempData["Error"] = "Choose a supported source and confirm permission."; return RedirectToAction("Details", "Projects", new { id = projectId }); }
        if (project.MediaAssets.Count >= options.Value.MaxAssetsPerProject || project.MediaAssets.Sum(x => x.ByteLength) + model.File.Length > options.Value.MaxProjectBytes)
        { TempData["Error"] = "This project has reached its private media limit."; return RedirectToAction("Details", "Projects", new { id = projectId }); }

        if (model.File.Length > options.Value.MaxUploadBytes) { TempData["Error"] = "The source exceeds the 20 MiB upload limit."; return RedirectToAction("Details", "Projects", new { id = projectId }); }
        await using var input = model.File.OpenReadStream();
        var inspected = await inspector.InspectAsync(input, cancellationToken);
        if (inspected is null) { TempData["Error"] = "The file content is not a supported JPEG, PNG, WebP, MP3, WAV, M4A/MP4, WebM, or WebA source."; return RedirectToAction("Details", "Projects", new { id = projectId }); }
        // MediaRecorder produces an EBML/WebM container for both audio-only and video content.
        // Preserve its audio intent only after the container signature and decoded streams pass inspection.
        var isWebAudio = model.File.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetExtension(model.File.FileName), ".weba", StringComparison.OrdinalIgnoreCase);
        if (inspected.ContentType == "video/webm" && isWebAudio)
            inspected = new InspectedMedia(MediaKind.Audio, "audio/webm");
        ProbedMedia probed;
        try { probed = await probe.ProbeAsync(input, cancellationToken); }
        catch (Exception exception) when (exception is InvalidDataException or System.ComponentModel.Win32Exception)
        { TempData["Error"] = exception.Message; return RedirectToAction("Details", "Projects", new { id = projectId }); }
        var stored = await storage.WriteOriginalAsync(input, options.Value.MaxUploadBytes, cancellationToken);
        try
        {
            project.MediaAssets.Add(new MediaAsset(project.Id, stored.Key, Path.GetFileName(model.File.FileName), model.DisplayName, stored.Sha256, inspected.ContentType, inspected.Kind, stored.Length, model.Attribution, DateTimeOffset.UtcNow, probed.DurationMs, probed.Width, probed.Height));
            await projects.SaveChangesAsync(cancellationToken);
            TempData["Notice"] = "Source saved privately. You can review it below or continue to Shape.";
        }
        catch { await storage.DeleteAsync(stored.Key, cancellationToken); throw; }
        return RedirectToAction("Details", "Projects", new { id = projectId });
    }

    [HttpGet]
    public async Task<IActionResult> Play(Guid id, CancellationToken cancellationToken)
    {
        var asset = await projects.FindOwnedAssetAsync(id, UserId, cancellationToken);
        if (asset is null || asset.PermissionStatus == PermissionStatus.Withdrawn) return NotFound();
        return File(await storage.OpenReadAsync(asset.StorageKey, cancellationToken), asset.ContentType, enableRangeProcessing: true);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        var project = await projects.FindOwnedAsync(projectId, UserId, cancellationToken);
        var asset = project?.MediaAssets.SingleOrDefault(x => x.Id == id);
        if (asset is null) return NotFound();
        asset.Withdraw(DateTimeOffset.UtcNow);
        await projects.SaveChangesAsync(cancellationToken);
        return RedirectToAction("Details", "Projects", new { id = projectId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Rename(Guid projectId, Guid id, SourceRenameViewModel model, CancellationToken cancellationToken)
    {
        var project = await projects.FindOwnedAsync(projectId, UserId, cancellationToken);
        var asset = project?.MediaAssets.SingleOrDefault(x => x.Id == id);
        if (asset is null) return NotFound();
        if (!ModelState.IsValid) { TempData["Error"] = "Enter a source name between 1 and 120 characters."; return RedirectToAction("Details", "Projects", new { id = projectId }); }
        asset.SetDisplayName(model.DisplayName);
        await projects.SaveChangesAsync(cancellationToken);
        TempData["Notice"] = "Source name updated.";
        return Redirect($"{Url.Action("Details", "Projects", new { id = projectId })}#sources");
    }
}
