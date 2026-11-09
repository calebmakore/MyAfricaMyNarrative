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
    Task<Project?> FindOwnedStudioAsync(Guid projectId, Guid ownerId, CancellationToken cancellationToken);
    Task<Job?> FindOwnedJobAsync(Guid jobId, Guid ownerId, CancellationToken cancellationToken);
    Task<Job?> FindOwnedJobForUpdateAsync(Guid projectId, Guid ownerId, string kind, string targetHash, CancellationToken cancellationToken);
    Task<ExportArtifact?> FindOwnedArtifactAsync(Guid artifactId, Guid ownerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Job>> ListOwnedJobsAsync(Guid projectId, Guid ownerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExportArtifact>> ListOwnedArtifactsAsync(Guid projectId, Guid ownerId, CancellationToken cancellationToken);
    Task AddAsync(Project project, CancellationToken cancellationToken);
    Task AddJobAsync(Job job, CancellationToken cancellationToken);
    Task RemoveAsync(Project project, CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IPrivateMediaStorage
{
    Task<StoredMedia> WriteOriginalAsync(Stream source, long maxBytes, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken);
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

public sealed record ClaimedJob(Guid Id, Guid ProjectId, Guid OwnerUserId, string Kind, string PayloadJson, Guid AttemptToken);
public interface IJobStore
{
    Task<ClaimedJob?> ClaimAsync(string worker, TimeSpan lease, CancellationToken cancellationToken);
    Task CompleteAsync(ClaimedJob claim, IReadOnlyList<ExportArtifact> artifacts, CancellationToken cancellationToken);
    Task FailAsync(ClaimedJob claim, string failure, DateTimeOffset retryAt, int maxAttempts, CancellationToken cancellationToken);
}

public interface IExportJobHandler
{
    Task<IReadOnlyList<ExportArtifact>> RenderAsync(ClaimedJob claim, CancellationToken cancellationToken);
}

public sealed record ExportReadiness(bool CanPreview, bool CanExportFinal, IReadOnlyList<string> Issues,
    StoryRevision? Revision, ReviewApproval? Approval, long DurationMs);

public interface IExportReadinessService
{
    ExportReadiness Check(Project project);
}

public sealed record StoredMedia(string Key, string Sha256, long Length);
public sealed record InspectedMedia(MediaKind Kind, string ContentType);
public sealed record ProbedMedia(long DurationMs, int? Width, int? Height, bool HasAudio, bool HasVideo, string FormatName);

public interface IMediaInspector
{
    ValueTask<InspectedMedia?> InspectAsync(Stream source, CancellationToken cancellationToken);
}

public interface IMediaProbe
{
    Task<ProbedMedia> ProbeAsync(Stream source, CancellationToken cancellationToken);
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
