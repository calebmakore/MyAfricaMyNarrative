using System.ComponentModel.DataAnnotations;
using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;

namespace MyAfricaMyNarrative.Web.Models;

public sealed record StudioViewModel(Project Project, StoryRevision? CurrentRevision, ExportReadiness Readiness,
    IReadOnlyList<Job> Jobs, IReadOnlyList<ExportArtifact> Artifacts, string AiMode, StudioProgress Progress);

public sealed record RecordViewModel(Project Project, StudioProgress Progress);

public sealed record StudioHeaderModel(Project Project, StudioProgress Progress, StudioStage Active);

/// <summary>Forms post friendly seconds (e.g. 12.5); the server keeps exact milliseconds.</summary>
public static class Seconds
{
    public static long ToMs(decimal value) => (long)Math.Round(value * 1000m, MidpointRounding.AwayFromZero);
    public static string FromMs(long ms) => (ms / 1000m).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}

public sealed class TranscriptEditViewModel
{
    [Required] public Guid AssetId { get; set; }
    [Required, StringLength(12000, MinimumLength = 1)] public string Text { get; set; } = string.Empty;
    [Required, StringLength(40, MinimumLength = 2)] public string Language { get; set; } = "English";
    [Range(0, 300000)] public long StartMs { get; set; }
    [Range(1, 300000)] public long EndMs { get; set; }
    public decimal? StartSeconds { get => null; set { if (value is decimal v) StartMs = Seconds.ToMs(v); } }
    public decimal? EndSeconds { get => null; set { if (value is decimal v) EndMs = Seconds.ToMs(v); } }
}

public sealed class TranscriptionReviewViewModel
{
    [Required] public Guid ProposalId { get; set; }
    [Required, StringLength(40, MinimumLength = 2)] public string Language { get; set; } = "English";
    [Range(0, 300000)] public long StartMs { get; set; }
    [Range(1, 300000)] public long EndMs { get; set; }
    public decimal? StartSeconds { get => null; set { if (value is decimal v) StartMs = Seconds.ToMs(v); } }
    public decimal? EndSeconds { get => null; set { if (value is decimal v) EndMs = Seconds.ToMs(v); } }
}

public sealed class CreatorAnswerViewModel
{
    [Required] public Guid QuestionId { get; set; }
    [Required, StringLength(1000, MinimumLength = 1)] public string Answer { get; set; } = string.Empty;
}

public sealed class SceneEditViewModel
{
    [Required, StringLength(120)] public string Title { get; set; } = string.Empty;
    [DisplayFormat(ConvertEmptyStringToNull = false), StringLength(4000)] public string Text { get; set; } = string.Empty;
    public TextOrigin TextOrigin { get; set; }
    [Required] public Guid AudioAssetId { get; set; }
    [Required] public Guid VisualAssetId { get; set; }
    public Guid? TranscriptRevisionId { get; set; }
    [Range(0, 300000)] public long StartMs { get; set; }
    [Range(1, 300000)] public long EndMs { get; set; }
    public decimal? StartSeconds { get => null; set { if (value is decimal v) StartMs = Seconds.ToMs(v); } }
    public decimal? EndSeconds { get => null; set { if (value is decimal v) EndMs = Seconds.ToMs(v); } }
    [DisplayFormat(ConvertEmptyStringToNull = false), StringLength(2000)] public string Caption { get; set; } = string.Empty;
    [DisplayFormat(ConvertEmptyStringToNull = false), StringLength(2000)] public string Translation { get; set; } = string.Empty;
    /// <summary>Extra pictures shown in turn under the same voice.</summary>
    public List<Guid> MorePictureIds { get; set; } = [];
    public bool KeepClipSound { get; set; }
    [Range(0, 300000)] public long ClipStartMs { get; set; }
    public decimal? ClipStartSeconds { get => null; set { if (value is decimal v) ClipStartMs = Seconds.ToMs(v); } }
}

public sealed class CreditEditViewModel
{
    [Required, StringLength(80, MinimumLength = 1)] public string Role { get; set; } = string.Empty;
    [Required, StringLength(120, MinimumLength = 1)] public string Name { get; set; } = string.Empty;
    public Guid? SourceAssetId { get; set; }
    [Range(0, 1000)] public int Order { get; set; }
}

public sealed class ShotEditViewModel
{
    [Required, StringLength(120, MinimumLength = 1)] public string SceneTitle { get; set; } = string.Empty;
    [Required, StringLength(500, MinimumLength = 3)] public string Description { get; set; } = string.Empty;
    public List<Guid> ResourceIds { get; set; } = [];
}

public sealed class PilotFeedbackViewModel
{
    public IntentMatch IntentMatch { get; set; }
    public List<HelpArea> NeededHelp { get; set; } = [];
    [StringLength(500)] public string? HelpNote { get; set; }
    [StringLength(1000)] public string? Comment { get; set; }
}

/// <summary>Extra pictures and clip-sound options shared by the add and edit scene forms.</summary>
public sealed record ScenePictureOptionsModel(IReadOnlyList<MediaAsset> Visuals, IReadOnlyList<Guid> Selected, bool KeepClipSound, long ClipStartMs, string IdPrefix);

public sealed record PlanStepViewModel(ProductionPlanStep? Step, bool Changed, IReadOnlyDictionary<Guid, string> ResourceNames, IReadOnlyDictionary<Guid, string> AssetNames);

public sealed class ResourceEditViewModel
{
    public ResourceKind Kind { get; set; }
    [Required, StringLength(120)] public string Name { get; set; } = string.Empty;
    [DisplayFormat(ConvertEmptyStringToNull = false), StringLength(300)] public string Availability { get; set; } = string.Empty;
    /// <summary>Filming hours, for time resources only.</summary>
    [Range(0.1, 168)] public double? Hours { get; set; }
}

public sealed class LanguageReviewViewModel
{
    [Required, StringLength(120, MinimumLength = 2)] public string ReviewerName { get; set; } = string.Empty;
    [Required, StringLength(40, MinimumLength = 2)] public string Language { get; set; } = string.Empty;
    [Required, StringLength(200, MinimumLength = 2)] public string FluencyBasis { get; set; } = string.Empty;
    public bool IsSelfReview { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false), StringLength(1000)] public string Notes { get; set; } = string.Empty;
    [Range(typeof(bool), "true", "true", ErrorMessage = "The reviewer must confirm the attestation.")] public bool Attested { get; set; }
}

public sealed class CreatorIntentViewModel
{
    [DisplayFormat(ConvertEmptyStringToNull = false), StringLength(500)] public string Audience { get; set; } = string.Empty;
    [DisplayFormat(ConvertEmptyStringToNull = false), StringLength(1000)] public string Meaning { get; set; } = string.Empty;
    [DisplayFormat(ConvertEmptyStringToNull = false), StringLength(200)] public string Tone { get; set; } = string.Empty;
    [DisplayFormat(ConvertEmptyStringToNull = false), StringLength(1500)] public string LocalContext { get; set; } = string.Empty;
    [DisplayFormat(ConvertEmptyStringToNull = false), StringLength(1000)] public string ProtectedTerms { get; set; } = string.Empty;
    [DisplayFormat(ConvertEmptyStringToNull = false), StringLength(40)] public string TranslationLanguage { get; set; } = string.Empty;
}
