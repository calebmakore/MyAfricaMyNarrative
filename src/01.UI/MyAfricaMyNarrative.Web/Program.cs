using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.DataProtection;
using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Infrastructure;
using MyAfricaMyNarrative.Infrastructure.Identity;
using MyAfricaMyNarrative.Infrastructure.Persistence;
using MyAfricaMyNarrative.Web.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
var keyPath = Path.GetFullPath(builder.Configuration["Security:DataProtectionKeysPath"] ?? "App_Data/data-protection-keys");
Directory.CreateDirectory(keyPath);
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keyPath)).SetApplicationName("MyAfricaMyNarrative");
builder.Services.AddHttpContextAccessor();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 10;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.ConfigureApplicationCookie(options => options.LoginPath = "/Account/Login");
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddControllersWithViews();
var maxUploadBytes = builder.Configuration.GetValue<long?>("Media:MaxUploadBytes") ?? 20 * 1024 * 1024;
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = maxUploadBytes);

var app = builder.Build();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.MapControllerRoute(name: "default", pattern: "{controller=Projects}/{action=Index}/{id?}").WithStaticAssets();
app.Run();

public partial class Program;
