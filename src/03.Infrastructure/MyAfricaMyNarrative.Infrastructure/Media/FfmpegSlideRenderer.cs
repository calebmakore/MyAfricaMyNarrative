using System.Text;
using Microsoft.Extensions.Options;
using MyAfricaMyNarrative.Application;

namespace MyAfricaMyNarrative.Infrastructure.Media;

/// <summary>Finds the font used for text burned into films and slides.</summary>
public static class FontLocator
{
    public static string Resolve(MediaOptions options)
    {
        var configured = options.CaptionFontFile;
        var path = string.IsNullOrWhiteSpace(configured) ? Path.Combine(AppContext.BaseDirectory, "Fonts", "Jost-SemiBold.ttf") : Path.GetFullPath(configured);
        if (!File.Exists(path))
            throw new InvalidOperationException($"Caption font not found at {Path.GetFileName(path)}. Rebuild so the bundled font is copied, or set Media:CaptionFontFile to a TTF/OTF file.");
        return path;
    }
}

/// <summary>
/// Draws a title slide with FFmpeg: a solid brand-colour background, the title and an optional line beneath it.
/// Text goes through files (never the command line) and FFmpeg runs bounded, with shell execution disabled.
/// </summary>
public sealed class FfmpegSlideRenderer(IOptions<MediaOptions> options) : ISlideRenderer
{
    public const int MaxTitle = 80, MaxSubtitle = 140;

    public static (string Background, string Text, string Accent, string Soft) Colours(SlideTheme theme) => theme switch
    {
        SlideTheme.Sand => ("0xF5ECDE", "0x1C1410", "0xB8472A", "0x5C4A3D"),
        SlideTheme.Terracotta => ("0xB8472A", "0xFFFAF2", "0xE3A33A", "0xF5E3D6"),
        SlideTheme.Ochre => ("0xE3A33A", "0x1C1410", "0xB8472A", "0x3A2A1E"),
        _ => ("0x1C1410", "0xFFFAF2", "0xB8472A", "0xE3A33A")
    };

    public static List<string> Args(string titleFile, string? subtitleFile, string fontFile, SlideTheme theme, string output, int titleLines)
    {
        var (bg, text, accent, soft) = Colours(theme);
        // Title block sits just above the middle; the accent bar and subtitle sit below it.
        var titleY = $"(h/2)-text_h-60";
        var filters = new List<string>
        {
            $"drawtext=fontfile={fontFile}:textfile={titleFile}:expansion=none:text_align=C:fontsize=96:fontcolor={text}:line_spacing=18:x=(w-text_w)/2:y={titleY}",
            $"drawbox=x=(iw-160)/2:y=(ih/2)-20:w=160:h=10:color={accent}:t=fill"
        };
        if (subtitleFile is not null)
            filters.Add($"drawtext=fontfile={fontFile}:textfile={subtitleFile}:expansion=none:text_align=C:fontsize=54:fontcolor={soft}:line_spacing=14:x=(w-text_w)/2:y=(h/2)+40");
        return ["-y", "-f", "lavfi", "-i", $"color=c={bg}:s=1080x1920:d=1", "-frames:v", "1", "-update", "1", "-vf", string.Join(',', filters), output];
    }

    public async Task<string> RenderAsync(string title, string subtitle, SlideTheme theme, string workFolder, CancellationToken ct)
    {
        title = title.Trim(); subtitle = (subtitle ?? "").Trim();
        if (title.Length is < 1 or > MaxTitle) throw new ArgumentException($"Slide titles are 1–{MaxTitle} characters.");
        if (subtitle.Length > MaxSubtitle) throw new ArgumentException($"The line beneath the title is up to {MaxSubtitle} characters.");
        if (!Enum.IsDefined(theme)) throw new ArgumentException("Choose a slide colour.");
        Directory.CreateDirectory(workFolder);
        File.Copy(FontLocator.Resolve(options.Value), Path.Combine(workFolder, "slide-font.ttf"), overwrite: true);
        var wrappedTitle = RenderText.Wrap(title, 16);
        await File.WriteAllTextAsync(Path.Combine(workFolder, "title.txt"), wrappedTitle, new UTF8Encoding(false), ct);
        string? subtitleFile = null;
        if (subtitle.Length > 0) { subtitleFile = "subtitle.txt"; await File.WriteAllTextAsync(Path.Combine(workFolder, subtitleFile), RenderText.Wrap(subtitle, 30), new UTF8Encoding(false), ct); }
        await BoundedProcessRunner.RunAsync(options.Value.FfmpegPath, Args("title.txt", subtitleFile, "slide-font.ttf", theme, "slide.png", wrappedTitle.Split('\n').Length),
            TimeSpan.FromSeconds(30), ct, workFolder);
        return Path.Combine(workFolder, "slide.png");
    }
}
