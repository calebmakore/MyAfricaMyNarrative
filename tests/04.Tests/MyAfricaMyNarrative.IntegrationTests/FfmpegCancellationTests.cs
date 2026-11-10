using MyAfricaMyNarrative.Infrastructure.Media;

namespace MyAfricaMyNarrative.IntegrationTests;

public sealed class FfmpegCancellationTests
{
    [Fact]
    public async Task In_process_cancellation_terminates_ffmpeg()
    {
        var ffmpeg = Environment.GetEnvironmentVariable("MAMN_FFMPEG_PATH");
        if (string.IsNullOrWhiteSpace(ffmpeg) || !File.Exists(ffmpeg)) return;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => BoundedProcessRunner.RunAsync(ffmpeg,
            ["-hide_banner", "-loglevel", "error", "-re", "-f", "lavfi", "-i", "testsrc=size=320x240:rate=30", "-t", "30", "-f", "null", "-"],
            TimeSpan.FromMinutes(1), cancellation.Token));
    }
}
