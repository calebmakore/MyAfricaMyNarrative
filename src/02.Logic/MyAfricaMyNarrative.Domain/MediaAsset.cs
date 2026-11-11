namespace MyAfricaMyNarrative.Domain;

public enum MediaKind { Image, Audio, Video }
public enum PermissionStatus { Granted, Withdrawn }
/// <summary>Whether the person who supplied a source agreed to its use in this story, as recorded by the project owner.</summary>
public enum ContributorUse { OwnMaterial, NotYetAsked, Agreed, Declined }

public sealed class MediaAsset
{
    private MediaAsset() { }

    public MediaAsset(Guid projectId, string storageKey, string originalName, string displayName, string sha256, string contentType,
        MediaKind kind, long byteLength, string? attribution, DateTimeOffset createdAtUtc, long durationMs = 0,
        int? width = null, int? height = null, bool hasSound = false)
    {
        HasSound = kind == MediaKind.Audio || (kind == MediaKind.Video && hasSound);
        Id = Guid.NewGuid();
        ProjectId = projectId;
        StorageKey = storageKey;
        OriginalName = originalName;
        SetDisplayName(displayName);
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
    public string DisplayName { get; private set; } = string.Empty;
    public string Sha256 { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public MediaKind Kind { get; private set; }
    public long ByteLength { get; private set; }
    public long DurationMs { get; private set; }
    /// <summary>True for audio, and for video whose decoded streams include sound (checked at upload).</summary>
    public bool HasSound { get; private set; }
    /// <summary>A recording a scene can use as its voice: an audio source, or a video recorded with sound.</summary>
    public bool CanProvideSound => Kind == MediaKind.Audio || (Kind == MediaKind.Video && HasSound);
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

    /// <summary>Who supplied this source. Empty means the project owner's own material.</summary>
    public string SuppliedBy { get; private set; } = string.Empty;
    public ContributorUse ContributorUse { get; private set; } = ContributorUse.OwnMaterial;
    public string ContributorNote { get; private set; } = string.Empty;
    public DateTimeOffset? ContributorUseRecordedAtUtc { get; private set; }

    /// <summary>
    /// Records who supplied the source and whether they agreed to this use. This is the owner's record of the contributor's answer,
    /// not independent verification, and the UI says so.
    /// </summary>
    public void SetContributor(string? suppliedBy, ContributorUse use, string? note, DateTimeOffset now)
    {
        suppliedBy = (suppliedBy ?? string.Empty).Trim(); note = (note ?? string.Empty).Trim();
        if (suppliedBy.Length > 120 || note.Length > 300) throw new ArgumentException("Contributor name or note is too long.");
        if (!Enum.IsDefined(use)) throw new ArgumentException("Unknown contributor status.");
        if (suppliedBy.Length == 0) use = ContributorUse.OwnMaterial;
        else if (use == ContributorUse.OwnMaterial) use = ContributorUse.NotYetAsked;
        SuppliedBy = suppliedBy; ContributorUse = use; ContributorNote = note; ContributorUseRecordedAtUtc = now;
    }

    public void SetDisplayName(string value)
    {
        value = value.Trim();
        if (value.Length is < 1 or > 120) throw new ArgumentException("Source name must be 1–120 characters.", nameof(value));
        DisplayName = value;
    }
}
