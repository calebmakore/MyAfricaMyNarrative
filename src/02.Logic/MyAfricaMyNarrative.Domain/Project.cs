namespace MyAfricaMyNarrative.Domain;

public sealed class Project
{
    private Project() { }

    public Project(Guid ownerUserId, string title, DateTimeOffset createdAtUtc)
    {
        if (ownerUserId == Guid.Empty) throw new ArgumentException("An owner is required.", nameof(ownerUserId));
        Id = Guid.NewGuid();
        OwnerUserId = ownerUserId;
        Rename(title, createdAtUtc);
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public bool IsArchived { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public ICollection<MediaAsset> MediaAssets { get; } = new List<MediaAsset>();
    public ICollection<TranscriptRevision> TranscriptRevisions { get; } = new List<TranscriptRevision>();
    public ICollection<StoryRevision> StoryRevisions { get; } = new List<StoryRevision>();
    public ICollection<ReviewApproval> ReviewApprovals { get; } = new List<ReviewApproval>();
    public ICollection<LanguageReview> LanguageReviews { get; } = new List<LanguageReview>();
    public ICollection<ResourceItem> Resources { get; } = new List<ResourceItem>();
    public ICollection<CreatorIntent> CreatorIntents { get; } = new List<CreatorIntent>();
    public ICollection<StudioProposal> StudioProposals { get; } = new List<StudioProposal>();

    public void Rename(string title, DateTimeOffset now)
    {
        title = title.Trim();
        if (title.Length is < 1 or > 120) throw new ArgumentException("Title must be 1–120 characters.", nameof(title));
        Title = title;
        UpdatedAtUtc = now;
    }

    public void SetArchived(bool archived, DateTimeOffset now)
    {
        IsArchived = archived;
        UpdatedAtUtc = now;
    }
}
