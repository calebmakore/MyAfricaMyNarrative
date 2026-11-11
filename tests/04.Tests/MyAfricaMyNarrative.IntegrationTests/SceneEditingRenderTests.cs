using System.Text;
using Microsoft.Extensions.Options;
using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Infrastructure.Media;

namespace MyAfricaMyNarrative.IntegrationTests;

/// <summary>
/// Real FFmpeg: a scene with a video clip and two photos under one voice (clip sound kept quietly), joined to an
/// ordinary scene, plus a generated title slide. Synthetic, labelled fixture media only.
/// Set MAMN_FFMPEG_PATH and MAMN_FFPROBE_PATH to run.
/// </summary>
public sealed class SceneEditingRenderTests
{
    private static (string Ffmpeg, string Ffprobe)? Tools()
    {
        var ffmpeg = Environment.GetEnvironmentVariable("MAMN_FFMPEG_PATH"); var ffprobe = Environment.GetEnvironmentVariable("MAMN_FFPROBE_PATH");
        return string.IsNullOrWhiteSpace(ffmpeg) || !File.Exists(ffmpeg) || string.IsNullOrWhiteSpace(ffprobe) || !File.Exists(ffprobe) ? null : (ffmpeg, ffprobe);
    }

    [Fact]
    public async Task Pictures_share_one_voice_keep_clip_sound_and_join_ordinary_scenes()
    {
        if (Tools() is not var (ffmpeg, ffprobe)) return;
        var temp = Directory.CreateTempSubdirectory("mamn-edit-test-").FullName;
        try
        {
            var t = TimeSpan.FromMinutes(2); var ct = CancellationToken.None;
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Fonts", "Jost-SemiBold.ttf"), Path.Combine(temp, "font.ttf"));
            await BoundedProcessRunner.RunAsync(ffmpeg, ["-y", "-f", "lavfi", "-i", "testsrc=size=720x1280:rate=30:duration=8", "-f", "lavfi", "-i", "sine=frequency=880:duration=8",
                "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", "-shortest", "-output_ts_offset", "1.4", "clip.mp4"], t, ct, temp);   // like a browser recording, timestamps do not start at zero
            await BoundedProcessRunner.RunAsync(ffmpeg, ["-y", "-f", "lavfi", "-i", "testsrc2=size=640x480", "-frames:v", "1", "photo-a.png"], t, ct, temp);
            await BoundedProcessRunner.RunAsync(ffmpeg, ["-y", "-f", "lavfi", "-i", "smptebars=size=640x480", "-frames:v", "1", "photo-b.png"], t, ct, temp);
            await BoundedProcessRunner.RunAsync(ffmpeg, ["-y", "-f", "lavfi", "-i", "sine=frequency=220:duration=12", "-ac", "1", "voice.wav"], t, ct, temp);
            await File.WriteAllTextAsync(Path.Combine(temp, "caption.txt"), RenderText.Wrap("SYNTHETIC FIXTURE: one voice, three pictures"), new UTF8Encoding(false), ct);

            List<RenderText.ScenePicture> pictures = [new("clip.mp4", false, 1_500), new("photo-a.png", true, 0), new("photo-b.png", true, 0)];
            await BoundedProcessRunner.RunAsync(ffmpeg, RenderText.SceneCompositeArgs(pictures, "voice.wav", 2_000, 8_000, 540, 960, "caption.txt", "font.ttf", "mixed.mp4", keepFirstPictureSound: true), t, ct, temp);
            await BoundedProcessRunner.RunAsync(ffmpeg, RenderText.SceneCompositeArgs(pictures, "voice.wav", 2_000, 8_000, 540, 960, "caption.txt", "font.ttf", "voice-only.mp4"), t, ct, temp);
            await BoundedProcessRunner.RunAsync(ffmpeg, RenderText.SceneSegmentArgs("photo-a.png", true, "voice.wav", 8_000, 11_000, 540, 960, null, "font.ttf", "plain.mp4"), t, ct, temp);
            await File.WriteAllLinesAsync(Path.Combine(temp, "list.txt"), ["file 'mixed.mp4'", "file 'plain.mp4'"], ct);
            await BoundedProcessRunner.RunAsync(ffmpeg, RenderText.ConcatArgs("list.txt", "film.mp4"), t, ct, temp);

            var probe = new FfprobeMediaProbe(Options.Create(new MediaOptions { FfprobePath = ffprobe }));
            async Task<ProbedMedia> Probe(string name) { await using var s = File.OpenRead(Path.Combine(temp, name)); return await probe.ProbeAsync(s, ct); }
            var mixed = await Probe("mixed.mp4");
            Assert.True(mixed.HasVideo, "no video"); Assert.True(mixed.HasAudio, "no audio"); Assert.Equal((540, 960), (mixed.Width, mixed.Height));
            Assert.InRange(mixed.DurationMs, 5_500, 6_500);                     // the scene length, not the clip's
            var (videoSeconds, audioSeconds) = await StreamSeconds(ffprobe, Path.Combine(temp, "mixed.mp4"));
            Assert.True(Math.Abs(videoSeconds - audioSeconds) < 0.15, $"picture track {videoSeconds:0.00} s vs voice {audioSeconds:0.00} s");   // no black tail
            var film = await Probe("film.mp4");
            Assert.InRange(film.DurationMs, 8_500, 9_500);                      // composite joins an ordinary scene cleanly

            // Keeping the clip's sound adds energy to the track compared with the voice alone.
            // The clip's 880 Hz tone is heard under the voice (220 Hz) while the clip is on screen, and only when it is kept.
            var withClip = await MeanVolume(ffmpeg, temp, "mixed.mp4", "atrim=0:1.8,bandpass=f=880:width_type=h:w=80");
            var voiceOnly = await MeanVolume(ffmpeg, temp, "voice-only.mp4", "atrim=0:1.8,bandpass=f=880:width_type=h:w=80");
            Assert.True(withClip > voiceOnly + 10, $"880 Hz level with clip sound {withClip} dB vs voice only {voiceOnly} dB");
        }
        finally { try { Directory.Delete(temp, true); } catch { } }
    }

    [Fact]
    public async Task A_real_browser_recording_keeps_picture_and_voice_the_same_length_when_cut()
    {
        if (Tools() is not var (ffmpeg, ffprobe)) return;
        var temp = Directory.CreateTempSubdirectory("mamn-webm-test-").FullName;
        try
        {
            var t = TimeSpan.FromMinutes(2); var ct = CancellationToken.None;
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "SYNTHETIC-edge-test-camera-recording.webm"), Path.Combine(temp, "rec.webm"));
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Fonts", "Jost-SemiBold.ttf"), Path.Combine(temp, "font.ttf"));
            await BoundedProcessRunner.RunAsync(ffmpeg, ["-y", "-f", "lavfi", "-i", "smptebars=size=640x480", "-frames:v", "1", "still.png"], t, ct, temp);

            // The recording is both the picture and the voice (a talking-head scene), cut from 1.5 s to 6 s.
            await BoundedProcessRunner.RunAsync(ffmpeg, RenderText.SceneSegmentArgs("rec.webm", false, "rec.webm", 1_500, 6_000, 540, 960, null, "font.ttf", "synced.mp4", visualCarriesTheSound: true), t, ct, temp);
            var (sv, sa) = await StreamSeconds(ffprobe, Path.Combine(temp, "synced.mp4"));
            Assert.True(Math.Abs(sv - 4.5) < 0.1 && Math.Abs(sa - 4.5) < 0.1, $"synced scene: picture {sv:0.00} s, voice {sa:0.00} s (expected 4.5)");

            // The recording as the second of two pictures under its own voice.
            await BoundedProcessRunner.RunAsync(ffmpeg, RenderText.SceneCompositeArgs([new("still.png", true, 0), new("rec.webm", false, 0)], "rec.webm", 500, 6_000, 540, 960, null, "font.ttf", "pair.mp4"), t, ct, temp);
            var (pv, pa) = await StreamSeconds(ffprobe, Path.Combine(temp, "pair.mp4"));
            Assert.True(Math.Abs(pv - 5.5) < 0.1 && Math.Abs(pa - 5.5) < 0.1, $"two pictures: picture {pv:0.00} s, voice {pa:0.00} s (expected 5.5)");
        }
        finally { try { Directory.Delete(temp, true); } catch { } }
    }

    [Fact]
    public async Task A_slide_is_a_vertical_picture_made_from_the_creators_words()
    {
        if (Tools() is not var (ffmpeg, ffprobe)) return;
        var work = Path.Combine(Path.GetTempPath(), "mamn-slide-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var renderer = new FfmpegSlideRenderer(Options.Create(new MediaOptions { FfmpegPath = ffmpeg }));
            var png = await renderer.RenderAsync("SYNTHETIC FIXTURE: It's 100% {ours} — chinyakare", "A line: with 'quotes' and %{pts}", SlideTheme.Terracotta, work, CancellationToken.None);
            var probe = new FfprobeMediaProbe(Options.Create(new MediaOptions { FfprobePath = ffprobe }));
            await using var s = File.OpenRead(png);
            var media = await probe.ProbeAsync(s, CancellationToken.None);
            Assert.Equal((1080, 1920), (media.Width, media.Height));
            await Assert.ThrowsAsync<ArgumentException>(() => renderer.RenderAsync("", "", SlideTheme.Night, work, CancellationToken.None));
        }
        finally { try { Directory.Delete(work, true); } catch { } }
    }

    private static async Task<(double Video, double Audio)> StreamSeconds(string ffprobe, string file)
    {
        var start = new System.Diagnostics.ProcessStartInfo(ffprobe) { RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var a in new[] { "-v", "error", "-show_entries", "stream=codec_type,duration", "-of", "csv=p=0", file }) start.ArgumentList.Add(a);
        using var p = System.Diagnostics.Process.Start(start)!;
        var lines = (await p.StandardOutput.ReadToEndAsync()).Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries); await p.WaitForExitAsync();
        double Of(string kind) => double.Parse(lines.First(l => l.StartsWith(kind)).Split(',')[1], System.Globalization.CultureInfo.InvariantCulture);
        return (Of("video"), Of("audio"));
    }

    private static async Task<double> MeanVolume(string ffmpeg, string folder, string file, string filter)
    {
        var start = new System.Diagnostics.ProcessStartInfo(ffmpeg) { RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = folder };
        foreach (var a in new[] { "-hide_banner", "-i", file, "-af", filter + ",volumedetect", "-f", "null", "-" }) start.ArgumentList.Add(a);
        using var p = System.Diagnostics.Process.Start(start)!;
        var err = await p.StandardError.ReadToEndAsync(); await p.WaitForExitAsync();
        var m = System.Text.RegularExpressions.Regex.Match(err, @"mean_volume: (-?[0-9.]+) dB");
        Assert.True(m.Success, "volumedetect produced no reading");
        return double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }
}
