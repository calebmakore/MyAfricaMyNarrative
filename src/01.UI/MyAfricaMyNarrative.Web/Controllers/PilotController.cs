using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;
using MyAfricaMyNarrative.Web.Models;

namespace MyAfricaMyNarrative.Web.Controllers;

/// <summary>
/// Pilot evidence for testing the workflow with creators: per-account measures computed from existing records,
/// plus each creator's own reflection. Only the signed-in owner's projects are read; the download holds no media or story text.
/// </summary>
[Authorize]
public sealed class PilotController(IProjectRepository projects, ICurrentUser currentUser) : Controller
{
    private const int MaxProjects = 50;
    private Guid UserId => currentUser.UserId ?? throw new InvalidOperationException("An authenticated user ID is required.");

    public async Task<IActionResult> Index(CancellationToken ct) => View(await Collect(ct));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveFeedback(Guid id, PilotFeedbackViewModel model, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var help = model.NeededHelp.Aggregate(HelpArea.None, (all, x) => all | x);
        if (!ModelState.IsValid || !Enum.IsDefined(model.IntentMatch)) { TempData["Error"] = "Choose how well the film says what you meant."; return RedirectToAction("Export", "Studio", new { id }, "reflect"); }
        try
        {
            var existing = project.PilotFeedback.FirstOrDefault();
            if (existing is null) project.PilotFeedback.Add(new PilotFeedback(id, model.IntentMatch, help, model.HelpNote ?? "", model.Comment ?? "", DateTimeOffset.UtcNow));
            else existing.Update(model.IntentMatch, help, model.HelpNote, model.Comment, DateTimeOffset.UtcNow);
        }
        catch (ArgumentException exception) { TempData["Error"] = exception.Message; return RedirectToAction("Export", "Studio", new { id }, "reflect"); }
        await projects.SaveChangesAsync(ct);
        TempData["Notice"] = "Thank you — your reflection is saved with this story.";
        return RedirectToAction("Export", "Studio", new { id }, "reflect");
    }

    /// <summary>Measures and reflections as JSON for the pilot facilitator. Titles are included; media, transcripts and scene words are not.</summary>
    [HttpGet]
    public async Task<IActionResult> Download(CancellationToken ct)
    {
        var rows = await Collect(ct);
        var payload = new
        {
            Schema = "mamn-pilot-1",
            ExportedAtUtc = DateTimeOffset.UtcNow,
            Note = "Computed from this account's own records. Durations are wall-clock time from creating the story. No media, transcripts or scene words are included.",
            Stories = rows.Select(x => new
            {
                x.Title, x.StoryType, x.StartedAtUtc,
                MinutesToFirstSource = x.ToFirstSource?.TotalMinutes, MinutesToApprovedCut = x.ToApprovedCut?.TotalMinutes, MinutesToFinalFilm = x.ToFinalFilm?.TotalMinutes,
                x.StoryVersions, x.Scenes, x.QuotedScenes, x.CreatorWrittenScenes, x.AiWordScenes,
                x.LiveAiRequests, x.AiSuggestionsKept, x.AiSuggestionsRejected, x.PracticeSuggestions,
                x.PlanVersions, x.PlanApproved, x.GlossaryApproved, x.ContributorsRecorded,
                Reflection = x.Feedback is null ? null : new
                {
                    IntentMatch = x.Feedback.IntentMatch.ToString(),
                    NeededHelp = PilotMetrics.HelpAreas.Where(h => x.Feedback.NeededHelp.HasFlag(h.Area)).Select(h => h.Label),
                    x.Feedback.HelpNote, x.Feedback.Comment, x.Feedback.UpdatedAtUtc
                }
            })
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions { WriteIndented = true });
        return File(bytes, "application/json", $"pilot-results-{DateTime.UtcNow:yyyyMMdd}.json");
    }

    private async Task<IReadOnlyList<PilotProjectMetrics>> Collect(CancellationToken ct)
    {
        var list = (await projects.ListAsync(UserId, ct)).Take(MaxProjects).ToList();
        var rows = new List<PilotProjectMetrics>();
        foreach (var item in list)
        {
            var project = await projects.FindOwnedStudioAsync(item.Id, UserId, ct); if (project is null) continue;
            rows.Add(PilotMetrics.Compute(project, await projects.ListOwnedArtifactsAsync(project.Id, UserId, ct)));
        }
        return rows;
    }
}
