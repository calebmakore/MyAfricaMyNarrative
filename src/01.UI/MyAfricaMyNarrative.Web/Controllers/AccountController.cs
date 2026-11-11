using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MyAfricaMyNarrative.Infrastructure.Email;
using MyAfricaMyNarrative.Infrastructure.Identity;
using MyAfricaMyNarrative.Web.Models;

namespace MyAfricaMyNarrative.Web.Controllers;

public sealed class AccountController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    IAccountEmailSender email,
    IOptions<EmailOptions> emailOptions,
    IMemoryCache cache,
    IWebHostEnvironment environment,
    ILogger<AccountController> logger) : Controller
{
    public const string PasswordResetRateLimit = "password-reset";
    private static readonly TimeSpan PerAccountResendWindow = TimeSpan.FromMinutes(2);

    [HttpGet] public IActionResult Register() => View(new RegisterViewModel());
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = new ApplicationUser { UserName = model.Email, Email = model.Email };
        var result = await users.CreateAsync(user, model.Password);
        if (result.Succeeded) { await signIn.SignInAsync(user, false); return RedirectToAction("Index", "Projects"); }
        foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
        return View(model);
    }

    [HttpGet] public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await signIn.PasswordSignInAsync(model.Email, model.Password, false, lockoutOnFailure: true);
        if (result.Succeeded) return LocalRedirect(model.ReturnUrl ?? Url.Action("Index", "Projects")!);
        ModelState.AddModelError(string.Empty, "Email or password was not recognised.");
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout() { await signIn.SignOutAsync(); return RedirectToAction("Index", "Home"); }

    [HttpGet] public IActionResult ForgotPassword(bool limited = false)
    {
        if (limited) ModelState.AddModelError(string.Empty, "Too many reset requests from this connection. Wait 15 minutes, then try again.");
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting(PasswordResetRateLimit)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);
        // The response is identical whether or not the address has an account, so the page cannot be used to discover accounts.
        var user = await users.FindByEmailAsync(model.Email);
        if (user?.Email is not null && cache.TryGetValue(ResendKey(user.Id), out _) is false)
        {
            var baseUrl = PublicBaseUrl();
            if (baseUrl is null)
            {
                logger.LogError("Password reset requested but Email:PublicBaseUrl is not configured outside Development; no email was sent.");
            }
            else
            {
                var token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await users.GeneratePasswordResetTokenAsync(user)));
                var path = Url.Action(nameof(ResetPassword), "Account", new { userId = user.Id, code = token })!;
                try
                {
                    await email.SendPasswordResetAsync(user.Email, baseUrl + path, cancellationToken);
                    cache.Set(ResendKey(user.Id), true, PerAccountResendWindow);
                }
                catch (Exception ex) when (ex is System.Net.Mail.SmtpException or IOException or InvalidOperationException)
                {
                    // Never log the link or token.
                    logger.LogError("Password reset email could not be delivered ({ExceptionType}).", ex.GetType().Name);
                }
            }
        }
        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    [HttpGet] public IActionResult ForgotPasswordConfirmation()
    {
        ViewData["LinkMinutes"] = emailOptions.Value.ResetLinkLifetimeMinutes;
        ViewData["PickupMode"] = environment.IsDevelopment() && emailOptions.Value.Mode == "Pickup";
        return View();
    }

    [HttpGet]
    public IActionResult ResetPassword(Guid? userId, string? code)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (userId is null || string.IsNullOrWhiteSpace(code)) return View("ResetPasswordInvalid");
        return View(new ResetPasswordViewModel { UserId = userId.Value, Code = code });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (!ModelState.IsValid) return View(model);
        var user = await users.FindByIdAsync(model.UserId.ToString());
        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Code)); }
        catch (FormatException) { return View("ResetPasswordInvalid"); }
        if (user is null) return View("ResetPasswordInvalid");

        var result = await users.ResetPasswordAsync(user, token, model.Password);
        if (result.Succeeded)
        {
            // Proving control of the email address also clears an earlier sign-in lockout.
            await users.ResetAccessFailedCountAsync(user);
            await users.SetLockoutEndDateAsync(user, null);
            cache.Remove(ResendKey(user.Id));
            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }
        if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken))) return View("ResetPasswordInvalid");
        foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
        return View(model);
    }

    [HttpGet] public IActionResult ResetPasswordConfirmation() => View();

    private static string ResendKey(Guid userId) => $"password-reset:{userId:N}";

    /// <summary>
    /// Reset links use a configured public address so a forged Host header cannot redirect a token elsewhere.
    /// The request address is accepted only in Development.
    /// </summary>
    private string? PublicBaseUrl()
    {
        var configured = emailOptions.Value.PublicBaseUrl;
        if (!string.IsNullOrWhiteSpace(configured)) return configured.TrimEnd('/');
        return environment.IsDevelopment() ? $"{Request.Scheme}://{Request.Host}" : null;
    }
}
