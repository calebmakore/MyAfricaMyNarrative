using System.Security.Cryptography;
using System.Text;
using MyAfricaMyNarrative.Domain;

namespace MyAfricaMyNarrative.Application;

public sealed class ExportReadinessService : IExportReadinessService
{
    public ExportReadiness Check(Project project)
    {
        var blocking = new List<string>(); var finalOnly = new List<string>();
        var revision = project.StoryRevisions.OrderByDescending(x => x.RevisionNumber).FirstOrDefault();
        var credits = CreditsRules.Ordered(project.Credits);
        var creditsHash = credits.Count == 0 ? string.Empty : CreditsRules.ComputeHash(credits);
        var creditsApproved = creditsHash.Length > 0 && project.CreditsApprovals.Any(x => x.CreditsHash == creditsHash);
        if (revision is null || revision.Scenes.Count == 0)
        { blocking.Add("Add at least one source-backed scene in Shape."); return new(false, false, blocking, revision, null, 0, creditsHash, string.Empty, creditsApproved, blocking.Count); }

        var approval = project.ReviewApprovals.OrderByDescending(x => x.ApprovedAtUtc)
            .FirstOrDefault(x => x.StoryRevisionId == revision.Id && x.SnapshotHash == revision.SnapshotHash);
        if (approval is null) blocking.Add("Approve the current story revision.");
        var languageReview = project.LanguageReviews.Any(x => x.StoryRevisionId == revision.Id && x.SnapshotHash == revision.SnapshotHash);
        if (!languageReview) finalOnly.Add("Record a fluent language review for the current approved revision.");

        var assets = project.MediaAssets.ToDictionary(x => x.Id);
        var transcripts = project.TranscriptRevisions.ToDictionary(x => x.Id);
        var terms = SourceFidelity.ParseProtectedTerms(project.CreatorIntents.SingleOrDefault()?.ProtectedTerms);
        var reported = new HashSet<Guid>();
        foreach (var scene in revision.Scenes.OrderBy(x => x.Order))
        {
            if (!assets.TryGetValue(scene.AudioAssetId, out var audio) || audio.PermissionStatus != PermissionStatus.Granted)
                blocking.Add($"Scene {scene.Order} needs permitted source audio.");
            var pictures = scene.Pictures.Select(id => assets.TryGetValue(id, out var p) && p.PermissionStatus == PermissionStatus.Granted ? p : null).ToList();
            if (pictures.Any(x => x is null))
                blocking.Add($"Scene {scene.Order} needs a permitted visual.");
            foreach (var used in pictures.Prepend(audio).Where(x => x is not null).Distinct())
            {
                if (used!.ContributorUse == ContributorUse.Declined)
                    blocking.Add($"Scene {scene.Order} uses “{used.DisplayName}”, which {used.SuppliedBy} declined. Replace it or remove the scene.");
                else if (used.ContributorUse == ContributorUse.NotYetAsked && reported.Add(used.Id))
                    finalOnly.Add($"Ask {used.SuppliedBy} whether “{used.DisplayName}” may be used, and record their answer in Record.");
            }
            TranscriptRevision? transcript = null;
            if (scene.TranscriptRevisionId is Guid transcriptId && !transcripts.TryGetValue(transcriptId, out transcript))
            { blocking.Add($"Scene {scene.Order} references a missing reviewed transcript."); continue; }
            if (SourceFidelity.ValidateScene(scene.ToDraft(), transcript) is string fidelity)
                blocking.Add($"Scene {scene.Order}: {fidelity}");
            if (transcript is not null && terms.Count > 0)
            {
                var spoken = SourceFidelity.SpokenInScene(scene.ToDraft(), transcript);
                var dropped = SourceFidelity.MissingProtectedTerms(terms, spoken, scene.EffectiveCaption);
                if (dropped.Count > 0) finalOnly.Add($"Scene {scene.Order} caption drops protected expression(s): {string.Join(", ", dropped)}.");
                if (scene.Translation.Length > 0)
                {
                    var droppedInTranslation = SourceFidelity.MissingProtectedTerms(terms, spoken, scene.Translation);
                    if (droppedInTranslation.Count > 0) finalOnly.Add($"Scene {scene.Order} translation drops protected expression(s): {string.Join(", ", droppedInTranslation)}. Keep original-language expressions as spoken.");
                }
            }
            foreach (var shot in project.Shots.Where(x => x.Status == ShotStatus.Planned && x.MatchesScene(scene.Title)))
                finalOnly.Add($"Scene {scene.Order} still has planned footage missing: “{shot.Description}”. Capture it or drop it in Make.");
        }

        if (credits.Count == 0) finalOnly.Add("Add credits for the people and sources in this film, then approve them in Export.");
        else if (!creditsApproved) finalOnly.Add("Approve the current credits. Any credit change needs a fresh approval.");

        var duration = revision.Scenes.Sum(x => x.EndMs - x.StartMs) + (credits.Count > 0 ? CreditsRules.CardDurationMs : 0);
        if (duration is < 60_000 or > 90_000) finalOnly.Add("Final film duration (scenes plus the 5-second credits card) must be 60–90 seconds; a different length can only be a labelled preview.");

        var captions = revision.Scenes.Select(x => x.EffectiveCaption + "\n" + x.Translation).ToList();
        foreach (var entry in project.Glossary.Where(x => !x.IsApproved && captions.Any(c => GlossaryRules.Appears(c, x.Term))))
            finalOnly.Add($"Approve the explanation for “{entry.Term}” in Shape, or remove it.");

        var exportHash = ComputeExportHash(revision.SnapshotHash, creditsHash, GlossaryRules.Hash(project.Glossary));
        var canPreview = blocking.Count == 0;
        return new(canPreview, canPreview && finalOnly.Count == 0, [.. blocking, .. finalOnly], revision, approval, duration, creditsHash, exportHash, creditsApproved, blocking.Count);
    }

    /// <summary>Identifies exactly what a render contains: the story snapshot plus the credits that will be shown.</summary>
    public static string ComputeExportHash(string snapshotHash, string creditsHash, string glossaryHash = "")
    {
        if (creditsHash.Length == 0 && glossaryHash.Length == 0) return snapshotHash;
        var canonical = snapshotHash + (creditsHash.Length > 0 ? "|credits:" + creditsHash : "") + (glossaryHash.Length > 0 ? "|glossary:" + glossaryHash : "");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}
