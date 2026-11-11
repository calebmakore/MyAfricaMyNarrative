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
            // Browser recordings (WebM) have no seek index: seeking the input lands picture and sound at different points.
            // Decode from the start and cut both streams at the same timestamps instead, so they stay exactly as recorded.
            var s = Seconds(startMs); var d = Seconds(endMs - startMs);
            var graph = $"[0:v]trim=start={s}:duration={d},setpts=PTS-STARTPTS,{filter},tpad=stop_mode=clone:stop_duration={d},trim=duration={d}[v];" +
                        $"[0:a]atrim=start={s}:duration={d},asetpts=PTS-STARTPTS,apad,atrim=duration={d}[a]";
            args.AddRange(["-t", Seconds(endMs), "-i", visualFile, "-filter_complex", graph, "-map", "[v]", "-map", "[a]", "-t", d,
                "-c:v", "libx264", "-preset", "veryfast", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "128k", "-ar", "48000", "-ac", "2", output]);
            return args;
        }
        if (visualIsImage) args.AddRange(["-loop", "1"]); else args.AddRange(["-stream_loop", "-1"]);
        args.AddRange(["-i", visualFile, "-ss", Seconds(startMs), "-i", audioFile, "-t", Seconds(endMs - startMs), "-vf", filter,
            "-map", "0:v:0", "-map", "1:a:0", "-c:v", "libx264", "-preset", "veryfast", "-pix_fmt", "yuv420p",
            "-c:a", "aac", "-b:a", "128k", "-ar", "48000", "-ac", "2", output]);
        return args;
    }

    /// <summary>One picture in a composite scene: a still image, or a video played from <paramref name="SeekMs"/>.</summary>
    public sealed record ScenePicture(string File, bool IsImage, long SeekMs);

    /// <summary>Volume of a video's own sound when it is kept quietly under the voice.</summary>
    public const double ClipSoundVolume = 0.25;

    /// <summary>
    /// FFmpeg arguments for a scene that shows several pictures in turn and/or keeps a video's own sound:
    /// the voice runs for the whole scene, the pictures share the time evenly (the last takes any remainder),
    /// and the caption stays on screen throughout. With <paramref name="keepFirstPictureSound"/> the first picture's
    /// sound is mixed under the voice at <see cref="ClipSoundVolume"/>.
    /// </summary>
    public static List<string> SceneCompositeArgs(IReadOnlyList<ScenePicture> pictures, string audioFile, long startMs, long endMs,
        int width, int height, string? captionFile, string fontFile, string output, bool keepFirstPictureSound = false)
    {
        if (pictures.Count is < 1 or > 6) throw new ArgumentOutOfRangeException(nameof(pictures), "A scene shows 1–6 pictures.");
        var total = endMs - startMs; var share = total / pictures.Count;
        long DurationMs(int i) => i == pictures.Count - 1 ? total - share * (pictures.Count - 1) : share;
        var args = new List<string> { "-y" };
        for (var i = 0; i < pictures.Count; i++)
        {
            var duration = Seconds(DurationMs(i));
            var p = pictures[i];
            if (p.IsImage) args.AddRange(["-loop", "1", "-t", duration, "-i", p.File]);
            else args.AddRange(["-stream_loop", "-1", "-t", Seconds(p.SeekMs + DurationMs(i)), "-i", p.File]);   // cut by timestamp below
        }
        var voice = pictures.Count;
        args.AddRange(["-t", Seconds(endMs), "-i", audioFile]);
        var frame = $"scale={width}:{height}:force_original_aspect_ratio=decrease,pad={width}:{height}:(ow-iw)/2:(oh-ih)/2,fps=30,setsar=1,format=yuv420p";
        var graph = new StringBuilder();
        // Each picture fills exactly its share. Timestamps restart at zero first (browser recordings often start later),
        // then the last frame is held if a clip comes up short, and the result is trimmed to length.
        for (var i = 0; i < pictures.Count; i++)
        {
            var d = Seconds(DurationMs(i));
            var cut = pictures[i].IsImage ? "" : $"trim=start={Seconds(pictures[i].SeekMs)}:duration={d},";
            graph.Append($"[{i}:v]{cut}setpts=PTS-STARTPTS,{frame},tpad=stop_mode=clone:stop_duration={d},trim=duration={d},setpts=PTS-STARTPTS[v{i}];");
        }
        for (var i = 0; i < pictures.Count; i++) graph.Append($"[v{i}]");
        graph.Append($"concat=n={pictures.Count}:v=1:a=0[vc];");
        graph.Append(captionFile is null ? "[vc]null[vout]" : $"[vc]{DrawText(captionFile, fontFile, CaptionFontSize(width), width, "h-text_h-h/8")}[vout]");
        var t = Seconds(total);
        graph.Append($";[{voice}:a]atrim=start={Seconds(startMs)}:duration={t},asetpts=PTS-STARTPTS,aresample=48000,apad,atrim=duration={t}[voice]");
        if (keepFirstPictureSound)
            graph.Append($";[0:a]atrim=start={Seconds(pictures[0].SeekMs)}:duration={Seconds(DurationMs(0))},asetpts=PTS-STARTPTS,aresample=48000," +
                         $"volume={ClipSoundVolume.ToString(CultureInfo.InvariantCulture)}[clip];[voice][clip]amix=inputs=2:duration=first:dropout_transition=0:normalize=0[aout]");
        args.AddRange(["-filter_complex", graph.ToString(), "-map", "[vout]", "-map", keepFirstPictureSound ? "[aout]" : "[voice]",
            "-t", Seconds(total), "-c:v", "libx264", "-preset", "veryfast", "-pix_fmt", "yuv420p",
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
