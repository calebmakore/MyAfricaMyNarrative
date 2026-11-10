using System.Diagnostics;

namespace MyAfricaMyNarrative.Infrastructure.Media;

public static class BoundedProcessRunner
{
    public static async Task RunAsync(string executable, IEnumerable<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start FFmpeg.");
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(limit.Token);
        }
        catch
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw;
        }
        var error = await stderr;
        _ = await stdout;
        if (process.ExitCode != 0)
            throw new InvalidDataException("FFmpeg failed: " + (error.Length > 2000 ? error[^2000..] : error));
    }
}
