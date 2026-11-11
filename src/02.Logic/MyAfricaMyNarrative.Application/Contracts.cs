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
    Task<Job?> FindOwnedJobForUpdateAsync(Guid jobId, Guid ownerId, CancellationToken cancellationToken);
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
    /// <summary>Keeps a running attempt's lease alive. False means the attempt lost its lease and must stop.</summary>
    Task<bool> RenewAsync(ClaimedJob claim, TimeSpan lease, CancellationToken cancellationToken);
    /// <summary>Best-effort progress for the page; never fails the render.</summary>
    Task ReportProgressAsync(ClaimedJob claim, int step, int total, string note, CancellationToken cancellationToken);
}

public interface IExportJobHandler
{
    Task<IReadOnlyList<ExportArtifact>> RenderAsync(ClaimedJob claim, CancellationToken cancellationToken);
}

public sealed record ExportReadiness(bool CanPreview, bool CanExportFinal, IReadOnlyList<string> Issues,
    StoryRevision? Revision, ReviewApproval? Approval, long DurationMs, string CreditsHash = "", string ExportHash = "", bool CreditsApproved = false, int BlockingCount = 0);

public interface IExportReadinessService
{
    ExportReadiness Check(Project project);
}

public sealed record StoryAssistantScene(Guid Id, int Order, string Title);
public sealed record StoryAssistantRequest(IReadOnlyList<StoryAssistantScene> Scenes, string Audience, string Meaning,
    string Tone, string LocalContext, string ProtectedTerms);
public sealed record StoryAssistantResult(IReadOnlyList<Guid> SceneOrder, string Summary, string Provider, string Model);
public interface IStoryAssistant
{
    Task<StoryAssistantResult> ProposeOrderAsync(StoryAssistantRequest request, CancellationToken cancellationToken);
}

public sealed record AudioTranscriptionResult(string Text, IReadOnlyList<string> Languages, string Provider, string Model);
public interface IAudioTranscriber
{
    Task<AudioTranscriptionResult> TranscribeAsync(Stream audio, string fileName, string contentType, CancellationToken cancellationToken);
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
    /// <summary>Burn approved captions and the credits card into rendered video. The SRT sidecar is always produced.</summary>
    public bool BurnInCaptions { get; set; } = true;
    /// <summary>Optional TTF/OTF used for burned-in text. Empty uses the bundled Jost font copied beside the application.</summary>
    public string CaptionFontFile { get; set; } = string.Empty;
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
    /// <summary>
    /// When true the web app renders films itself, so creators never start anything by hand. A separate Worker can still run
    /// alongside it; jobs are claimed with row locks and renewed leases, so each job renders once.
    /// </summary>
    public bool ProcessInWeb { get; set; } = true;
}
