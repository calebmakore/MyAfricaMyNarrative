using MyAfricaMyNarrative.Domain;
using MyAfricaMyNarrative.Infrastructure.Media;

namespace MyAfricaMyNarrative.UnitTests;

/// <summary>A recorded video can be a scene's voice as well as its picture, and the film keeps the two in sync.</summary>
public sealed class VideoRecordingTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static MediaAsset Asset(MediaKind kind, bool hasSound) =>
        new(Guid.NewGuid(), "k", "f", "Source", new string('a', 64), "video/webm", kind, 10, "", Now, 30_000, 720, 1280, hasSound);

    [Fact]
    public void Only_audio_and_video_recorded_with_sound_can_be_a_scenes_voice()
    {
        Assert.True(Asset(MediaKind.Audio, hasSound: false).CanProvideSound);   // audio always has sound
        Assert.True(Asset(MediaKind.Video, hasSound: true).CanProvideSound);
        Assert.False(Asset(MediaKind.Video, hasSound: false).CanProvideSound);   // a silent clip is picture only
        Assert.False(Asset(MediaKind.Image, hasSound: true).CanProvideSound);    // an image never has sound
    }

    [Fact]
    public void A_scene_whose_picture_and_voice_share_one_video_cuts_both_from_the_same_moment()
    {
        var synced = RenderText.SceneSegmentArgs("clip.webm", false, "clip.webm", 12_500, 20_000, 1080, 1920, null, "font.ttf", "out.mp4", visualCarriesTheSound: true);
        Assert.Equal(["-y", "-t", "20", "-i", "clip.webm"], synced.Take(5));
        Assert.Single(synced, x => x == "-i");                                  // one input: picture and sound together
        var graph = synced[synced.IndexOf("-filter_complex") + 1];
        Assert.Contains("[0:v]trim=start=12.5:duration=7.5", graph);           // both cut at the same timestamps...
        Assert.Contains("[0:a]atrim=start=12.5:duration=7.5", graph);          // ...so lips and words stay in sync
        Assert.DoesNotContain("-ss", synced);                                   // no input seek: WebM recordings have no index
        Assert.DoesNotContain("-stream_loop", synced);

        // A separate picture still loops under a voice cut from its own recording.
        var separate = RenderText.SceneSegmentArgs("clip.webm", false, "voice.wav", 12_500, 20_000, 1080, 1920, null, "font.ttf", "out.mp4");
        Assert.Equal(2, separate.Count(x => x == "-i"));
        Assert.Contains("1:a:0", separate);
    }
}
