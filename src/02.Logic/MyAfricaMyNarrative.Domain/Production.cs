using System.Security.Cryptography;
using System.Text;

namespace MyAfricaMyNarrative.Domain;

public enum ShotStatus { Planned, Captured, Dropped }

/// <summary>A person or source credited in the finished film. Credits are written by the creator, never generated.</summary>
public sealed class CreditEntry
{
    private CreditEntry() { }
    public CreditEntry(Guid projectId, string role, string name, Guid? sourceAssetId, int order, DateTimeOffset now)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("A project is required.");
        Id = Guid.NewGuid(); ProjectId = projectId; CreatedAtUtc = now; Update(role, name, sourceAssetId, order);
    }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Role { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public Guid? SourceAssetId { get; private set; }
    public int Order { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public void Update(string role, string name, Guid? sourceAssetId, int order)
    {
        role = role.Trim(); name = name.Trim();
        if (role.Length is < 1 or > 80) throw new ArgumentException("Credit role must be 1–80 characters.");
        if (name.Length is < 1 or > 120) throw new ArgumentException("Credited name must be 1–120 characters.");
        if (order is < 0 or > 1000) throw new ArgumentException("Credit order is out of range.");
        Role = role; Name = name; SourceAssetId = sourceAssetId == Guid.Empty ? null : sourceAssetId; Order = order;
    }
}

/// <summary>The creator's approval of one exact set of credits. Any credit change produces a new hash and needs a new approval.</summary>
public sealed class CreditsApproval
{
    private CreditsApproval() { }
    public CreditsApproval(Guid projectId, string creditsHash, Guid actorUserId, DateTimeOffset now)
    {
        if (projectId == Guid.Empty || actorUserId == Guid.Empty || creditsHash.Length != 64) throw new ArgumentException("Project, approver and credits hash are required.");
        Id = Guid.NewGuid(); ProjectId = projectId; CreditsHash = creditsHash; ActorUserId = actorUserId; ApprovedAtUtc = now;
    }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string CreditsHash { get; private set; } = string.Empty;
    public Guid ActorUserId { get; private set; }
    public DateTimeOffset ApprovedAtUtc { get; private set; }
}

public static class CreditsRules
{
    /// <summary>Length of the end credits card appended to rendered films.</summary>
    public const long CardDurationMs = 5000;
    public const int MaxCredits = 30;

    public static IReadOnlyList<CreditEntry> Ordered(IEnumerable<CreditEntry> credits) =>
        credits.OrderBy(x => x.Order).ThenBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).ToList();

    public static string ComputeHash(IEnumerable<CreditEntry> credits)
    {
        var canonical = string.Join("\n", Ordered(credits).Select(x => string.Join('|', x.Role, x.Name, x.SourceAssetId)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}

/// <summary>A planned shot linked to a story scene by title. Planned footage stays missing until captured with a real upload or dropped by the creator.</summary>
public sealed class ShotPlanItem
{
    private ShotPlanItem() { }
    public ShotPlanItem(Guid projectId, string sceneTitle, string description, IEnumerable<Guid> resourceIds, bool isSuggestion, DateTimeOffset now)
    {
        sceneTitle = sceneTitle.Trim(); description = description.Trim();
        if (projectId == Guid.Empty) throw new ArgumentException("A project is required.");
        if (sceneTitle.Length is < 1 or > 120) throw new ArgumentException("Choose the scene this shot belongs to.");
        if (description.Length is < 3 or > 500) throw new ArgumentException("Describe the shot in 3–500 characters.");
        var ids = resourceIds.Where(x => x != Guid.Empty).Distinct().ToList();
        if (ids.Count > 20) throw new ArgumentException("A shot can use at most 20 resources.");
        Id = Guid.NewGuid(); ProjectId = projectId; SceneTitle = sceneTitle; Description = description;
        ResourceIdList = string.Join(',', ids); IsSuggestion = isSuggestion; Status = ShotStatus.Planned; CreatedAtUtc = now; UpdatedAtUtc = now;
    }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string SceneTitle { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    /// <summary>Comma-separated declared resource IDs. Stored flat so the plan stays readable in the preservation archive.</summary>
    public string ResourceIdList { get; private set; } = string.Empty;
    public bool IsSuggestion { get; private set; }
    public ShotStatus Status { get; private set; }
    public Guid? CapturedAssetId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public IReadOnlyList<Guid> ResourceIds => ResourceIdList.Length == 0 ? [] :
        ResourceIdList.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList();

    public bool MatchesScene(string title) => string.Equals(SceneTitle, title.Trim(), StringComparison.OrdinalIgnoreCase);

    public void MarkCaptured(Guid assetId, DateTimeOffset now)
    {
        if (assetId == Guid.Empty) throw new ArgumentException("Choose the uploaded footage that fulfils this shot.");
        Status = ShotStatus.Captured; CapturedAssetId = assetId; UpdatedAtUtc = now;
    }
    public void Drop(DateTimeOffset now) { Status = ShotStatus.Dropped; CapturedAssetId = null; UpdatedAtUtc = now; }
    public void Reopen(DateTimeOffset now) { Status = ShotStatus.Planned; CapturedAssetId = null; UpdatedAtUtc = now; }
}

public enum PlanApproach { FilmNew, UseExisting }
public enum PlanStatus { Proposed, Approved, Rejected, Superseded }

/// <summary>One scene's step in a production plan, as proposed (by the practice planner or Live AI) and reviewed by the creator.</summary>
public sealed record PlanStepDraft(Guid SceneId, string SceneTitle, PlanApproach Approach, string Instruction,
    int OnScreenSeconds, int ShootMinutes, string Purpose, IReadOnlyList<Guid> ResourceIds, Guid? ExistingAssetId);

/// <summary>
/// A versioned "make it with what I have" plan bound to one story revision and one exact resource state.
/// It is a proposal until the creator approves it; a change to the story or resources makes it stale.
/// </summary>
public sealed class ProductionPlan
{
    public const int MaxShootMinutesPerStep = 240;
    private ProductionPlan() { }
    public ProductionPlan(Guid projectId, int version, Guid storyRevisionId, string storyHash, string resourceHash, string resourceSnapshot,
        int? timeBudgetMinutes, string summary, string changeNote, string source, bool isFixture, IEnumerable<PlanStepDraft> steps, DateTimeOffset now)
    {
        if (projectId == Guid.Empty || storyRevisionId == Guid.Empty || version < 1) throw new ArgumentException("Project, story revision and version are required.");
        if (storyHash.Length != 64 || resourceHash.Length != 64) throw new ArgumentException("Plan baselines are required.");
        summary = summary.Trim(); changeNote = changeNote.Trim(); source = source.Trim();
        if (summary.Length is < 1 or > 1500 || changeNote.Length > 1500 || source.Length is < 1 or > 120 || resourceSnapshot.Length > 8000) throw new ArgumentException("Plan text is out of bounds.");
        var items = steps.ToList();
        if (items.Count is < 1 or > 50) throw new ArgumentException("A plan needs 1–50 steps.");
        Id = Guid.NewGuid(); ProjectId = projectId; Version = version; StoryRevisionId = storyRevisionId; StoryHash = storyHash; ResourceHash = resourceHash;
        ResourceSnapshot = resourceSnapshot; TimeBudgetMinutes = timeBudgetMinutes; Summary = summary; ChangeNote = changeNote; Source = source; IsFixture = isFixture;
        Status = PlanStatus.Proposed; CreatedAtUtc = now;
        for (var i = 0; i < items.Count; i++) Steps.Add(new ProductionPlanStep(Id, i + 1, items[i]));
        PlannedShootMinutes = Steps.Sum(x => x.ShootMinutes);
    }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public int Version { get; private set; }
    public Guid StoryRevisionId { get; private set; }
    public string StoryHash { get; private set; } = string.Empty;
    public string ResourceHash { get; private set; } = string.Empty;
    /// <summary>Readable resource lines captured when the plan was made, so comparisons survive later edits.</summary>
    public string ResourceSnapshot { get; private set; } = string.Empty;
    public int? TimeBudgetMinutes { get; private set; }
    public int PlannedShootMinutes { get; private set; }
    public string Summary { get; private set; } = string.Empty;
    public string ChangeNote { get; private set; } = string.Empty;
    /// <summary>"Practice planner (no AI)" or "Live OpenAI (model)".</summary>
    public string Source { get; private set; } = string.Empty;
    public bool IsFixture { get; private set; }
    public PlanStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ReviewedAtUtc { get; private set; }
    public ICollection<ProductionPlanStep> Steps { get; } = new List<ProductionPlanStep>();

    public void Approve(DateTimeOffset now) { Require(PlanStatus.Proposed); Status = PlanStatus.Approved; ReviewedAtUtc = now; ApprovedOnce = true; }
    /// <summary>True once the creator approved this plan, even after a later plan supersedes it.</summary>
    public bool ApprovedOnce { get; private set; }
    public void Reject(DateTimeOffset now) { Require(PlanStatus.Proposed); Status = PlanStatus.Rejected; ReviewedAtUtc = now; }
    public void Supersede(DateTimeOffset now) { if (Status is PlanStatus.Approved or PlanStatus.Proposed) { Status = PlanStatus.Superseded; ReviewedAtUtc ??= now; } }
    private void Require(PlanStatus status) { if (Status != status) throw new InvalidOperationException("This plan was already reviewed."); }
}

public sealed class ProductionPlanStep
{
    private ProductionPlanStep() { }
    internal ProductionPlanStep(Guid planId, int order, PlanStepDraft draft)
    {
        var title = draft.SceneTitle.Trim(); var instruction = draft.Instruction.Trim(); var purpose = draft.Purpose.Trim();
        if (draft.SceneId == Guid.Empty || title.Length is < 1 or > 120) throw new ArgumentException("Each step must belong to a scene.");
        if (instruction.Length is < 3 or > 600 || purpose.Length is < 3 or > 400) throw new ArgumentException("Step instructions and purpose must be 3–600 and 3–400 characters.");
        if (draft.OnScreenSeconds is < 1 or > 300 || draft.ShootMinutes is < 0 or > ProductionPlan.MaxShootMinutesPerStep) throw new ArgumentException("Step timing is out of bounds.");
        if (draft.Approach == PlanApproach.UseExisting && draft.ShootMinutes != 0) throw new ArgumentException("Using existing material needs no filming time.");
        var ids = draft.ResourceIds.Where(x => x != Guid.Empty).Distinct().ToList();
        if (ids.Count > 20) throw new ArgumentException("A step can use at most 20 resources.");
        Id = Guid.NewGuid(); PlanId = planId; Order = order; SceneId = draft.SceneId; SceneTitle = title; Approach = draft.Approach;
        Instruction = instruction; OnScreenSeconds = draft.OnScreenSeconds; ShootMinutes = draft.ShootMinutes; Purpose = purpose;
        ResourceIdList = string.Join(',', ids); ExistingAssetId = draft.ExistingAssetId == Guid.Empty ? null : draft.ExistingAssetId;
    }
    public Guid Id { get; private set; }
    public Guid PlanId { get; private set; }
    public int Order { get; private set; }
    public Guid SceneId { get; private set; }
    public string SceneTitle { get; private set; } = string.Empty;
    public PlanApproach Approach { get; private set; }
    public string Instruction { get; private set; } = string.Empty;
    public int OnScreenSeconds { get; private set; }
    public int ShootMinutes { get; private set; }
    public string Purpose { get; private set; } = string.Empty;
    public string ResourceIdList { get; private set; } = string.Empty;
    public Guid? ExistingAssetId { get; private set; }
    public IReadOnlyList<Guid> ResourceIds => ResourceIdList.Length == 0 ? [] :
        ResourceIdList.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList();
}
