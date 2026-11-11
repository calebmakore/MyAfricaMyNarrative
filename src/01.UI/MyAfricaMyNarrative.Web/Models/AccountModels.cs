using System.ComponentModel.DataAnnotations;

namespace MyAfricaMyNarrative.Web.Models;

public sealed class RegisterViewModel
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 10), DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
}
public sealed class LoginViewModel
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
}
public sealed class ForgotPasswordViewModel
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
}
public sealed class ResetPasswordViewModel
{
    [Required] public Guid UserId { get; set; }
    [Required, StringLength(2048)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 10), DataType(DataType.Password), Display(Name = "New password")] public string Password { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "The passwords do not match."), Display(Name = "Confirm new password")] public string ConfirmPassword { get; set; } = string.Empty;
}
public sealed class ProjectEditViewModel
{
    [Required, StringLength(120, MinimumLength = 1)] public string Title { get; set; } = string.Empty;
    public MyAfricaMyNarrative.Domain.StoryType StoryType { get; set; }
    [StringLength(2000)] public string? Idea { get; set; }
}
public sealed class UploadViewModel
{
    [Required] public IFormFile? File { get; set; }
    [Required, StringLength(120, MinimumLength = 1)] public string DisplayName { get; set; } = string.Empty;
    [StringLength(300)] public string? Attribution { get; set; }
    [Range(typeof(bool), "true", "true", ErrorMessage = "Confirm that you have permission to use this source.")]
    public bool PermissionConfirmed { get; set; }
    [StringLength(120)] public string? SuppliedBy { get; set; }
    public MyAfricaMyNarrative.Domain.ContributorUse ContributorUse { get; set; } = MyAfricaMyNarrative.Domain.ContributorUse.NotYetAsked;
}
public sealed class StoryBasicsViewModel
{
    public MyAfricaMyNarrative.Domain.StoryType StoryType { get; set; }
    [StringLength(2000)] public string? Idea { get; set; }
}
public sealed class ContributorViewModel
{
    [StringLength(120)] public string? SuppliedBy { get; set; }
    public MyAfricaMyNarrative.Domain.ContributorUse ContributorUse { get; set; }
    [StringLength(300)] public string? Note { get; set; }
}
public sealed class GlossaryViewModel
{
    [Required, StringLength(80, MinimumLength = 2)] public string Term { get; set; } = string.Empty;
    [StringLength(40)] public string? Language { get; set; }
    [Required, StringLength(160, MinimumLength = 2)] public string Explanation { get; set; } = string.Empty;
}
public sealed class SourceRenameViewModel
{
    [Required, StringLength(120, MinimumLength = 1)] public string DisplayName { get; set; } = string.Empty;
}
