using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MyAfricaMyNarrative.Infrastructure.Identity;
using MyAfricaMyNarrative.Web.Models;

namespace MyAfricaMyNarrative.Web.Controllers;

public sealed class AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn) : Controller
{
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
}
