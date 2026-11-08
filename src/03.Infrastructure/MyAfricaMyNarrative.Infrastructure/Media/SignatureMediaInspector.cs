using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;

namespace MyAfricaMyNarrative.Infrastructure.Media;

public sealed class SignatureMediaInspector : IMediaInspector
{
    public async ValueTask<InspectedMedia?> InspectAsync(Stream source, CancellationToken cancellationToken)
    {
        var header = new byte[16];
        var read = await source.ReadAsync(header, cancellationToken);
        source.Position = 0;
        var h = header.AsSpan(0, read);
        if (read >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return new(MediaKind.Image, "image/jpeg");
        if (read >= 8 && h[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return new(MediaKind.Image, "image/png");
        if (read >= 12 && h[..4].SequenceEqual("RIFF"u8) && h[8..12].SequenceEqual("WEBP"u8)) return new(MediaKind.Image, "image/webp");
        if (h.StartsWith("ID3"u8) || (read >= 2 && h[0] == 0xFF && (h[1] & 0xE0) == 0xE0)) return new(MediaKind.Audio, "audio/mpeg");
        if (read >= 12 && h[..4].SequenceEqual("RIFF"u8) && h[8..12].SequenceEqual("WAVE"u8)) return new(MediaKind.Audio, "audio/wav");
        if (read >= 12 && h[4..8].SequenceEqual("ftyp"u8)) return new(MediaKind.Video, "video/mp4");
        if (read >= 4 && h[..4].SequenceEqual(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 })) return new(MediaKind.Video, "video/webm");
        return null;
    }
}
