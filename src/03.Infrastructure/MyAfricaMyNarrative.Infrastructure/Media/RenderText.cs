using System.Globalization;
using System.Text;
using MyAfricaMyNarrative.Domain;

namespace MyAfricaMyNarrative.Infrastructure.Media;

/// <summary>
/// Text layout for captions, credits and SRT files. Caption text is only ever written to files that FFmpeg reads
/// with expansion disabled, so apostrophes, brackets, colons, percent signs and newlines are never interpreted.
/// </summary>
public static class RenderText
{
    public const string Tagline = "Your story. Your voice. Our Africa.";

    public static string Wrap(string text, int maxChars = 32)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = new StringBuilder();
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length > 0 && line.Length + 1 + word.Length > maxChars) { lines.Add(line.ToString()); line.Clear(); }
                if (line.Length > 0) line.Append(' ');
                line.Append(word);
            }
            if (line.Length > 0) lines.Add(line.ToString());
        }
        return string.Join('\n', lines);
    }

    /// <summary>Disclosure added to the credits whenever an approved revision keeps AI-suggested words.</summary>
    public const string AiWordsDisclosure = "Some words drafted with AI, approved by the storyteller";

    public static string CreditsCard(IReadOnlyList<CreditEntry> credits, bool aiWords = false)
    {
        var builder = new StringBuilder("Credits\n");
        foreach (var credit in credits) builder.Append('\n').Append(credit.Role).Append(": ").Append(credit.Name);
        if (aiWords) builder.Append("\n\n").Append(AiWordsDisclosure);
        return builder.Append("\n\n").Append(Tagline).ToString();
    }

    public static string CreditsCue(IReadOnlyList<CreditEntry> credits, bool aiWords = false) =>
        "Credits\n" + string.Join('\n', credits.Select(x => $"{x.Role}: {x.Name}")) + (aiWords ? "\n" + AiWordsDisclosure : "");

    public static void AppendCue(StringBuilder srt, int index, long startMs, long endMs, string text)
    {
        // A blank line ends an SRT cue, so blank lines inside caption text are collapsed.
        var safe = string.Join('\n', text.Replace("\r\n", "\n").Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0));
        srt.Append(index.ToString(CultureInfo.InvariantCulture)).Append('\n')
            .Append(SrtTime(startMs)).Append(" --> ").Append(SrtTime(endMs)).Append('\n')
            .Append(safe).Append("\n\n");
    }

    public static string SrtTime(long ms) => TimeSpan.FromMilliseconds(ms).ToString(@"hh\:mm\:ss\,fff", CultureInfo.InvariantCulture);

    /// <summary>Builds a drawtext filter that reads text from a relative file in the FFmpeg working directory.</summary>
    public static string DrawText(string textFile, string fontFile, int fontSize, int width, string yExpression) =>
        $"drawtext=fontfile={fontFile}:textfile={textFile}:expansion=none:text_align=C:fontcolor=white:fontsize={fontSize}" +
        $":line_spacing={Math.Max(4, fontSize / 4)}:box=1:boxcolor=black@0.55:boxborderw={Math.Max(8, width / 54)}:x=(w-text_w)/2:y={yExpression}";

    /// <summary>FFmpeg arguments for one scene segment. Paths are relative to the render working directory.</summary>
    /// <param name="visualCarriesTheSound">True when the picture and the voice come from the same recorded video:
    /// both are then cut from the same moment so lips and words stay in sync.</param>
    public static List<string> SceneSegmentArgs(string visualFile, bool visualIsImage, string audioFile, long startMs, long endMs,
        int width, int height, string? captionFile, string fontFile, string output, bool visualCarriesTheSound = false)
    {
        var filter = $"scale={width}:{height}:force_original_aspect_ratio=decrease,pad={width}:{height}:(ow-iw)/2:(oh-ih)/2,fps=30";
        if (captionFile is not null) filter += "," + DrawText(captionFile, fontFile, CaptionFontSize(width), width, "h-text_h-h/8");
        var args = new List<string> { "-y" };
        if (visualCarriesTheSound && !visualIsImage)
        {
            args.AddRange(["-ss", Seconds(startMs), "-i", visualFile, "-t", Seconds(endMs - startMs), "-vf", filter,
                "-map", "0:v:0", "-map", "0:a:0", "-c:v", "libx264", "-preset", "veryfast", "-pix_fmt", "yuv420p",
                "-c:a", "aac", "-b:a", "128k", "-ar", "48000", "-ac", "2", output]);
            return args;
        }
        if (visualIsImage) args.AddRange(["-loop", "1"]); else args.AddRange(["-stream_loop", "-1"]);
        args.AddRange(["-i", visualFile, "-ss", Seconds(startMs), "-i", audioFile, "-t", Seconds(endMs - startMs), "-vf", filter,
            "-map", "0:v:0", "-map", "1:a:0", "-c:v", "libx264", "-preset", "veryfast", "-pix_fmt", "yuv420p",
            "-c:a", "aac", "-b:a", "128k", "-ar", "48000", "-ac", "2", output]);
        return args;
    }

    /// <summary>FFmpeg arguments for the silent end credits card.</summary>
    public static List<string> CreditsCardArgs(int creditCount, int width, int height, string creditsFile, string fontFile, string output)
    {
        var lines = creditCount + 4;
        var fontSize = (int)Math.Min(CaptionFontSize(width), Math.Floor(height * 0.8 / (lines * 1.35)));
        var seconds = Seconds(CreditsRules.CardDurationMs);
        return ["-y", "-f", "lavfi", "-i", $"color=c=0x2A2320:s={width}x{height}:r=30:d={seconds}",
            "-f", "lavfi", "-i", "anullsrc=r=48000:cl=stereo", "-t", seconds,
            "-vf", DrawText(creditsFile, fontFile, fontSize, width, "(h-text_h)/2"),
            "-map", "0:v:0", "-map", "1:a:0", "-c:v", "libx264", "-preset", "veryfast", "-pix_fmt", "yuv420p",
            "-c:a", "aac", "-b:a", "128k", "-ar", "48000", "-ac", "2", output];
    }

    public static List<string> ConcatArgs(string listFile, string output) =>
        ["-y", "-f", "concat", "-safe", "0", "-i", listFile, "-c", "copy", "-movflags", "+faststart", output];

    public static int CaptionFontSize(int width) => (int)Math.Round(46d * width / 1080);

    private static string Seconds(long ms) => (ms / 1000d).ToString("0.###", CultureInfo.InvariantCulture);
}
