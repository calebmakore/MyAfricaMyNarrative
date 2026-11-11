using System.IO.Compression;
using System.Text;
using System.Text.Json;
using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;

namespace MyAfricaMyNarrative.Infrastructure.Media;

public static class PreservationArchiveBuilder
{
    public static async Task BuildAsync(Stream output, Project project, StoryRevision revision, ReviewApproval approval,
        IReadOnlyList<ExportArtifact> artifacts, IPrivateMediaStorage storage, CancellationToken cancellationToken)
    {
        var film = artifacts.First(x => x.Kind is ArtifactKind.FinalVideo or ArtifactKind.PreviewVideo);
        var checksums = new List<string>();
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);

        async Task AddStored(string entryName, string key, string expectedSha)
        {
            var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
            await using var target = entry.Open();
            await using var source = await storage.OpenReadAsync(key, cancellationToken);
            await source.CopyToAsync(target, cancellationToken);
            checksums.Add($"{expectedSha}  {entryName}");
        }

        var referencedIds = revision.Scenes.SelectMany(x => x.Pictures.Append(x.AudioAssetId)).Distinct().ToHashSet();
        foreach (var asset in project.MediaAssets.Where(x => referencedIds.Contains(x.Id) && x.PermissionStatus == PermissionStatus.Granted))
            await AddStored($"originals/{asset.Id:N}{Path.GetExtension(asset.OriginalName)}", asset.StorageKey, asset.Sha256);
        await AddStored("film/approved.mp4", film.StorageKey, film.Sha256);
        var captions = artifacts.FirstOrDefault(x => x.Kind == ArtifactKind.Captions);
        if (captions is not null) await AddStored("captions/approved.srt", captions.StorageKey, captions.Sha256);
        var translated = artifacts.FirstOrDefault(x => x.Kind == ArtifactKind.TranslatedCaptions);
        if (translated is not null) await AddStored("captions/translated.srt", translated.StorageKey, translated.Sha256);
        var intent = project.CreatorIntents.SingleOrDefault();
        var credits = CreditsRules.Ordered(project.Credits);
        var creditsHash = credits.Count == 0 ? string.Empty : CreditsRules.ComputeHash(credits);
        var languageReviewed = project.LanguageReviews.Any(x => x.StoryRevisionId == revision.Id && x.SnapshotHash == revision.SnapshotHash);

        var metadata = JsonSerializer.Serialize(new
        {
            SchemaVersion = "1.2",
            StoryType = project.StoryType.ToString(),
            CreatorIdea = project.Idea.Length == 0 ? null : new { Text = project.Idea, Note = "Written by the creator; not generated." },
            Glossary = project.Glossary.Where(x => x.IsApproved).OrderBy(x => x.Term).Select(x => new { x.Term, x.Language, x.Explanation, x.ApprovedAtUtc, Note = x.AiDrafted ? "Explanation drafted by AI, checked and approved by the creator." : "Explanation written and approved by the creator." }),
            ApprovedProductionPlan = project.ProductionPlans.Where(x => x.Status == PlanStatus.Approved).Select(x => new { x.Version, x.Source, x.IsFixture, x.Summary, x.PlannedShootMinutes, x.TimeBudgetMinutes, Steps = x.Steps.OrderBy(s => s.Order).Select(s => new { s.SceneTitle, Approach = s.Approach.ToString(), s.Instruction, s.OnScreenSeconds, s.ShootMinutes, s.Purpose }) }).FirstOrDefault(),
            Project = new { project.Id, project.Title },
            Revision = new { revision.Id, revision.RevisionNumber, revision.SnapshotHash, approval.ApprovedAtUtc },
            LanguageReview = project.LanguageReviews.Where(x => x.StoryRevisionId == revision.Id && x.SnapshotHash == revision.SnapshotHash)
                .Select(x => new { x.ReviewerName, x.Language, x.FluencyBasis, x.IsSelfReview, x.Notes, x.AttestedAtUtc }),
            CreatorDirection = intent is null ? null : new { intent.Audience, intent.Meaning, intent.Tone, intent.LocalContext, intent.ProtectedTerms, intent.TranslationLanguage, Note = "Supplied by the creator; not generated." },
            CreatorAnswers = project.CreatorQuestions.Where(x => x.AnsweredAtUtc is not null).OrderBy(x => x.CreatedAtUtc).Select(x => new { x.Field, x.Prompt, x.Answer, x.AnsweredAtUtc }),
            Scenes = revision.Scenes.OrderBy(x => x.Order).Select(x => new { x.Order, x.Title, x.Text, TextOrigin = x.TextOrigin.ToString(), x.AudioAssetId, x.VisualAssetId, x.MorePictureIds, x.KeepClipSound, x.ClipStartMs, x.TranscriptRevisionId, x.StartMs, x.EndMs, x.Caption,
                Translation = x.Translation.Length == 0 ? null : new { Language = intent?.TranslationLanguage, Text = x.Translation, Status = languageReviewed ? "Reviewed" : "Draft" } }),
            Credits = new { Hash = creditsHash, Approved = creditsHash.Length > 0 && project.CreditsApprovals.Any(x => x.CreditsHash == creditsHash),
                Entries = credits.Select(x => new { x.Order, x.Role, x.Name, x.SourceAssetId }) },
            ProductionPlan = project.Shots.OrderBy(x => x.CreatedAtUtc).Select(x => new { x.SceneTitle, x.Description, Status = x.Status.ToString(), x.CapturedAssetId, x.IsSuggestion,
                Resources = x.ResourceIds.Select(id => project.Resources.FirstOrDefault(r => r.Id == id)).Where(r => r is not null).Select(r => new { Kind = r!.Kind.ToString(), r.Name }) }),
            Sources = project.MediaAssets.Where(x => referencedIds.Contains(x.Id)).Select(x => new { x.Id, x.DisplayName, x.OriginalName, Kind = x.Kind.ToString(), x.Sha256, Permission = x.PermissionStatus.ToString(), x.PermissionDeclaration, x.Attribution, SuppliedBy = x.SuppliedBy.Length == 0 ? null : x.SuppliedBy, ContributorUse = x.ContributorUse.ToString(), ContributorNote = x.ContributorNote.Length == 0 ? null : x.ContributorNote, ContributorUseNote = "Recorded by the project owner; not independently verified." }),
            Transcripts = project.TranscriptRevisions.Where(x => revision.Scenes.Any(s => s.TranscriptRevisionId == x.Id)).Select(x => new { x.Id, x.AssetId, x.Language, x.Text, x.StartMs, x.EndMs, x.ReviewerUserId, x.ReviewedAtUtc })
        }, new JsonSerializerOptions { WriteIndented = true });
        await WriteText(zip, "metadata.json", metadata);
        await WriteText(zip, "README.txt", "My Africa, My Narrative preservation archive\nYour story. Your voice. Our Africa.\n\n" +
            "film/ holds the approved film; captions/ holds the reviewed captions (translated.srt is present only when translations were written).\n" +
            "originals/ holds the permitted source recordings and images exactly as uploaded.\n" +
            "metadata.json explains every scene, its source range, the creator's direction, credits and the production plan.\n" +
            "checksums.sha256 lists a SHA-256 for each stored file.\n\n" +
            "Owner attestation is not independent permission verification. Translations marked Draft have not been reviewed by a fluent speaker.");
        await WriteText(zip, "checksums.sha256", string.Join("\n", checksums) + "\n");
    }

    private static async Task WriteText(ZipArchive zip, string name, string value)
    {
        var entry = zip.CreateEntry(name);
        await using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        await writer.WriteAsync(value);
    }
}
