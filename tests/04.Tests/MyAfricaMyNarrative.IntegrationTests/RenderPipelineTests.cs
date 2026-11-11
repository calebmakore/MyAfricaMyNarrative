using System.Text;
using Microsoft.Extensions.Options;
using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;
using MyAfricaMyNarrative.Infrastructure.Media;

namespace MyAfricaMyNarrative.IntegrationTests;

/// <summary>
/// Runs the renderer's real FFmpeg argument builders against installed FFmpeg/ffprobe with a labelled synthetic fixture:
/// burned-in captions containing characters that must stay literal, a credits card, concat and stream verification.
/// Set MAMN_FFMPEG_PATH and MAMN_FFPROBE_PATH to run it; otherwise it is skipped by returning early.
/// </summary>
public sealed class RenderPipelineTests
{
    [Fact]
    public async Task Burned_captions_and_credits_card_render_a_verified_portrait_film()
    {
        var ffmpeg = Environment.GetEnvironmentVariable("MAMN_FFMPEG_PATH");
        var ffprobe = Environment.GetEnvironmentVariable("MAMN_FFPROBE_PATH");
        if (string.IsNullOrWhiteSpace(ffmpeg) || !File.Exists(ffmpeg) || string.IsNullOrWhiteSpace(ffprobe) || !File.Exists(ffprobe)) return;
        var font = Path.Combine(AppContext.BaseDirectory, "Fonts", "Jost-SemiBold.ttf");
        Assert.True(File.Exists(font), "The bundled caption font must be copied beside the application.");

        var temp = Directory.CreateTempSubdirectory("mamn-render-test-").FullName;
        try
        {
            var timeout = TimeSpan.FromMinutes(2); var ct = CancellationToken.None;
            File.Copy(font, Path.Combine(temp, "caption-font.ttf"));
            // Synthetic, visibly labelled fixture media.
            await BoundedProcessRunner.RunAsync(ffmpeg, ["-y", "-f", "lavfi", "-i", "testsrc=size=640x480", "-frames:v", "1", "visual-1.source.png"], timeout, ct, temp);
            File.Move(Path.Combine(temp, "visual-1.source.png"), Path.Combine(temp, "visual-1.source"));
            await BoundedProcessRunner.RunAsync(ffmpeg, ["-y", "-f", "lavfi", "-i", "sine=frequency=330:duration=8", "-ac", "1", "-ar", "44100", "audio-1.source.wav"], timeout, ct, temp);
            File.Move(Path.Combine(temp, "audio-1.source.wav"), Path.Combine(temp, "audio-1.source"));

            const string hostile = "SYNTHETIC FIXTURE: It's [Mbuya's] story — 100% {\\an8} %{pts} ; 'quoted' \\ chinyakare";
            await File.WriteAllTextAsync(Path.Combine(temp, "caption-001.txt"), RenderText.Wrap(hostile), new UTF8Encoding(false), ct);
            await BoundedProcessRunner.RunAsync(ffmpeg, RenderText.SceneSegmentArgs("visual-1.source", true, "audio-1.source", 1000, 4000, 540, 960, "caption-001.txt", "caption-font.ttf", "segment-001.mp4"), timeout, ct, temp);
            await BoundedProcessRunner.RunAsync(ffmpeg, RenderText.SceneSegmentArgs("visual-1.source", true, "audio-1.source", 4000, 6500, 540, 960, null, "caption-font.ttf", "segment-002.mp4"), timeout, ct, temp);

            var credits = new[] { new CreditEntry(Guid.NewGuid(), "Storyteller", "Synthetic Fixture", null, 1, DateTimeOffset.UtcNow) };
            await File.WriteAllTextAsync(Path.Combine(temp, "credits.txt"), RenderText.CreditsCard(credits), new UTF8Encoding(false), ct);
            await BoundedProcessRunner.RunAsync(ffmpeg, RenderText.CreditsCardArgs(credits.Length, 540, 960, "credits.txt", "caption-font.ttf", "segment-credits.mp4"), timeout, ct, temp);

            await File.WriteAllLinesAsync(Path.Combine(temp, "segments.txt"), ["file 'segment-001.mp4'", "file 'segment-002.mp4'", "file 'segment-credits.mp4'"], ct);
            await BoundedProcessRunner.RunAsync(ffmpeg, RenderText.ConcatArgs("segments.txt", "preview.mp4"), timeout, ct, temp);

            var probe = new FfprobeMediaProbe(Options.Create(new MediaOptions { FfprobePath = ffprobe }));
            await using var film = File.OpenRead(Path.Combine(temp, "preview.mp4"));
            var actual = await probe.ProbeAsync(film, ct);
            Assert.True(actual.HasVideo); Assert.True(actual.HasAudio);
            Assert.Equal(540, actual.Width); Assert.Equal(960, actual.Height);
            Assert.InRange(actual.DurationMs, 10_500 - 1000, 10_500 + 1000);
        }
        finally { try { Directory.Delete(temp, true); } catch { } }
    }
}
