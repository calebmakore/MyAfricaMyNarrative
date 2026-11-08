using MyAfricaMyNarrative.Domain;

namespace MyAfricaMyNarrative.Application;

public interface ICurrentUser
{
    Guid? UserId { get; }
}

public interface IProjectRepository
{
    Task<IReadOnlyList<Project>> ListAsync(Guid ownerId, CancellationToken cancellationToken);
    Task<Project?> FindOwnedAsync(Guid projectId, Guid ownerId, CancellationToken cancellationToken);
    Task<MediaAsset?> FindOwnedAssetAsync(Guid assetId, Guid ownerId, CancellationToken cancellationToken);
    Task AddAsync(Project project, CancellationToken cancellationToken);
    Task RemoveAsync(Project project, CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IPrivateMediaStorage
{
    Task<StoredMedia> WriteOriginalAsync(Stream source, long maxBytes, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken);
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

public sealed record StoredMedia(string Key, string Sha256, long Length);
public sealed record InspectedMedia(MediaKind Kind, string ContentType);

public interface IMediaInspector
{
    ValueTask<InspectedMedia?> InspectAsync(Stream source, CancellationToken cancellationToken);
}

public sealed class MediaOptions
{
    public const string SectionName = "Media";
    public string StorageRoot { get; set; } = "App_Data/private-media";
    public long MaxUploadBytes { get; set; } = 20 * 1024 * 1024;
    public int MaxAssetsPerProject { get; set; } = 10;
    public long MaxProjectBytes { get; set; } = 200 * 1024 * 1024;
    public string FfmpegPath { get; set; } = "ffmpeg";
    public string FfprobePath { get; set; } = "ffprobe";
}

public sealed class AiOptions
{
    public const string SectionName = "Ai";
    public string Mode { get; set; } = "Manual";
    public string Provider { get; set; } = "OpenAI";
    public string? ApiKey { get; set; }
    public string StoryModel { get; set; } = string.Empty;
    public string TranscriptionModel { get; set; } = string.Empty;
    public int MaxCallsPerProject { get; set; } = 20;
    public int RequestTimeoutSeconds { get; set; } = 60;
}

public sealed class JobOptions
{
    public const string SectionName = "Jobs";
    public int MaxAttempts { get; set; } = 3;
    public int LeaseSeconds { get; set; } = 120;
}
