namespace MyAfricaMyNarrative.Domain;

public enum StoryType { Unspecified, Creativity, Humour, Business, Love, Sport, Family, Community, Faith, History, Other }

/// <summary>
/// A local expression kept in its original language with a creator-written explanation shown as a subtitle the first time it appears.
/// Any edit clears the approval; only approved entries are rendered.
/// </summary>
public sealed class GlossaryEntry
{
    private GlossaryEntry() { }
    /// <param name="aiDrafted">True when the explanation text is an AI draft the creator accepted without rewriting it.</param>
    public GlossaryEntry(Guid projectId, string term, string language, string explanation, DateTimeOffset now, bool aiDrafted = false)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("A project is required.");
        Id = Guid.NewGuid(); ProjectId = projectId; CreatedAtUtc = now; Update(term, language, explanation, now); AiDrafted = aiDrafted;
    }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Term { get; private set; } = string.Empty;
    public string Language { get; private set; } = string.Empty;
    public string Explanation { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public bool IsApproved => ApprovedAtUtc is not null;
    /// <summary>The explanation is still the AI's draft wording. Cleared when the creator rewrites it.</summary>
    public bool AiDrafted { get; private set; }

    public void Update(string term, string language, string explanation, DateTimeOffset now)
    {
        term = term.Trim(); language = language.Trim(); explanation = explanation.Trim();
        if (term.Length is < 2 or > 80) throw new ArgumentException("The expression must be 2–80 characters.");
        if (language.Length > 40) throw new ArgumentException("Language must be up to 40 characters.");
        if (explanation.Length is < 2 or > 160) throw new ArgumentException("Keep the explanation to 2–160 characters so it fits on screen.");
        if (term == Term && language == Language && explanation == Explanation) return;
        if (Explanation.Length > 0 && explanation != Explanation) AiDrafted = false;
        Term = term; Language = language; Explanation = explanation; UpdatedAtUtc = now; ApprovedAtUtc = null;
    }
    public void Approve(DateTimeOffset now) => ApprovedAtUtc = now;
}

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
    public ICollection<TranscriptionProposal> TranscriptionProposals { get; } = new List<TranscriptionProposal>();
    public ICollection<StoryRevision> StoryRevisions { get; } = new List<StoryRevision>();
    public ICollection<ReviewApproval> ReviewApprovals { get; } = new List<ReviewApproval>();
    public ICollection<LanguageReview> LanguageReviews { get; } = new List<LanguageReview>();
    public ICollection<ResourceItem> Resources { get; } = new List<ResourceItem>();
    public ICollection<CreatorIntent> CreatorIntents { get; } = new List<CreatorIntent>();
    public ICollection<CreatorQuestion> CreatorQuestions { get; } = new List<CreatorQuestion>();
    public ICollection<StudioProposal> StudioProposals { get; } = new List<StudioProposal>();
    public ICollection<CreditEntry> Credits { get; } = new List<CreditEntry>();
    public ICollection<CreditsApproval> CreditsApprovals { get; } = new List<CreditsApproval>();
    public ICollection<ShotPlanItem> Shots { get; } = new List<ShotPlanItem>();
    public ICollection<ProductionPlan> ProductionPlans { get; } = new List<ProductionPlan>();

    /// <summary>The kind of everyday story this is — African storytelling is not one genre.</summary>
    public StoryType StoryType { get; private set; } = StoryType.Unspecified;
    /// <summary>The creator's own starting idea, pasted or typed before any recording. Creator-written, never generated.</summary>
    public string Idea { get; private set; } = string.Empty;
    public ICollection<GlossaryEntry> Glossary { get; } = new List<GlossaryEntry>();
    public ICollection<PilotFeedback> PilotFeedback { get; } = new List<PilotFeedback>();

    public void SetStoryType(StoryType type, DateTimeOffset now)
    {
        if (!Enum.IsDefined(type)) throw new ArgumentException("Unknown story type.", nameof(type));
        StoryType = type; UpdatedAtUtc = now;
    }

    public void SetIdea(string? idea, DateTimeOffset now)
    {
        idea = (idea ?? string.Empty).Trim();
        if (idea.Length > 2000) throw new ArgumentException("Keep the idea under 2000 characters.", nameof(idea));
        Idea = idea; UpdatedAtUtc = now;
    }

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
