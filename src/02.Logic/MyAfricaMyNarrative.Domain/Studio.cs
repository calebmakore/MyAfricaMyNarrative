using System.Security.Cryptography;
using System.Text;

namespace MyAfricaMyNarrative.Domain;

public enum TextOrigin { SourceQuotation, CreatorWritten }
public enum ResourceKind { Equipment, Person, Location, Time, Budget }
public enum ArtifactKind { PreviewVideo, FinalVideo, Captions, PreservationArchive }

public sealed class TranscriptRevision
{
    private TranscriptRevision() { }
    public TranscriptRevision(Guid projectId, Guid assetId, string text, string language, long startMs, long endMs, Guid reviewerUserId, DateTimeOffset now)
    {
        if (projectId == Guid.Empty || assetId == Guid.Empty || reviewerUserId == Guid.Empty) throw new ArgumentException("Project, asset and reviewer are required.");
        text = text.Trim(); language = language.Trim();
        if (text.Length is < 1 or > 12000) throw new ArgumentException("Reviewed text must be 1–12000 characters.", nameof(text));
        if (language.Length is < 2 or > 40) throw new ArgumentException("Language must be 2–40 characters.", nameof(language));
        if (startMs < 0 || endMs <= startMs || endMs - startMs > 300_000) throw new ArgumentException("Timing must be a positive range no longer than five minutes.");
        Id = Guid.NewGuid(); ProjectId = projectId; AssetId = assetId; Text = text; Language = language;
        StartMs = startMs; EndMs = endMs; ReviewerUserId = reviewerUserId; ReviewedAtUtc = now; CreatedAtUtc = now;
    }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid AssetId { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public string Language { get; private set; } = string.Empty;
    public long StartMs { get; private set; }
    public long EndMs { get; private set; }
    public Guid ReviewerUserId { get; private set; }
    public DateTimeOffset ReviewedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}

public sealed record SceneDraft(string Title, string Text, TextOrigin TextOrigin, Guid AudioAssetId,
    Guid VisualAssetId, Guid? TranscriptRevisionId, long StartMs, long EndMs, string Caption);

public sealed class StoryRevision
{
    private StoryRevision() { }
    public StoryRevision(Guid projectId, int revisionNumber, IEnumerable<SceneDraft> drafts, DateTimeOffset now)
    {
        if (projectId == Guid.Empty || revisionNumber < 1) throw new ArgumentException("A project and positive revision number are required.");
        var items = drafts.ToList();
        if (items.Count > 50) throw new ArgumentException("A story can contain at most 50 scenes.");
        Id = Guid.NewGuid(); ProjectId = projectId; RevisionNumber = revisionNumber; CreatedAtUtc = now;
        for (var index = 0; index < items.Count; index++) Scenes.Add(new StoryScene(Id, index + 1, items[index]));
        SnapshotHash = ComputeHash(items);
    }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public int RevisionNumber { get; private set; }
    public string SnapshotHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public ICollection<StoryScene> Scenes { get; } = new List<StoryScene>();

    private static string ComputeHash(IEnumerable<SceneDraft> scenes)
    {
        var canonical = string.Join("\n", scenes.Select((x, i) => string.Join('|', i + 1, x.Title.Trim(), x.Text.Trim(),
            (int)x.TextOrigin, x.AudioAssetId, x.VisualAssetId, x.TranscriptRevisionId, x.StartMs, x.EndMs, x.Caption.Trim())));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}

public sealed class StoryScene
{
    private StoryScene() { }
    internal StoryScene(Guid revisionId, int order, SceneDraft draft)
    {
        var title = draft.Title.Trim(); var text = draft.Text.Trim(); var caption = draft.Caption.Trim();
        if (title.Length is < 1 or > 120) throw new ArgumentException("Scene title must be 1–120 characters.");
        if (text.Length > 4000 || caption.Length > 2000) throw new ArgumentException("Scene text is too long.");
        if (draft.AudioAssetId == Guid.Empty || draft.VisualAssetId == Guid.Empty) throw new ArgumentException("Each scene needs real audio and a visual.");
        if (draft.StartMs < 0 || draft.EndMs <= draft.StartMs || draft.EndMs - draft.StartMs > 300_000) throw new ArgumentException("Scene timing is invalid.");
        Id = Guid.NewGuid(); StoryRevisionId = revisionId; Order = order; Title = title; Text = text; TextOrigin = draft.TextOrigin;
        AudioAssetId = draft.AudioAssetId; VisualAssetId = draft.VisualAssetId; TranscriptRevisionId = draft.TranscriptRevisionId;
        StartMs = draft.StartMs; EndMs = draft.EndMs; Caption = caption;
    }
    public Guid Id { get; private set; }
    public Guid StoryRevisionId { get; private set; }
    public int Order { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Text { get; private set; } = string.Empty;
    public TextOrigin TextOrigin { get; private set; }
    public Guid AudioAssetId { get; private set; }
    public Guid VisualAssetId { get; private set; }
    public Guid? TranscriptRevisionId { get; private set; }
    public long StartMs { get; private set; }
    public long EndMs { get; private set; }
    public string Caption { get; private set; } = string.Empty;
}

public sealed class ReviewApproval
{
    private ReviewApproval() { }
    public ReviewApproval(Guid projectId, Guid storyRevisionId, string snapshotHash, Guid actorUserId, DateTimeOffset now)
    { Id = Guid.NewGuid(); ProjectId = projectId; StoryRevisionId = storyRevisionId; SnapshotHash = snapshotHash; ActorUserId = actorUserId; ApprovedAtUtc = now; }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid StoryRevisionId { get; private set; }
    public string SnapshotHash { get; private set; } = string.Empty;
    public Guid ActorUserId { get; private set; }
    public DateTimeOffset ApprovedAtUtc { get; private set; }
}

public sealed class ResourceItem
{
    private ResourceItem() { }
    public ResourceItem(Guid projectId, ResourceKind kind, string name, string availability, DateTimeOffset now)
    {
        name = name.Trim(); availability = availability.Trim();
        if (name.Length is < 1 or > 120 || availability.Length > 300) throw new ArgumentException("Resource details are invalid.");
        Id = Guid.NewGuid(); ProjectId = projectId; Kind = kind; Name = name; Availability = availability; IsAvailable = true; CreatedAtUtc = now;
    }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public ResourceKind Kind { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Availability { get; private set; } = string.Empty;
    public bool IsAvailable { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public void SetAvailable(bool value) => IsAvailable = value;
}

public sealed class ExportArtifact
{
    private ExportArtifact() { }
    public ExportArtifact(Guid projectId, Guid jobId, Guid storyRevisionId, string snapshotHash, ArtifactKind kind,
        string storageKey, string sha256, long byteLength, DateTimeOffset now)
    { Id = Guid.NewGuid(); ProjectId = projectId; JobId = jobId; StoryRevisionId = storyRevisionId; SnapshotHash = snapshotHash; Kind = kind; StorageKey = storageKey; Sha256 = sha256; ByteLength = byteLength; CreatedAtUtc = now; }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid JobId { get; private set; }
    public Guid StoryRevisionId { get; private set; }
    public string SnapshotHash { get; private set; } = string.Empty;
    public ArtifactKind Kind { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string Sha256 { get; private set; } = string.Empty;
    public long ByteLength { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
