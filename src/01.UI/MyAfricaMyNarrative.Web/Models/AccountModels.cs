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
public sealed class ProjectEditViewModel
{
    [Required, StringLength(120, MinimumLength = 1)] public string Title { get; set; } = string.Empty;
}
public sealed class UploadViewModel
{
    [Required] public IFormFile? File { get; set; }
    [StringLength(300)] public string Attribution { get; set; } = string.Empty;
    [Range(typeof(bool), "true", "true", ErrorMessage = "Confirm that you have permission to use this source.")]
    public bool PermissionConfirmed { get; set; }
}
