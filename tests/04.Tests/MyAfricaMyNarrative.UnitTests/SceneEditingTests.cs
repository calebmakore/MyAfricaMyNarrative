using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;
using MyAfricaMyNarrative.Infrastructure.Media;

namespace MyAfricaMyNarrative.UnitTests;

/// <summary>Several pictures under one voice, a video's own sound kept underneath, and slides — without disturbing existing films.</summary>
public sealed class SceneEditingTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly Guid Voice = Guid.NewGuid(), A = Guid.NewGuid(), B = Guid.NewGuid(), C = Guid.NewGuid();
    private static SceneDraft Draft(IReadOnlyList<Guid>? more = null, bool keep = false, long clip = 0, long end = 12_000) =>
        new("Scene", "Words", TextOrigin.CreatorWritten, Voice, A, null, 0, end, "", "", more, keep, clip);

    [Fact]
    public void Existing_scenes_keep_their_snapshot_fingerprint_and_new_options_change_it()
    {
        var project = Guid.NewGuid();
        var plain = new StoryRevision(project, 1, [new SceneDraft("Scene", "Words", TextOrigin.CreatorWritten, Voice, A, null, 0, 12_000, "")], Now);
        var defaults = new StoryRevision(project, 1, [Draft()], Now);
        Assert.Equal(plain.SnapshotHash, defaults.SnapshotHash);   // approved films stay approved
        Assert.NotEqual(plain.SnapshotHash, new StoryRevision(project, 1, [Draft([B])], Now).SnapshotHash);
        Assert.NotEqual(plain.SnapshotHash, new StoryRevision(project, 1, [Draft(keep: true)], Now).SnapshotHash);
        Assert.NotEqual(plain.SnapshotHash, new StoryRevision(project, 1, [Draft(clip: 2_500)], Now).SnapshotHash);
    }

    [Fact]
    public void Extra_pictures_round_trip_in_order_and_are_bounded()
    {
        var scene = new StoryRevision(Guid.NewGuid(), 1, [Draft([B, C], keep: true, clip: 1_500)], Now).Scenes.Single();
        Assert.Equal([A, B, C], scene.Pictures);
        Assert.Equal(Draft([B, C], keep: true, clip: 1_500).Pictures, scene.ToDraft().Pictures);
        Assert.True(scene.KeepClipSound); Assert.Equal(1_500, scene.ClipStartMs);

        Assert.Throws<ArgumentException>(() => new StoryRevision(Guid.NewGuid(), 1, [Draft(Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToList())], Now));   // 7 pictures
        Assert.Throws<ArgumentException>(() => new StoryRevision(Guid.NewGuid(), 1, [Draft([B, B])], Now));                  // repeated picture
        Assert.Throws<ArgumentException>(() => new StoryRevision(Guid.NewGuid(), 1, [Draft([A])], Now));                     // main picture repeated
        Assert.Throws<ArgumentException>(() => new StoryRevision(Guid.NewGuid(), 1, [Draft([B, C], end: 2_500)], Now));     // under 1 s each
    }

    [Fact]
    public void Composite_render_splits_time_keeps_the_voice_and_mixes_clip_sound_quietly()
    {
        var args = RenderText.SceneCompositeArgs([new("clip.webm", false, 2_000), new("photo.png", true, 0), new("art.png", true, 0)],
            "voice.wav", 10_000, 20_000, 1080, 1920, "caption.txt", "font.ttf", "out.mp4", keepFirstPictureSound: true);
        var joined = string.Join(' ', args);
        Assert.Equal(4, args.Count(x => x == "-i"));
        Assert.Contains("-stream_loop -1 -t 5.333 -i clip.webm", joined);          // decoded up to clip start + share...
        Assert.Contains("[0:v]trim=start=2:duration=3.333", joined);              // ...and cut from the clip start by timestamp
        Assert.Contains("-loop 1 -t 3.334 -i art.png", joined);                    // last picture takes the remainder
        Assert.Contains("-t 20 -i voice.wav", joined);
        Assert.Contains("atrim=start=10:duration=10", joined);
        Assert.Contains("concat=n=3:v=1:a=0", joined);
        Assert.Contains("volume=0.25", joined); Assert.Contains("amix=inputs=2:duration=first", joined);
        Assert.Contains("[aout]", args);

        var silent = string.Join(' ', RenderText.SceneCompositeArgs([new("a.png", true, 0), new("b.png", true, 0)], "voice.wav", 0, 8_000, 540, 960, null, "font.ttf", "o.mp4"));
        Assert.DoesNotContain("amix", silent);
        Assert.Contains("-map [voice]", silent);
    }

    [Fact]
    public void A_withdrawn_extra_picture_blocks_export_like_the_main_picture()
    {
        var owner = Guid.NewGuid(); var project = new Project(owner, "Story", Now);
        var voice = new MediaAsset(project.Id, "v", "v.wav", "Voice", new string('a', 64), "audio/wav", MediaKind.Audio, 10, "", Now, 20_000);
        var main = new MediaAsset(project.Id, "m", "m.png", "Main", new string('b', 64), "image/png", MediaKind.Image, 10, "", Now, width: 10, height: 10);
        var extra = new MediaAsset(project.Id, "e", "e.png", "Extra", new string('c', 64), "image/png", MediaKind.Image, 10, "", Now, width: 10, height: 10);
        foreach (var a in new[] { voice, main, extra }) project.MediaAssets.Add(a);
        var revision = new StoryRevision(project.Id, 1, [new SceneDraft("S", "Mine", TextOrigin.CreatorWritten, voice.Id, main.Id, null, 0, 10_000, "", "", [extra.Id])], Now);
        project.StoryRevisions.Add(revision);
        project.ReviewApprovals.Add(new ReviewApproval(project.Id, revision.Id, revision.SnapshotHash, owner, Now));
        Assert.True(new ExportReadinessService().Check(project).CanPreview);

        extra.Withdraw(Now);
        var check = new ExportReadinessService().Check(project);
        Assert.False(check.CanPreview);
        Assert.Contains("Scene 1 needs a permitted visual.", check.Issues);
    }

    [Fact]
    public void Slides_use_the_chosen_colours_and_draw_text_from_files_only()
    {
        var args = FfmpegSlideRenderer.Args("title.txt", "subtitle.txt", "font.ttf", SlideTheme.Terracotta, "slide.png", 1);
        var joined = string.Join(' ', args);
        Assert.Contains("color=c=0xB8472A:s=1080x1920", joined);
        Assert.Contains("textfile=title.txt:expansion=none", joined); Assert.Contains("textfile=subtitle.txt", joined);
        Assert.DoesNotContain("text='", joined);                                     // creator words never reach the command line
        Assert.DoesNotContain("subtitle.txt", string.Join(' ', FfmpegSlideRenderer.Args("title.txt", null, "font.ttf", SlideTheme.Night, "slide.png", 1)));
    }
}
