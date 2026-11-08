using MyAfricaMyNarrative.Domain;
using MyAfricaMyNarrative.Infrastructure.Media;

namespace MyAfricaMyNarrative.UnitTests;

public sealed class MediaInspectorTests
{
    [Fact]
    public async Task Detects_png_from_bytes_not_filename()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0, 0, 0, 0 };
        var result = await new SignatureMediaInspector().InspectAsync(new MemoryStream(bytes), CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(MediaKind.Image, result.Kind);
        Assert.Equal("image/png", result.ContentType);
    }

    [Fact]
    public async Task Rejects_unknown_content()
    {
        var result = await new SignatureMediaInspector().InspectAsync(new MemoryStream("not media"u8.ToArray()), CancellationToken.None);
        Assert.Null(result);
    }
}
