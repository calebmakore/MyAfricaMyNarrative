using System.Text;
using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;
using MyAfricaMyNarrative.Infrastructure.Media;

namespace MyAfricaMyNarrative.UnitTests;

public sealed class ProductionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private sealed record Fixture(Project Project, MediaAsset Audio, MediaAsset Visual, TranscriptRevision Transcript, Guid Owner);

    /// <summary>A project that is otherwise ready for final export: 60 s of quoted scenes plus the credits card.</summary>
    private static Fixture ReadyProject(string caption = "Mbuya taught me chinyakare stitching", string? protectedTerms = "chinyakare")
    {
        var owner = Guid.NewGuid(); var project = new Project(owner, "Tailoring", Now);
        var audio = new MediaAsset(project.Id, "a", "voice.weba", "Mbuya voice", new string('a', 64), "audio/webm", MediaKind.Audio, 10, "", Now, 120_000);
        var visual = new MediaAsset(project.Id, "v", "home.jpg", "Family home", new string('b', 64), "image/jpeg", MediaKind.Image, 10, "", Now, width: 10, height: 10);
        project.MediaAssets.Add(audio); project.MediaAssets.Add(visual);
        var transcript = new TranscriptRevision(project.Id, audio.Id, "Mbuya taught me chinyakare stitching when I was nine.", "Shona", 0, 60_000, owner, Now);
        project.TranscriptRevisions.Add(transcript);
        project.CreatorIntents.Add(new CreatorIntent(project.Id, "", "", "", "", protectedTerms ?? "", Now));
        var revision = new StoryRevision(project.Id, 1,
            [new SceneDraft("Mbuya", "Mbuya taught me chinyakare stitching", TextOrigin.SourceQuotation, audio.Id, visual.Id, transcript.Id, 0, 57_000, caption)], Now);
        project.StoryRevisions.Add(revision);
        project.ReviewApprovals.Add(new ReviewApproval(project.Id, revision.Id, revision.SnapshotHash, owner, Now));
        project.LanguageReviews.Add(new LanguageReview(project.Id, revision.Id, revision.SnapshotHash, "Rudo Moyo", "Shona", "Home-language speaker", false, "", owner, Now));
        project.Credits.Add(new CreditEntry(project.Id, "Storyteller", "Rudo Moyo", audio.Id, 1, Now));
        project.CreditsApprovals.Add(new CreditsApproval(project.Id, CreditsRules.ComputeHash(project.Credits), owner, Now));
        return new(project, audio, visual, transcript, owner);
    }

    [Fact]
    public void Ready_project_can_export_final_including_credits_card_duration()
    {
        var check = new ExportReadinessService().Check(ReadyProject().Project);
        Assert.Empty(check.Issues);
        Assert.True(check.CanExportFinal);
        Assert.Equal(62_000, check.DurationMs);
        Assert.NotEqual(check.Revision!.SnapshotHash, check.ExportHash);
    }

    [Fact]
    public void Quotation_must_link_reviewed_transcript_of_same_audio_inside_its_range_with_matching_words()
    {
        var f = ReadyProject();
        SceneDraft Draft(string text, Guid? transcriptId, long start = 0, long end = 5000, Guid? audio = null) =>
            new("Scene", text, TextOrigin.SourceQuotation, audio ?? f.Audio.Id, f.Visual.Id, transcriptId, start, end, "");

        Assert.Null(SourceFidelity.ValidateScene(Draft("taught me  chinyakare\nstitching", f.Transcript.Id), f.Transcript));
        Assert.NotNull(SourceFidelity.ValidateScene(Draft("Mbuya taught me", null), null));
        Assert.NotNull(SourceFidelity.ValidateScene(Draft("Mbuya taught me modern stitching", f.Transcript.Id), f.Transcript));
        Assert.NotNull(SourceFidelity.ValidateScene(Draft("Mbuya taught me", f.Transcript.Id, 59_000, 61_000), f.Transcript));
        Assert.NotNull(SourceFidelity.ValidateScene(Draft("Mbuya taught me", f.Transcript.Id, audio: Guid.NewGuid()), f.Transcript));
        Assert.Null(SourceFidelity.ValidateScene(Draft("", f.Transcript.Id), f.Transcript));
        var creatorWritten = new SceneDraft("Scene", "Any words I chose", TextOrigin.CreatorWritten, f.Audio.Id, f.Visual.Id, null, 0, 5000, "");
        Assert.Null(SourceFidelity.ValidateScene(creatorWritten, null));
    }

    [Fact]
    public void Unlinked_quotation_blocks_even_a_preview()
    {
        var f = ReadyProject();
        var revision = new StoryRevision(f.Project.Id, 2,
            [new SceneDraft("Mbuya", "Words never reviewed", TextOrigin.SourceQuotation, f.Audio.Id, f.Visual.Id, null, 0, 57_000, "Words")], Now);
        f.Project.StoryRevisions.Add(revision);
        f.Project.ReviewApprovals.Add(new ReviewApproval(f.Project.Id, revision.Id, revision.SnapshotHash, f.Owner, Now));
        var check = new ExportReadinessService().Check(f.Project);
        Assert.False(check.CanPreview);
        Assert.Contains(check.Issues, x => x.Contains("must link to saved words", StringComparison.Ordinal));
    }

    [Fact]
    public void Caption_that_drops_a_spoken_protected_expression_blocks_final_but_not_preview()
    {
        var check = new ExportReadinessService().Check(ReadyProject(caption: "Grandmother taught me traditional stitching").Project);
        Assert.True(check.CanPreview);
        Assert.False(check.CanExportFinal);
        Assert.Contains(check.Issues, x => x.Contains("chinyakare", StringComparison.Ordinal));
    }

    [Fact]
    public void Protected_terms_parse_common_separators_and_ignore_case_when_checking()
    {
        var terms = SourceFidelity.ParseProtectedTerms("chinyakare, mbuya\n“Dzimba”; ·x");
        Assert.Equal(["chinyakare", "mbuya", "Dzimba"], terms);
        Assert.Empty(SourceFidelity.MissingProtectedTerms(terms, "Mbuya said chinyakare", "MBUYA: Chinyakare"));
        Assert.Equal(["Dzimba"], SourceFidelity.MissingProtectedTerms(terms, "dzimba yedu", "our home"));
    }

    [Fact]
    public void Credits_need_approval_of_the_exact_current_set()
    {
        var f = ReadyProject();
        f.Project.Credits.Add(new CreditEntry(f.Project.Id, "Photographs", "Family archive", f.Visual.Id, 2, Now));
        var check = new ExportReadinessService().Check(f.Project);
        Assert.False(check.CreditsApproved);
        Assert.False(check.CanExportFinal);
        Assert.True(check.CanPreview);
        f.Project.CreditsApprovals.Add(new CreditsApproval(f.Project.Id, CreditsRules.ComputeHash(f.Project.Credits), f.Owner, Now));
        Assert.True(new ExportReadinessService().Check(f.Project).CanExportFinal);
    }

    [Fact]
    public void Credits_hash_changes_with_wording_and_order_but_not_with_insertion_sequence()
    {
        var project = Guid.NewGuid();
        var a = new CreditEntry(project, "Storyteller", "Rudo", null, 1, Now);
        var b = new CreditEntry(project, "Camera", "Tendai", null, 2, Now);
        Assert.Equal(CreditsRules.ComputeHash([a, b]), CreditsRules.ComputeHash([b, a]));
        var before = CreditsRules.ComputeHash([a, b]);
        b.Update("Camera", "Tendai M.", null, 2);
        Assert.NotEqual(before, CreditsRules.ComputeHash([a, b]));
    }

    [Fact]
    public void Planned_shot_blocks_final_until_captured_or_dropped()
    {
        var f = ReadyProject();
        var shot = new ShotPlanItem(f.Project.Id, "mbuya", "Close-up of the sewing machine", [], false, Now);
        f.Project.Shots.Add(shot);
        Assert.False(new ExportReadinessService().Check(f.Project).CanExportFinal);
        shot.MarkCaptured(f.Visual.Id, Now);
        Assert.True(new ExportReadinessService().Check(f.Project).CanExportFinal);
        shot.Reopen(Now);
        Assert.False(new ExportReadinessService().Check(f.Project).CanExportFinal);
        shot.Drop(Now);
        Assert.True(new ExportReadinessService().Check(f.Project).CanExportFinal);
    }

    [Fact]
    public void Shot_keeps_only_declared_resource_ids()
    {
        var keep = Guid.NewGuid();
        var shot = new ShotPlanItem(Guid.NewGuid(), "Scene", "Wide shot", [keep, Guid.Empty, keep], true, Now);
        Assert.Equal([keep], shot.ResourceIds);
        Assert.True(shot.IsSuggestion);
        Assert.Equal(ShotStatus.Planned, shot.Status);
    }

    [Fact]
    public void Translation_changes_snapshot_so_earlier_language_review_no_longer_counts()
    {
        var f = ReadyProject();
        var current = f.Project.StoryRevisions.Single();
        var draft = current.Scenes.Single().ToDraft() with { Translation = "Grandmother taught me chinyakare stitching" };
        var translated = new StoryRevision(f.Project.Id, 2, [draft], Now);
        Assert.NotEqual(current.SnapshotHash, translated.SnapshotHash);
        f.Project.StoryRevisions.Add(translated);
        f.Project.ReviewApprovals.Add(new ReviewApproval(f.Project.Id, translated.Id, translated.SnapshotHash, f.Owner, Now));
        var check = new ExportReadinessService().Check(f.Project);
        Assert.False(check.CanExportFinal);
        Assert.Contains(check.Issues, x => x.Contains("language review", StringComparison.Ordinal));
    }

    [Fact]
    public void Translation_that_drops_a_protected_expression_is_flagged()
    {
        var f = ReadyProject();
        var draft = f.Project.StoryRevisions.Single().Scenes.Single().ToDraft() with { Translation = "Grandmother taught me traditional stitching" };
        var revision = new StoryRevision(f.Project.Id, 2, [draft], Now);
        f.Project.StoryRevisions.Add(revision);
        Assert.Contains(new ExportReadinessService().Check(f.Project).Issues, x => x.Contains("translation drops", StringComparison.Ordinal));
    }

    [Fact]
    public void Snapshot_hash_without_translation_is_unchanged_from_earlier_format()
    {
        var audio = Guid.NewGuid(); var visual = Guid.NewGuid(); var project = Guid.NewGuid();
        var withEmpty = new StoryRevision(project, 1, [new SceneDraft("A", "B", TextOrigin.CreatorWritten, audio, visual, null, 0, 1000, "C", "")], Now);
        var withoutArgument = new StoryRevision(project, 1, [new SceneDraft("A", "B", TextOrigin.CreatorWritten, audio, visual, null, 0, 1000, "C")], Now);
        Assert.Equal(withoutArgument.SnapshotHash, withEmpty.SnapshotHash);
    }

    [Fact]
    public void Srt_cues_are_bounded_ordered_and_cannot_be_split_by_blank_lines()
    {
        var srt = new StringBuilder();
        RenderText.AppendCue(srt, 1, 0, 19_000, "It's [Mbuya's] story: 100%\n\n2\n00:00:00,000 --> 00:00:01,000");
        RenderText.AppendCue(srt, 2, 19_000, 37_000, "{\\an8} second");
        var text = srt.ToString();
        Assert.StartsWith("1\n00:00:00,000 --> 00:00:19,000\nIt's [Mbuya's] story: 100%\n2\n", text);
        Assert.Equal(2, text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Contains("00:00:19,000 --> 00:00:37,000", text);
    }

    [Fact]
    public void Caption_wrapping_keeps_every_word_and_respects_line_width()
    {
        var caption = "Mbuya taught me chinyakare stitching when I was nine years old in Mutare";
        var wrapped = RenderText.Wrap(caption, 20);
        Assert.All(wrapped.Split('\n'), line => Assert.True(line.Length <= 20));
        Assert.Equal(caption, wrapped.Replace('\n', ' '));
    }

    [Fact]
    public void Credits_card_lists_roles_names_and_tagline()
    {
        var project = Guid.NewGuid();
        var card = RenderText.CreditsCard([new CreditEntry(project, "Storyteller", "Rudo Moyo", null, 1, Now)]);
        Assert.Contains("Storyteller: Rudo Moyo", card);
        Assert.EndsWith(RenderText.Tagline, card);
    }
}

public sealed class StudioProgressTests
{
    [Fact]
    public void New_project_points_to_recording_a_voice_first()
    {
        var project = new Project(Guid.NewGuid(), "New", DateTimeOffset.UtcNow);
        var progress = StudioProgressCalculator.Calculate(project, new ExportReadinessService().Check(project), []);
        Assert.Equal(StudioStage.Record, progress.Next!.Stage);
        Assert.Equal("Add your voice", progress.Next.Title);
        Assert.Equal(0, progress.Percent);
    }

    [Fact]
    public void Next_step_follows_real_state_and_skips_completed_items()
    {
        var owner = Guid.NewGuid(); var now = DateTimeOffset.UtcNow; var project = new Project(owner, "Story", now);
        project.MediaAssets.Add(new MediaAsset(project.Id, "a", "a.wav", "Voice", new string('a', 64), "audio/wav", MediaKind.Audio, 1, "", now, 10_000));
        project.MediaAssets.Add(new MediaAsset(project.Id, "b", "b.png", "Photo", new string('b', 64), "image/png", MediaKind.Image, 1, "", now));
        var progress = StudioProgressCalculator.Calculate(project, new ExportReadinessService().Check(project), []);
        Assert.True(progress.IsStageComplete(StudioStage.Record));
        Assert.Equal(StudioStage.Shape, progress.Next!.Stage);
        Assert.Equal("intention", progress.Next.Anchor);
    }
}
