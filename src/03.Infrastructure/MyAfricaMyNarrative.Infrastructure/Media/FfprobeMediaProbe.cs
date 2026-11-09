using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MyAfricaMyNarrative.Application;

namespace MyAfricaMyNarrative.Infrastructure.Media;

public sealed class FfprobeMediaProbe(IOptions<MediaOptions> options) : IMediaProbe
{
    public async Task<ProbedMedia> ProbeAsync(Stream source, CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"mamn-probe-{Guid.NewGuid():N}.media");
        try
        {
            await using (var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
                await source.CopyToAsync(output, cancellationToken);
            var start = new ProcessStartInfo { FileName = options.Value.FfprobePath, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            start.ArgumentList.Add("-v"); start.ArgumentList.Add("error"); start.ArgumentList.Add("-show_packets"); start.ArgumentList.Add("-show_entries");
            start.ArgumentList.Add("format=format_name,duration:stream=codec_type,width,height,duration:packet=pts_time,duration_time"); start.ArgumentList.Add("-of"); start.ArgumentList.Add("json"); start.ArgumentList.Add(tempPath);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("ffprobe could not be started.");
            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken); var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(20));
            try { await process.WaitForExitAsync(timeout.Token); } catch (OperationCanceledException) { try { process.Kill(true); } catch { } throw new InvalidDataException("Media probing timed out."); }
            var json = await stdout; var error = await stderr;
            if (process.ExitCode != 0) throw new InvalidDataException($"Media could not be decoded: {Trim(error)}");
            using var document = JsonDocument.Parse(json); var root = document.RootElement;
            var format = root.GetProperty("format"); var durationText = format.TryGetProperty("duration", out var durationValue) ? durationValue.GetString() : null;
            double.TryParse(durationText, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds);
            var hasAudio = false; var hasVideo = false; int? width = null; int? height = null;
            foreach (var stream in root.GetProperty("streams").EnumerateArray())
            {
                var type = stream.GetProperty("codec_type").GetString(); hasAudio |= type == "audio"; hasVideo |= type == "video";
                if (type == "video") { if (stream.TryGetProperty("width", out var w)) width = w.GetInt32(); if (stream.TryGetProperty("height", out var h)) height = h.GetInt32(); }
                if (seconds <= 0 && stream.TryGetProperty("duration", out var streamDuration) && double.TryParse(streamDuration.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) seconds = Math.Max(seconds, parsed);
            }
            if (!hasAudio && !hasVideo) throw new InvalidDataException("No playable audio or video stream was found.");
            if (seconds <= 0 && root.TryGetProperty("packets", out var packets))
                foreach (var packet in packets.EnumerateArray())
                {
                    double.TryParse(packet.TryGetProperty("pts_time", out var pts) ? pts.GetString() : null, NumberStyles.Float, CultureInfo.InvariantCulture, out var packetStart);
                    double.TryParse(packet.TryGetProperty("duration_time", out var packetDuration) ? packetDuration.GetString() : null, NumberStyles.Float, CultureInfo.InvariantCulture, out var packetLength);
                    seconds = Math.Max(seconds, packetStart + packetLength);
                }
            if (seconds <= 0 || seconds > 300) throw new InvalidDataException("Media duration is missing or exceeds five minutes.");
            return new((long)Math.Round(seconds * 1000), width, height, hasAudio, hasVideo, format.GetProperty("format_name").GetString() ?? "unknown");
        }
        finally { if (File.Exists(tempPath)) File.Delete(tempPath); if (source.CanSeek) source.Position = 0; }
    }

    private static string Trim(string value) => value.Length <= 400 ? value.Trim() : value[..400].Trim();
}
