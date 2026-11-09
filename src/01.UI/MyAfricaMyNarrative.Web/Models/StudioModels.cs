using System.ComponentModel.DataAnnotations;
using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;

namespace MyAfricaMyNarrative.Web.Models;

public sealed record StudioViewModel(Project Project, StoryRevision? CurrentRevision, ExportReadiness Readiness,
    IReadOnlyList<Job> Jobs, IReadOnlyList<ExportArtifact> Artifacts, string AiMode);

public sealed class TranscriptEditViewModel
{
    [Required] public Guid AssetId { get; set; }
    [Required, StringLength(12000, MinimumLength = 1)] public string Text { get; set; } = string.Empty;
    [Required, StringLength(40, MinimumLength = 2)] public string Language { get; set; } = "English";
    [Range(0, 300000)] public long StartMs { get; set; }
    [Range(1, 300000)] public long EndMs { get; set; }
}

public sealed class SceneEditViewModel
{
    [Required, StringLength(120)] public string Title { get; set; } = string.Empty;
    [StringLength(4000)] public string Text { get; set; } = string.Empty;
    public TextOrigin TextOrigin { get; set; }
    [Required] public Guid AudioAssetId { get; set; }
    [Required] public Guid VisualAssetId { get; set; }
    public Guid? TranscriptRevisionId { get; set; }
    [Range(0, 300000)] public long StartMs { get; set; }
    [Range(1, 300000)] public long EndMs { get; set; }
    [StringLength(2000)] public string Caption { get; set; } = string.Empty;
}

public sealed class ResourceEditViewModel
{
    public ResourceKind Kind { get; set; }
    [Required, StringLength(120)] public string Name { get; set; } = string.Empty;
    [StringLength(300)] public string Availability { get; set; } = string.Empty;
}
