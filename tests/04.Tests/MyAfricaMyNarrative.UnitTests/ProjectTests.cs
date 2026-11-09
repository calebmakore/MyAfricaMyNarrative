using MyAfricaMyNarrative.Domain;

namespace MyAfricaMyNarrative.UnitTests;

public sealed class ProjectTests
{
    [Fact]
    public void Project_requires_an_owner() =>
        Assert.Throws<ArgumentException>(() => new Project(Guid.Empty, "Story", DateTimeOffset.UtcNow));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Project_rejects_empty_titles(string title) =>
        Assert.Throws<ArgumentException>(() => new Project(Guid.NewGuid(), title, DateTimeOffset.UtcNow));

    [Fact]
    public void Permission_withdrawal_is_explicit_and_timestamped()
    {
        var granted = DateTimeOffset.UtcNow;
        var withdrawn = granted.AddMinutes(1);
        var asset = new MediaAsset(Guid.NewGuid(), "key.original", "voice.wav", "Grandmother interview", new string('a', 64), "audio/wav", MediaKind.Audio, 12, "Creator", granted);
        asset.Withdraw(withdrawn);
        Assert.Equal(PermissionStatus.Withdrawn, asset.PermissionStatus);
        Assert.Equal(withdrawn, asset.PermissionWithdrawnAtUtc);
        Assert.Equal("Owner attestation", asset.PermissionDeclaration);
    }

    [Fact]
    public void Media_asset_allows_omitted_optional_attribution()
    {
        var asset = new MediaAsset(Guid.NewGuid(), "key.original", "voice.weba", "Opening voice note", new string('a', 64),
            "audio/webm", MediaKind.Audio, 12, null, DateTimeOffset.UtcNow);

        Assert.Equal(string.Empty, asset.Attribution);
    }

    [Fact]
    public void Source_display_name_can_change_without_changing_original_filename()
    {
        var asset = new MediaAsset(Guid.NewGuid(), "key.original", "device-file.weba", "First interview", new string('a', 64),
            "audio/webm", MediaKind.Audio, 12, null, DateTimeOffset.UtcNow);
        asset.SetDisplayName("Market memories");
        Assert.Equal("Market memories", asset.DisplayName);
        Assert.Equal("device-file.weba", asset.OriginalName);
    }
}
