using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;
using MyAfricaMyNarrative.Infrastructure.Media;

namespace MyAfricaMyNarrative.UnitTests;

public sealed class PreservationArchiveTests
{
    [Fact]
    public async Task Archive_contains_referenced_originals_metadata_media_and_matching_checksums()
    {
        var now = DateTimeOffset.UtcNow;
        var owner = Guid.NewGuid();
        var project = new Project(owner, "Dzimba — home", now);
        var files = new Dictionary<string, byte[]>
        {
            ["audio"] = Encoding.UTF8.GetBytes("audio-original"),
            ["visual"] = Encoding.UTF8.GetBytes("visual-original"),
            ["film"] = Encoding.UTF8.GetBytes("verified-film"),
            ["srt"] = Encoding.UTF8.GetBytes("1\n00:00:00,000 --> 00:00:01,000\nMhoro\n")
        };
        var audio = new MediaAsset(project.Id, "audio", "voice.weba", "Mbuya voice", Sha(files["audio"]), "audio/webm", MediaKind.Audio, files["audio"].Length, "Creator", now, 1000);
        var visual = new MediaAsset(project.Id, "visual", "home.jpg", "Family home", Sha(files["visual"]), "image/jpeg", MediaKind.Image, files["visual"].Length, "Creator", now, width: 100, height: 100);
        project.MediaAssets.Add(audio); project.MediaAssets.Add(visual);
        var revision = new StoryRevision(project.Id, 1,
            [new SceneDraft("Home", "Mhoro", TextOrigin.CreatorWritten, audio.Id, visual.Id, null, 0, 1000, "Mhoro")], now);
        project.StoryRevisions.Add(revision);
        var approval = new ReviewApproval(project.Id, revision.Id, revision.SnapshotHash, owner, now);
        var jobId = Guid.NewGuid();
        ExportArtifact[] artifacts =
        [
            new(project.Id, jobId, revision.Id, revision.SnapshotHash, ArtifactKind.FinalVideo, "film", Sha(files["film"]), files["film"].Length, now),
            new(project.Id, jobId, revision.Id, revision.SnapshotHash, ArtifactKind.Captions, "srt", Sha(files["srt"]), files["srt"].Length, now)
        ];
        await using var archiveBytes = new MemoryStream();

        await PreservationArchiveBuilder.BuildAsync(archiveBytes, project, revision, approval, artifacts, new MemoryStorage(files), CancellationToken.None);

        archiveBytes.Position = 0;
        using var archive = new ZipArchive(archiveBytes, ZipArchiveMode.Read, leaveOpen: true);
        var names = archive.Entries.Select(x => x.FullName).ToHashSet();
        Assert.Contains("film/approved.mp4", names);
        Assert.Contains("captions/approved.srt", names);
        Assert.Contains("metadata.json", names);
        Assert.Contains("README.txt", names);
        Assert.Contains("checksums.sha256", names);
        Assert.Equal(2, names.Count(x => x.StartsWith("originals/", StringComparison.Ordinal)));
        var metadata = await Read(archive.GetEntry("metadata.json")!);
        using var metadataJson = JsonDocument.Parse(metadata);
        Assert.Equal("Dzimba — home", metadataJson.RootElement.GetProperty("Project").GetProperty("Title").GetString());
        Assert.Contains("Your story. Your voice. Our Africa.", await Read(archive.GetEntry("README.txt")!));
        foreach (var line in (await Read(archive.GetEntry("checksums.sha256")!)).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split("  ", 2, StringSplitOptions.None);
            var entry = archive.GetEntry(parts[1]);
            Assert.NotNull(entry);
            await using var stream = entry!.Open();
            Assert.Equal(parts[0], Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant());
        }
    }

    private static string Sha(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
    private static async Task<string> Read(ZipArchiveEntry entry) { using var reader = new StreamReader(entry.Open(), Encoding.UTF8); return await reader.ReadToEndAsync(); }

    private sealed class MemoryStorage(IReadOnlyDictionary<string, byte[]> files) : IPrivateMediaStorage
    {
        public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken) => Task.FromResult<Stream>(new MemoryStream(files[key], writable: false));
        public Task<StoredMedia> WriteOriginalAsync(Stream source, long maxBytes, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteAsync(string key, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
