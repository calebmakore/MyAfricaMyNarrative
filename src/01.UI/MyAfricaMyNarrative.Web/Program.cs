using Microsoft.AspNetCore.Http.Features;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using MyAfricaMyNarrative.Web.Controllers;
using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Infrastructure;
using MyAfricaMyNarrative.Infrastructure.Identity;
using MyAfricaMyNarrative.Infrastructure.Persistence;
using MyAfricaMyNarrative.Web.Services;

// Browsers always submit number fields with a dot ("12.5"), whatever the server's locale. Hosts in comma-decimal locales
// (for example en-ZA) would otherwise reject or misread every timing field, so the app reads and writes numbers invariantly.
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;
System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
var keyPath = Path.GetFullPath(builder.Configuration["Security:DataProtectionKeysPath"] ?? "App_Data/data-protection-keys");
Directory.CreateDirectory(keyPath);
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keyPath)).SetApplicationName("MyAfricaMyNarrative");
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddInfrastructure(builder.Configuration);
// Render exports in the background inside the web app so creators never start a separate process.
// Set Jobs:ProcessInWeb to false when a dedicated Worker should do all rendering.
if (builder.Configuration.GetValue("Jobs:ProcessInWeb", true))
    builder.Services.AddHostedService<MyAfricaMyNarrative.Infrastructure.Jobs.ExportJobProcessor>();
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 10;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();
var resetLinkMinutes = Math.Clamp(builder.Configuration.GetValue<int?>("Email:ResetLinkLifetimeMinutes") ?? 60, 5, 1440);
builder.Services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromMinutes(resetLinkMinutes));
// A password reset rotates the security stamp; other signed-in sessions are rejected at the next validation.
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(5));
builder.Services.ConfigureApplicationCookie(options => options.LoginPath = "/Account/Login");
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        // Send people back to the form with an explanation instead of a blank error page.
        context.HttpContext.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.HttpContext.Response.Headers.Location = "/Account/ForgotPassword?limited=true";
        return ValueTask.CompletedTask;
    };
    // Bounds reset emails per client address; the per-account resend window is enforced in AccountController.
    options.AddPolicy(AccountController.PasswordResetRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(15), QueueLimit = 0 }));
});
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddControllersWithViews();
var maxUploadBytes = builder.Configuration.GetValue<long?>("Media:MaxUploadBytes") ?? 20 * 1024 * 1024;
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = maxUploadBytes);

var app = builder.Build();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }
app.UseStatusCodePagesWithReExecute("/Home/Status", "?code={0}");
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["X-Frame-Options"] = "DENY";
    // Camera and microphone are used only by this site's own recorders.
    headers["Permissions-Policy"] = "camera=(self), microphone=(self), geolocation=(), payment=()";
    headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; " +
        "media-src 'self' blob:; font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
    await next();
});
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}").WithStaticAssets();
app.Run();

public partial class Program;
