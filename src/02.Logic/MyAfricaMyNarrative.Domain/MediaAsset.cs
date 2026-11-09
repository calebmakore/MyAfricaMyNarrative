namespace MyAfricaMyNarrative.Domain;

public enum MediaKind { Image, Audio, Video }
public enum PermissionStatus { Granted, Withdrawn }

public sealed class MediaAsset
{
    private MediaAsset() { }

    public MediaAsset(Guid projectId, string storageKey, string originalName, string sha256, string contentType,
        MediaKind kind, long byteLength, string? attribution, DateTimeOffset createdAtUtc, long durationMs = 0,
        int? width = null, int? height = null)
    {
        Id = Guid.NewGuid();
        ProjectId = projectId;
        StorageKey = storageKey;
        OriginalName = originalName;
        Sha256 = sha256;
        ContentType = contentType;
        Kind = kind;
        ByteLength = byteLength;
        DurationMs = durationMs;
        Width = width;
        Height = height;
        Attribution = attribution?.Trim() ?? string.Empty;
        PermissionStatus = PermissionStatus.Granted;
        PermissionDeclaration = "Owner attestation";
        PermissionGrantedAtUtc = createdAtUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Project Project { get; private set; } = null!;
    public string StorageKey { get; private set; } = string.Empty;
    public string OriginalName { get; private set; } = string.Empty;
    public string Sha256 { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public MediaKind Kind { get; private set; }
    public long ByteLength { get; private set; }
    public long DurationMs { get; private set; }
    public int? Width { get; private set; }
    public int? Height { get; private set; }
    public string Attribution { get; private set; } = string.Empty;
    public string PermissionDeclaration { get; private set; } = string.Empty;
    public PermissionStatus PermissionStatus { get; private set; }
    public DateTimeOffset PermissionGrantedAtUtc { get; private set; }
    public DateTimeOffset? PermissionWithdrawnAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public void Withdraw(DateTimeOffset now)
    {
        PermissionStatus = PermissionStatus.Withdrawn;
        PermissionWithdrawnAtUtc = now;
    }
}
