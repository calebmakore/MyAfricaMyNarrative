using System.ComponentModel.DataAnnotations;
using MyAfricaMyNarrative.Application;

namespace MyAfricaMyNarrative.Web.Models;

public sealed class SlideViewModel
{
    [Required, StringLength(80, MinimumLength = 1)] public string Title { get; set; } = string.Empty;
    [StringLength(140)] public string? Subtitle { get; set; }
    public SlideTheme Theme { get; set; } = SlideTheme.Night;
}
