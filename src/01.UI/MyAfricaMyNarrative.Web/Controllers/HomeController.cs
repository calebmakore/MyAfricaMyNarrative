using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MyAfricaMyNarrative.Web.Models;

namespace MyAfricaMyNarrative.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Projects") : View();

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Help() => View();

    /// <summary>Friendly page for 404 and other status codes (re-executed by the status-code middleware).</summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Status(int code)
    {
        ViewData["Code"] = code is >= 400 and <= 599 ? code : 404;
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
