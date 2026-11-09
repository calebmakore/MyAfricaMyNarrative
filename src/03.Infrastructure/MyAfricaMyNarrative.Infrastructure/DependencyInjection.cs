using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Infrastructure.Media;
using MyAfricaMyNarrative.Infrastructure.Jobs;
using MyAfricaMyNarrative.Infrastructure.Persistence;
using MyAfricaMyNarrative.Infrastructure.Storage;

namespace MyAfricaMyNarrative.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required. Configure a dedicated SQL Server database; credentials belong in user secrets or environment variables.");

        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IProjectRepository>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IJobStore, SqlJobStore>();
        services.AddScoped<IExportJobHandler, FfmpegExportJobHandler>();
        services.AddSingleton<IPrivateMediaStorage, LocalPrivateMediaStorage>();
        services.AddSingleton<IMediaInspector, SignatureMediaInspector>();
        services.AddSingleton<IMediaProbe, FfprobeMediaProbe>();
        services.AddSingleton<IExportReadinessService, ExportReadinessService>();
        services.AddOptions<MediaOptions>().Bind(configuration.GetSection(MediaOptions.SectionName)).Validate(x => x.MaxUploadBytes > 0, "Media:MaxUploadBytes must be positive.").ValidateOnStart();
        services.AddOptions<AiOptions>().Bind(configuration.GetSection(AiOptions.SectionName)).Validate(x => x.Mode is "Manual" or "Live" or "Fixture", "Ai:Mode must be Manual, Live, or Fixture.").ValidateOnStart();
        services.AddOptions<JobOptions>().Bind(configuration.GetSection(JobOptions.SectionName)).Validate(x => x.MaxAttempts > 0 && x.LeaseSeconds > 0, "Jobs limits must be positive.").ValidateOnStart();
        return services;
    }
}
