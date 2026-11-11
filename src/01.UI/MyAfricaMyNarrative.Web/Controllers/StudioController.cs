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
    IExportReadinessService readiness, IPrivateMediaStorage storage, IOptions<AiOptions> aiOptions,
    IStoryAssistant storyAssistant, IAudioTranscriber audioTranscriber, IStoryCoach storyCoach, IGlossaryAssistant glossaryAssistant) : Controller
{
    internal static readonly JsonSerializerOptions StructureJson = new() { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

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
        var check = readiness.Check(project); var artifacts = await projects.ListOwnedArtifactsAsync(id, UserId, ct);
        return View(view, new StudioViewModel(project, current, check, await projects.ListOwnedJobsAsync(id, UserId, ct), artifacts,
            aiOptions.Value.Mode, StudioProgressCalculator.Calculate(project, check, artifacts)));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTranscript(Guid id, TranscriptEditViewModel model, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct);
        if (project is null) return NotFound();
        var asset = project.MediaAssets.SingleOrDefault(x => x.Id == model.AssetId && x.PermissionStatus == PermissionStatus.Granted && x.CanProvideSound);
        if (!ModelState.IsValid || asset is null || model.EndMs <= model.StartMs || model.EndMs > asset.DurationMs)
        { TempData["Error"] = "Choose permitted audio, reviewed text, language and a valid playback range."; return RedirectToAction(nameof(Shape), new { id }); }
        project.TranscriptRevisions.Add(new TranscriptRevision(id, asset.Id, model.Text, model.Language, model.StartMs, model.EndMs, UserId, DateTimeOffset.UtcNow));
        await projects.SaveChangesAsync(ct);
        TempData["Notice"] = "Reviewed transcript revision saved. The original recording was not changed.";
        return RedirectToAction(nameof(Shape), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateTranscription(Guid id, Guid assetId, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var asset = project.MediaAssets.SingleOrDefault(x => x.Id == assetId && x.CanProvideSound && x.PermissionStatus == PermissionStatus.Granted);
        if (asset is null) { TempData["Error"] = "Choose permitted audio before requesting transcription."; return RedirectToAction(nameof(Shape), new { id }); }
        if (aiOptions.Value.Mode != "Live") { TempData["Error"] = "Live transcription is disabled. You can still enter and review the words manually."; return RedirectToAction(nameof(Shape), new { id }); }
        var liveCalls = ProductionController.LiveCalls(project);
        if (liveCalls >= aiOptions.Value.MaxCallsPerProject) { TempData["Error"] = "This project reached its bounded Live AI call limit."; return RedirectToAction(nameof(Shape), new { id }); }
        try
        {
            await using var source = await storage.OpenReadAsync(asset.StorageKey, ct);
            var result = await audioTranscriber.TranscribeAsync(source, asset.OriginalName, asset.ContentType, ct);
            project.TranscriptionProposals.Add(new TranscriptionProposal(id, asset.Id, result.Text,
                string.Join(", ", result.Languages), result.Provider, result.Model, DateTimeOffset.UtcNow));
            await projects.SaveChangesAsync(ct);
            TempData["Notice"] = "Live transcription returned as a proposal. Listen, add the real timing and language, then review it before use.";
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException)
        { TempData["Error"] = AiServiceException.Describe(exception); }
        return RedirectToAction(nameof(Shape), null, new { id }, "transcription-proposals");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewTranscription(Guid id, TranscriptionReviewViewModel model, bool reject, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var proposal = project.TranscriptionProposals.SingleOrDefault(x => x.Id == model.ProposalId);
        if (proposal is null) return NotFound();
        if (proposal.Status != TranscriptionProposalStatus.NeedsTiming) { TempData["Error"] = "That transcription proposal was already reviewed."; return RedirectToAction(nameof(Shape), new { id }); }
        if (reject) { proposal.Reject(DateTimeOffset.UtcNow); await projects.SaveChangesAsync(ct); TempData["Notice"] = "Transcription proposal rejected; the original recording was preserved."; return RedirectToAction(nameof(Shape), new { id }); }
        var asset = project.MediaAssets.Single(x => x.Id == proposal.AssetId);
        if (!ModelState.IsValid || model.EndMs <= model.StartMs || model.EndMs > asset.DurationMs)
        { TempData["Error"] = "Listen to the recording and enter a language and valid timing within the source duration."; return RedirectToAction(nameof(Shape), new { id }); }
        project.TranscriptRevisions.Add(new TranscriptRevision(id, asset.Id, proposal.Text, model.Language, model.StartMs, model.EndMs, UserId, DateTimeOffset.UtcNow));
        proposal.Review(DateTimeOffset.UtcNow); await projects.SaveChangesAsync(ct);
        TempData["Notice"] = "Transcription reviewed and saved as an immutable source revision.";
        return RedirectToAction(nameof(Shape), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddScene(Guid id, SceneEditViewModel model, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct);
        if (project is null) return NotFound();
        var audio = project.MediaAssets.SingleOrDefault(x => x.Id == model.AudioAssetId && x.CanProvideSound && x.PermissionStatus == PermissionStatus.Granted);
        var visual = project.MediaAssets.SingleOrDefault(x => x.Id == model.VisualAssetId && x.Kind != MediaKind.Audio && x.PermissionStatus == PermissionStatus.Granted);
        if (!TryBuildDraft(project, model, out var draft, out var error)) { TempData["Error"] = error; return RedirectToAction(nameof(Shape), new { id }); }
        var drafts = CurrentDrafts(project);
        drafts.Add(draft);
        project.StoryRevisions.Add(new StoryRevision(id, NextRevision(project), drafts, DateTimeOffset.UtcNow));
        await projects.SaveChangesAsync(ct);
        TempData["Notice"] = "A new immutable story revision was saved.";
        return RedirectToAction(nameof(Shape), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditScene(Guid id, Guid sceneId, SceneEditViewModel model, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var current = Current(project); var ordered = current?.Scenes.OrderBy(x => x.Order).ToList() ?? [];
        var index = ordered.FindIndex(x => x.Id == sceneId);
        if (index < 0) { TempData["Error"] = "That scene is not in the current revision. Refresh and edit the latest story."; return RedirectToAction(nameof(Shape), new { id }); }
        if (!TryBuildDraft(project, model, out var draft, out var error)) { TempData["Error"] = $"Scene {index + 1} was not changed. {error}"; return RedirectToAction(nameof(Shape), null, new { id }, "current-cut"); }
        var drafts = CurrentDrafts(project);
        if (drafts[index] == draft) { TempData["Notice"] = "No changes to save."; return RedirectToAction(nameof(Shape), null, new { id }, "current-cut"); }
        drafts[index] = draft;
        project.StoryRevisions.Add(new StoryRevision(id, NextRevision(project), drafts, DateTimeOffset.UtcNow));
        await projects.SaveChangesAsync(ct);
        TempData["Notice"] = $"Scene {index + 1} updated as a new revision. Approve it again before export.";
        return RedirectToAction(nameof(Shape), null, new { id }, "current-cut");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveScene(Guid id, Guid sceneId, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var current = Current(project); var ordered = current?.Scenes.OrderBy(x => x.Order).ToList() ?? [];
        var index = ordered.FindIndex(x => x.Id == sceneId);
        if (index < 0) { TempData["Error"] = "That scene is not in the current revision."; return RedirectToAction(nameof(Shape), new { id }); }
        var drafts = CurrentDrafts(project); drafts.RemoveAt(index);
        project.StoryRevisions.Add(new StoryRevision(id, NextRevision(project), drafts, DateTimeOffset.UtcNow));
        await projects.SaveChangesAsync(ct);
        TempData["Notice"] = $"Scene “{ordered[index].Title}” removed in a new revision. Earlier revisions and the original recordings are unchanged.";
        return RedirectToAction(nameof(Shape), null, new { id }, "current-cut");
    }

    private static bool TryBuildDraft(Project project, SceneEditViewModel model, out SceneDraft draft, out string error)
    {
        draft = null!; error = "Each scene needs permitted audio, a real visual and a valid reviewed source range.";
        if (!Enum.IsDefined(model.TextOrigin)) { error = "Choose whose words these are."; return false; }
        var audio = project.MediaAssets.SingleOrDefault(x => x.Id == model.AudioAssetId && x.CanProvideSound && x.PermissionStatus == PermissionStatus.Granted);
        var visual = project.MediaAssets.SingleOrDefault(x => x.Id == model.VisualAssetId && x.Kind != MediaKind.Audio && x.PermissionStatus == PermissionStatus.Granted);
        var transcript = model.TranscriptRevisionId is null ? null : project.TranscriptRevisions.SingleOrDefault(x => x.Id == model.TranscriptRevisionId);
        if (string.IsNullOrWhiteSpace(model.Title) || model.Title.Trim().Length > 120 || (model.Text?.Length ?? 0) > 4000 || (model.Caption?.Length ?? 0) > 2000 || (model.Translation?.Length ?? 0) > 2000)
        { error = "Enter a scene title (up to 120 characters) and keep text, caption and translation within their limits."; return false; }
        if (audio is null || visual is null || model.StartMs < 0 || model.EndMs <= model.StartMs || model.EndMs - model.StartMs > 300_000) return false;
        if (audio.DurationMs > 0 && model.EndMs > audio.DurationMs) { error = $"Mark out is beyond the end of “{audio.DisplayName}” ({audio.DurationMs} ms)."; return false; }
        var candidate = new SceneDraft(model.Title.Trim(), (model.Text ?? "").Trim(), model.TextOrigin, audio.Id, visual.Id, transcript?.Id, model.StartMs, model.EndMs, (model.Caption ?? "").Trim(), (model.Translation ?? "").Trim());
        if (SourceFidelity.ValidateScene(candidate, transcript) is string fidelity) { error = fidelity; return false; }
        draft = candidate; return true;
    }

    /// <summary>
    /// Story coach: proposes opening / context / turning point / ending and lists real gaps as questions for the creator.
    /// Live reads reviewed words only when the creator ticks the sharing box for this request. Suggested narration is labelled AI text.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateStructure(Guid id, bool live, bool shareWords, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var revision = Current(project);
        if (revision is null || revision.Scenes.Count == 0) { TempData["Error"] = "Add scenes before asking the story coach."; return RedirectToAction(nameof(Shape), null, new { id }, "coach"); }
        var request = StoryCoachRules.BuildRequest(project, revision, live && shareWords);
        StoryStructure structure; string source; var fixture = !live;
        if (live)
        {
            if (aiOptions.Value.Mode != "Live") { TempData["Error"] = "Live AI is not configured. Use the practice coach instead."; return RedirectToAction(nameof(Shape), null, new { id }, "coach"); }
            if (ProductionController.LiveCalls(project) >= aiOptions.Value.MaxCallsPerProject) { TempData["Error"] = "This project reached its bounded Live AI call limit."; return RedirectToAction(nameof(Shape), null, new { id }, "coach"); }
            try { var result = await storyCoach.ProposeStructureAsync(request, ct); structure = result.Structure; source = $"Live {result.Provider} ({result.Model})" + (request.WordsShared ? " · read your reviewed words" : " · titles and intention only"); }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException)
            {
                var failed = new StudioProposal(id, ProposalKind.StoryStructure, revision.SnapshotHash, "Live story coaching failed: " + exception.Message, "{}", DateTimeOffset.UtcNow, false);
                failed.Review(false, DateTimeOffset.UtcNow); project.StudioProposals.Add(failed); await projects.SaveChangesAsync(ct);
                TempData["Error"] = AiServiceException.Describe(exception) + " Nothing was changed."; return RedirectToAction(nameof(Shape), null, new { id }, "coach");
            }
        }
        else { structure = StoryCoachRules.Practice(request); source = StoryCoachRules.PracticeSource; }

        var (safe, error) = StoryCoachRules.Validate(structure, revision.Scenes.Select(x => x.Id).ToList());
        if (safe is null) { TempData["Error"] = "The story coach suggestion was refused safely: " + error; return RedirectToAction(nameof(Shape), null, new { id }, "coach"); }
        foreach (var pending in project.StudioProposals.Where(x => x.Kind == ProposalKind.StoryStructure && x.Status == ProposalStatus.Pending)) pending.Review(false, DateTimeOffset.UtcNow);
        project.StudioProposals.Add(new StudioProposal(id, ProposalKind.StoryStructure, revision.SnapshotHash, $"{source}: {safe.Summary}", JsonSerializer.Serialize(safe, StructureJson), DateTimeOffset.UtcNow, fixture));
        var added = 0;
        foreach (var gap in safe.Gaps)
        {
            var field = StoryCoachRules.FieldFor(gap.Kind);
            if (project.CreatorQuestions.Any(x => x.Field == field && x.AnsweredAtUtc is null)) continue;
            project.CreatorQuestions.Add(new CreatorQuestion(id, field, gap.Question.Length > 300 ? gap.Question[..300] : gap.Question, DateTimeOffset.UtcNow)); added++;
        }
        await projects.SaveChangesAsync(ct);
        TempData["Notice"] = safe.Gaps.Count == 0 ? "The coach found no gaps. Review the structure below." : $"The coach found {safe.Gaps.Count} thing(s) to look at; {added} new question(s) are waiting for your answers.";
        return RedirectToAction(nameof(Shape), null, new { id }, "coach");
    }

    private IActionResult ToGlossary(Guid id) => RedirectToAction(nameof(Shape), null, new { id }, "glossary");

    internal static IReadOnlyList<GlossarySuggestion> Suggestions(StudioProposal proposal)
    {
        try { return JsonSerializer.Deserialize<List<GlossarySuggestion>>(proposal.PayloadJson, StructureJson) ?? []; }
        catch (JsonException) { return []; }
    }

    /// <summary>Suggestions from a proposal whose term is not yet in the glossary.</summary>
    internal static IReadOnlyList<GlossarySuggestion> RemainingSuggestions(Project project, StudioProposal proposal) =>
        Suggestions(proposal).Where(x => project.Glossary.All(g => !string.Equals(g.Term, x.Term, StringComparison.OrdinalIgnoreCase))).ToList();

    /// <summary>
    /// Finds expressions in the reviewed captions that may need an explanation. Live reads the captions, translations and local-context note;
    /// practice lists the creator's own protected terms that appear. Nothing is added to the glossary until the creator chooses it.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SuggestGlossary(Guid id, bool live, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var revision = Current(project);
        if (revision is null || revision.Scenes.Count == 0) { TempData["Error"] = "Add scenes with captions before looking for words to explain."; return ToGlossary(id); }
        var request = GlossarySuggestionRules.BuildRequest(project, revision);
        if (request.Captions.All(x => string.IsNullOrWhiteSpace(x.Caption) && string.IsNullOrWhiteSpace(x.Translation))) { TempData["Error"] = "Your scenes have no caption words yet. Check the words in step 2 first."; return ToGlossary(id); }
        if (project.Glossary.Count >= GlossaryRules.MaxEntries) { TempData["Error"] = $"A story can explain at most {GlossaryRules.MaxEntries} expressions."; return ToGlossary(id); }
        IReadOnlyList<GlossarySuggestion> raw; string source;
        if (live)
        {
            if (aiOptions.Value.Mode != "Live") { TempData["Error"] = "Live AI is not configured. Use the practice finder instead."; return ToGlossary(id); }
            if (ProductionController.LiveCalls(project) >= aiOptions.Value.MaxCallsPerProject) { TempData["Error"] = "This project reached its bounded Live AI call limit."; return ToGlossary(id); }
            try { var result = await glossaryAssistant.SuggestAsync(request, ct); raw = result.Suggestions; source = $"Live {result.Provider} ({result.Model}) · read your captions"; }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException)
            {
                var failed = new StudioProposal(id, ProposalKind.GlossarySuggestions, revision.SnapshotHash, "Live glossary suggestions failed: " + exception.Message, "[]", DateTimeOffset.UtcNow, false);
                failed.Review(false, DateTimeOffset.UtcNow); project.StudioProposals.Add(failed); await projects.SaveChangesAsync(ct);
                TempData["Error"] = AiServiceException.Describe(exception) + " Nothing was changed."; return ToGlossary(id);
            }
        }
        else { raw = GlossarySuggestionRules.Practice(request); source = GlossarySuggestionRules.PracticeSource; }

        var safe = GlossarySuggestionRules.Validate(request, raw);
        foreach (var pending in project.StudioProposals.Where(x => x.Kind == ProposalKind.GlossarySuggestions && x.Status == ProposalStatus.Pending)) pending.Review(false, DateTimeOffset.UtcNow);
        var proposal = new StudioProposal(id, ProposalKind.GlossarySuggestions, revision.SnapshotHash, $"{source}: {safe.Count} word(s) to consider", JsonSerializer.Serialize(safe, StructureJson), DateTimeOffset.UtcNow, !live);
        if (safe.Count == 0) proposal.Review(false, DateTimeOffset.UtcNow);
        project.StudioProposals.Add(proposal);
        await projects.SaveChangesAsync(ct);
        var dropped = raw.Count - safe.Count;
        TempData["Notice"] = safe.Count == 0
            ? (live ? "No new words to explain were found in your captions." : "None of your protected words appear in a caption without an explanation. Try the AI suggestions.")
            : $"{safe.Count} word(s) to consider. Add the ones you want — each stays a draft until you approve it." + (live && dropped > 0 ? $" {dropped} suggestion(s) not found in your captions were left out." : "");
        return ToGlossary(id);
    }

    /// <summary>Adds every suggestion that has a drafted explanation as an unapproved draft.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAllGlossarySuggestions(Guid id, Guid proposalId, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var proposal = project.StudioProposals.SingleOrDefault(x => x.Id == proposalId && x.Kind == ProposalKind.GlossarySuggestions && x.Status == ProposalStatus.Pending);
        if (proposal is null) { TempData["Error"] = "Those suggestions are no longer waiting. Ask for new ones."; return ToGlossary(id); }
        var added = 0;
        foreach (var item in RemainingSuggestions(project, proposal).Where(x => x.Explanation.Length > 0))
        {
            if (project.Glossary.Count >= GlossaryRules.MaxEntries) break;
            project.Glossary.Add(new GlossaryEntry(id, item.Term, item.Language, item.Explanation, DateTimeOffset.UtcNow, aiDrafted: true)); added++;
        }
        var left = RemainingSuggestions(project, proposal).Count;
        if (left == 0) proposal.Review(true, DateTimeOffset.UtcNow);
        await projects.SaveChangesAsync(ct);
        TempData["Notice"] = $"Added {added} draft explanation(s). Check each one, then approve it to show it in the film." + (left > 0 ? $" {left} word(s) still need an explanation in your words." : "");
        return ToGlossary(id);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DismissGlossarySuggestions(Guid id, Guid proposalId, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var proposal = project.StudioProposals.SingleOrDefault(x => x.Id == proposalId && x.Kind == ProposalKind.GlossarySuggestions && x.Status == ProposalStatus.Pending);
        if (proposal is not null) { proposal.Review(false, DateTimeOffset.UtcNow); await projects.SaveChangesAsync(ct); }
        TempData["Notice"] = "Suggestions dismissed. Your glossary is unchanged."; return ToGlossary(id);
    }


    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddGlossary(Guid id, GlossaryViewModel model, Guid? suggestionId, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        if (!ModelState.IsValid) { TempData["Error"] = "Enter the expression (2–80 characters) and a short explanation (2–160 characters)."; return ToGlossary(id); }
        if (project.Glossary.Count >= GlossaryRules.MaxEntries) { TempData["Error"] = $"A story can explain at most {GlossaryRules.MaxEntries} expressions."; return ToGlossary(id); }
        if (project.Glossary.Any(x => string.Equals(x.Term, model.Term.Trim(), StringComparison.OrdinalIgnoreCase))) { TempData["Error"] = "That expression is already in your glossary. Edit it instead."; return ToGlossary(id); }
        var suggested = suggestionId is { } sid ? project.StudioProposals.SingleOrDefault(x => x.Id == sid && x.Kind == ProposalKind.GlossarySuggestions && x.Status == ProposalStatus.Pending) : null;
        var draft = suggested is null ? null : Suggestions(suggested).FirstOrDefault(x => string.Equals(x.Term, model.Term.Trim(), StringComparison.OrdinalIgnoreCase));
        var aiDrafted = draft is not null && draft.Explanation.Length > 0 && draft.Explanation == model.Explanation.Trim();
        project.Glossary.Add(new GlossaryEntry(id, model.Term, model.Language ?? "", model.Explanation, DateTimeOffset.UtcNow, aiDrafted));
        if (suggested is not null && RemainingSuggestions(project, suggested).Count == 0) suggested.Review(true, DateTimeOffset.UtcNow);
        await projects.SaveChangesAsync(ct); TempData["Notice"] = "Explanation saved as a draft. Approve it to show it in the film."; return ToGlossary(id);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateGlossary(Guid id, Guid entryId, GlossaryViewModel model, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var entry = project.Glossary.SingleOrDefault(x => x.Id == entryId); if (entry is null) return NotFound();
        if (!ModelState.IsValid) { TempData["Error"] = "Enter the expression and a short explanation."; return ToGlossary(id); }
        if (project.Glossary.Any(x => x.Id != entryId && string.Equals(x.Term, model.Term.Trim(), StringComparison.OrdinalIgnoreCase))) { TempData["Error"] = "Another entry already explains that expression."; return ToGlossary(id); }
        var wasApproved = entry.IsApproved;
        entry.Update(model.Term, model.Language ?? "", model.Explanation, DateTimeOffset.UtcNow);
        await projects.SaveChangesAsync(ct);
        TempData["Notice"] = wasApproved && !entry.IsApproved ? "Explanation changed — approve it again before the final export." : "No changes to save.";
        return ToGlossary(id);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveGlossary(Guid id, Guid entryId, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var entry = project.Glossary.SingleOrDefault(x => x.Id == entryId); if (entry is null) return NotFound();
        entry.Approve(DateTimeOffset.UtcNow); await projects.SaveChangesAsync(ct);
        TempData["Notice"] = $"“{entry.Term}” will be explained the first time it appears on screen."; return ToGlossary(id);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveGlossary(Guid id, Guid entryId, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var entry = project.Glossary.SingleOrDefault(x => x.Id == entryId); if (entry is null) return NotFound();
        project.Glossary.Remove(entry); await projects.SaveChangesAsync(ct);
        TempData["Notice"] = $"Removed the explanation for “{entry.Term}”. The word itself stays exactly as spoken."; return ToGlossary(id);
    }

    /// <summary>Drag-and-drop reorder: saves the full new order as one new revision.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReorderScenes(Guid id, Guid[] order, string? baseline, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var current = Current(project);
        if (current is null || current.SnapshotHash != baseline) { TempData["Error"] = "The story changed in another tab. Refresh, then reorder again."; return RedirectToAction(nameof(Shape), null, new { id }, "current-cut"); }
        var scenes = current.Scenes.ToDictionary(x => x.Id);
        if (order.Length != scenes.Count || order.Distinct().Count() != order.Length || order.Any(x => !scenes.ContainsKey(x))) { TempData["Error"] = "That order did not match your scenes. Nothing was changed."; return RedirectToAction(nameof(Shape), null, new { id }, "current-cut"); }
        if (order.SequenceEqual(current.Scenes.OrderBy(x => x.Order).Select(x => x.Id))) return RedirectToAction(nameof(Shape), null, new { id }, "current-cut");
        project.StoryRevisions.Add(new StoryRevision(id, NextRevision(project), order.Select(x => scenes[x].ToDraft()), DateTimeOffset.UtcNow));
        await projects.SaveChangesAsync(ct);
        TempData["Notice"] = "New order saved as a new version. Approve it again before export.";
        return RedirectToAction(nameof(Shape), null, new { id }, "current-cut");
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
        if (intent is null) { intent = new CreatorIntent(id, model.Audience, model.Meaning, model.Tone, model.LocalContext, model.ProtectedTerms, DateTimeOffset.UtcNow); project.CreatorIntents.Add(intent); }
        else intent.Update(model.Audience, model.Meaning, model.Tone, model.LocalContext, model.ProtectedTerms, DateTimeOffset.UtcNow);
        intent.SetTranslationLanguage(model.TranslationLanguage, DateTimeOffset.UtcNow);
        await projects.SaveChangesAsync(ct); TempData["Notice"] = "Creator intention saved."; return RedirectToAction(nameof(Shape), null, new { id }, "intention");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateCreatorQuestions(Guid id, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var intent = project.CreatorIntents.SingleOrDefault();
        var candidates = new[] {
            ("Audience", intent?.Audience, "Who should this story speak to, and what do they already understand?"),
            ("Meaning", intent?.Meaning, "What is the one meaning the finished story must not lose?"),
            ("Tone", intent?.Tone, "How should the story feel to the people represented in it?"),
            ("Local context", intent?.LocalContext, "What local context would an outsider need to understand this story fairly?"),
            ("Protected expressions", intent?.ProtectedTerms, "Which names, words or expressions must remain exactly as you use them?") };
        foreach (var candidate in candidates.Where(x => string.IsNullOrWhiteSpace(x.Item2)))
            if (!project.CreatorQuestions.Any(x => x.Field == candidate.Item1 && x.AnsweredAtUtc is null))
                project.CreatorQuestions.Add(new CreatorQuestion(id, candidate.Item1, candidate.Item3, DateTimeOffset.UtcNow));
        await projects.SaveChangesAsync(ct); TempData["Notice"] = "Focused questions are ready. Only your answers are treated as creator facts.";
        return RedirectToAction(nameof(Shape), null, new { id }, "creator-questions");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AnswerCreatorQuestion(Guid id, CreatorAnswerViewModel model, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var question = project.CreatorQuestions.SingleOrDefault(x => x.Id == model.QuestionId); if (question is null) return NotFound();
        if (!ModelState.IsValid || question.AnsweredAtUtc is not null) { TempData["Error"] = "Enter a bounded answer for an unanswered question."; return RedirectToAction(nameof(Shape), new { id }); }
        question.RecordAnswer(model.Answer, DateTimeOffset.UtcNow); await projects.SaveChangesAsync(ct);
        TempData["Notice"] = "Creator answer saved separately from AI suggestions.";
        return RedirectToAction(nameof(Shape), null, new { id }, "creator-questions");
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
            var creatorAnswers = string.Join("\n", project.CreatorQuestions.Where(x => x.AnsweredAtUtc is not null).Select(x => $"{x.Field}: {x.Answer}"));
            try
            {
                var result = await storyAssistant.ProposeOrderAsync(new StoryAssistantRequest(
                    revision.Scenes.OrderBy(x => x.Order).Select(x => new StoryAssistantScene(x.Id, x.Order, x.Title)).ToList(),
                    intent?.Audience ?? "", intent?.Meaning ?? "", intent?.Tone ?? "", string.Join("\n", new[] { intent?.LocalContext ?? "", creatorAnswers }.Where(x => x.Length > 0)), intent?.ProtectedTerms ?? ""), ct);
                order = result.SceneOrder.ToArray(); summary = $"Live {result.Provider} proposal ({result.Model}): {result.Summary}";
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException)
            { TempData["Error"] = AiServiceException.Describe(exception); return RedirectToAction(nameof(Shape), new { id }); }
        }
        var known = revision.Scenes.Select(x => x.Id).Order().ToArray();
        if (order.Length != known.Length || !order.Order().SequenceEqual(known)) { TempData["Error"] = "The assistant proposal changed or invented scene IDs and was rejected safely."; return RedirectToAction(nameof(Shape), new { id }); }
        project.StudioProposals.Add(new StudioProposal(id, ProposalKind.StoryOrder, revision.SnapshotHash, summary, JsonSerializer.Serialize(order), DateTimeOffset.UtcNow, fixture));
        await projects.SaveChangesAsync(ct); TempData["Notice"] = fixture ? "A clearly labelled fixture proposal is ready to compare." : "A bounded Live proposal is ready to compare."; return RedirectToAction(nameof(Shape), null, new { id }, "proposals");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewProposal(Guid id, Guid proposalId, bool apply, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var proposal = project.StudioProposals.SingleOrDefault(x => x.Id == proposalId); if (proposal is null) return NotFound();
        if (proposal.Status != ProposalStatus.Pending) { TempData["Error"] = "That proposal was already reviewed."; return RedirectToAction(proposal.Kind == ProposalKind.ResourcePlan ? nameof(Make) : nameof(Shape), new { id }); }
        if (apply && proposal.Kind is ProposalKind.StoryOrder or ProposalKind.StoryStructure)
        {
            var current = Current(project); if (current is null || current.SnapshotHash != proposal.BaselineHash) { TempData["Error"] = "The story changed. Generate a fresh proposal instead of applying a stale one."; return RedirectToAction(nameof(Shape), new { id }); }
            var ids = proposal.Kind == ProposalKind.StoryStructure
                ? StoryCoachRules.Order(JsonSerializer.Deserialize<StoryStructure>(proposal.PayloadJson, StructureJson)!).ToArray()
                : JsonSerializer.Deserialize<Guid[]>(proposal.PayloadJson) ?? []; var scenes = current.Scenes.ToDictionary(x => x.Id);
            if (ids.Length != scenes.Count || ids.Any(x => !scenes.ContainsKey(x))) { TempData["Error"] = "The fixture proposal references an invalid scene and was rejected safely."; return RedirectToAction(nameof(Shape), new { id }); }
            var drafts = ids.Select(sceneId => scenes[sceneId].ToDraft());
            project.StoryRevisions.Add(new StoryRevision(id, NextRevision(project), drafts, DateTimeOffset.UtcNow));
        }
        if (apply && proposal.Kind == ProposalKind.ResourcePlan)
        {
            var ids = JsonSerializer.Deserialize<Guid[]>(proposal.PayloadJson) ?? [];
            if (ids.Any(resourceId => project.Resources.All(x => x.Id != resourceId || !x.IsAvailable))) { TempData["Error"] = "Resources changed. Generate a fresh plan instead of applying a stale one."; return RedirectToAction(nameof(Make), new { id }); }
        }
        proposal.Review(apply, DateTimeOffset.UtcNow); await projects.SaveChangesAsync(ct);
        TempData["Notice"] = apply ? "Proposal applied and recorded." : "Proposal rejected. Your current work was preserved.";
        return RedirectToAction(proposal.Kind == ProposalKind.ResourcePlan ? nameof(Make) : nameof(Shape), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddResource(Guid id, ResourceEditViewModel model, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        if (!ModelState.IsValid) { TempData["Error"] = "Enter a resource name and availability."; return RedirectToAction(nameof(Make), new { id }); }
        int? minutes = model.Kind == ResourceKind.Time && model.Hours is > 0 ? (int)Math.Round(model.Hours.Value * 60) : null;
        if (model.Kind == ResourceKind.Time && minutes is null) { TempData["Error"] = "Say how many hours of filming time you have, so the plan can fit it."; return RedirectToAction(nameof(Make), null, new { id }, "resources"); }
        try { project.Resources.Add(new ResourceItem(id, model.Kind, model.Name, model.Availability, DateTimeOffset.UtcNow, minutes)); }
        catch (ArgumentException exception) { TempData["Error"] = exception.Message; return RedirectToAction(nameof(Make), null, new { id }, "resources"); }
        await projects.SaveChangesAsync(ct);
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
        var targetHash = check.ExportHash;
        var existing = await projects.FindOwnedJobForUpdateAsync(id, UserId, kind, targetHash, ct);
        if (existing is not null)
        {
            if (existing.State is JobState.Queued or JobState.Running or JobState.Succeeded)
            { TempData["Notice"] = "This exact version is already being made or is ready below."; return RedirectToAction(nameof(Export), new { id }); }
            existing.Retry(DateTimeOffset.UtcNow); await projects.SaveChangesAsync(ct);
            TempData["Notice"] = "Trying again. Progress shows below.";
            return RedirectToAction(nameof(Export), new { id });
        }
        var payload = JsonSerializer.Serialize(new { StoryRevisionId = check.Revision.Id, Final = final, check.ExportHash });
        await projects.AddJobAsync(new Job(id, UserId, kind, targetHash, payload, DateTimeOffset.UtcNow), ct); await projects.SaveChangesAsync(ct);
        TempData["Notice"] = "Your film is being made. You can keep working — this page shows progress and the film appears here when it’s ready.";
        return RedirectToAction(nameof(Export), null, new { id }, "render");
    }

    /// <summary>
    /// Live progress for the Export page: the active render (or the latest one) for a project the signed-in user owns.
    /// Steps come from the renderer itself; nothing is estimated.
    /// </summary>
    [HttpGet, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> ExportStatus(Guid id, CancellationToken ct)
    {
        var jobs = await projects.ListOwnedJobsAsync(id, UserId, ct);
        if (jobs.Count == 0 && await projects.FindOwnedAsync(id, UserId, ct) is null) return NotFound();
        var job = jobs.Where(x => x.State is JobState.Queued or JobState.Running).OrderBy(x => x.CreatedAtUtc).FirstOrDefault()
            ?? jobs.OrderByDescending(x => x.CreatedAtUtc).FirstOrDefault();
        if (job is null) return Json(new { state = "none" });
        var now = DateTimeOffset.UtcNow;
        return Json(new
        {
            jobId = job.Id,
            state = job.State.ToString().ToLowerInvariant(),
            final = job.Kind == "RenderFinal",
            step = job.ProgressStep,
            total = job.ProgressTotal,
            note = job.State switch
            {
                JobState.Queued when job.Attempts > 0 => "Something went wrong — trying again shortly",
                JobState.Queued => "Waiting to start",
                JobState.Running => job.ProgressNote.Length > 0 ? job.ProgressNote : "Getting started",
                JobState.Succeeded => "Your film is ready",
                JobState.Failed => "The film could not be made",
                _ => "Stopped"
            },
            elapsedSeconds = (int)(now - (job.StartedAtUtc ?? job.CreatedAtUtc)).TotalSeconds,
            waitingSeconds = job.State == JobState.Queued ? (int)(now - job.CreatedAtUtc).TotalSeconds : 0,
            attempts = job.Attempts,
            failure = job.State == JobState.Failed ? job.LastFailure : null
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelJob(Guid id, Guid jobId, CancellationToken ct)
    {
        var project = await projects.FindOwnedAsync(id, UserId, ct); if (project is null) return NotFound();
        var job = await projects.FindOwnedJobForUpdateAsync(jobId, UserId, ct);
        if (job is null || job.ProjectId != id) return NotFound();
        job.Cancel(DateTimeOffset.UtcNow); await projects.SaveChangesAsync(ct);
        TempData["Notice"] = "Stopped. Nothing from this attempt will be saved.";
        return RedirectToAction(nameof(Export), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var artifact = await projects.FindOwnedArtifactAsync(id, UserId, ct); if (artifact is null) return NotFound();
        var project = await projects.FindOwnedStudioAsync(artifact.ProjectId, UserId, ct); if (project is null) return NotFound();
        var check = readiness.Check(project);
        if (check.Revision?.Id != artifact.StoryRevisionId || check.Approval?.SnapshotHash != artifact.SnapshotHash) return Conflict("This output is stale because the approved story changed.");
        if (artifact.CreditsHash.Length > 0 && artifact.CreditsHash != check.CreditsHash) return Conflict("This output is stale because the credits changed.");
        if (IsSourceWithdrawn(project, check.Revision)) return Conflict("A source used in this output has had its permission withdrawn.");
        var isCaptions = artifact.Kind is ArtifactKind.Captions or ArtifactKind.TranslatedCaptions;
        return File(await storage.OpenReadAsync(artifact.StorageKey, ct), isCaptions ? "application/x-subrip" : "video/mp4", enableRangeProcessing: true);
    }

    [HttpGet]
    public async Task<IActionResult> PreservationArchive(Guid id, CancellationToken ct)
    {
        var project = await projects.FindOwnedStudioAsync(id, UserId, ct); if (project is null) return NotFound();
        var check = readiness.Check(project); var revision = check.Revision;
        if (revision is null || check.Approval?.SnapshotHash != revision.SnapshotHash) return Conflict("Approve the current story before creating an archive.");
        if (IsSourceWithdrawn(project, revision)) return Conflict("A source used in the approved story has had its permission withdrawn. Remove it from the story before archiving.");
        var artifacts = (await projects.ListOwnedArtifactsAsync(id, UserId, ct))
            .Where(x => x.StoryRevisionId == revision.Id && x.SnapshotHash == revision.SnapshotHash && (x.CreditsHash.Length == 0 || x.CreditsHash == check.CreditsHash))
            .OrderByDescending(x => x.Kind == ArtifactKind.FinalVideo).ThenByDescending(x => x.CreatedAtUtc).ToList();
        var film = artifacts.FirstOrDefault(x => x.Kind is ArtifactKind.FinalVideo or ArtifactKind.PreviewVideo);
        if (film is null) return Conflict("Render the approved story before creating an archive.");
        var path = Path.Combine(Path.GetTempPath(), $"mamn-archive-{Guid.NewGuid():N}.zip");
        await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.Asynchronous))
            await PreservationArchiveBuilder.BuildAsync(output, project, revision, check.Approval, artifacts, storage, ct);
        return File(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose), "application/zip", $"{project.Title}-preservation.zip");
    }

    private static bool IsSourceWithdrawn(Project project, StoryRevision revision)
    {
        var used = revision.Scenes.SelectMany(x => new[] { x.AudioAssetId, x.VisualAssetId }).ToHashSet();
        return project.MediaAssets.Any(x => used.Contains(x.Id) && x.PermissionStatus != PermissionStatus.Granted);
    }

    private static StoryRevision? Current(Project p) => p.StoryRevisions.OrderByDescending(x => x.RevisionNumber).FirstOrDefault();
    private static int NextRevision(Project p) => (p.StoryRevisions.Count == 0 ? 0 : p.StoryRevisions.Max(x => x.RevisionNumber)) + 1;
    private static List<SceneDraft> CurrentDrafts(Project p) => Current(p)?.Scenes.OrderBy(x => x.Order).Select(x => x.ToDraft()).ToList() ?? [];
}
