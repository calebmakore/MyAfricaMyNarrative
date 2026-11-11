using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Infrastructure.Media;
using MyAfricaMyNarrative.Infrastructure.Jobs;
using MyAfricaMyNarrative.Infrastructure.Persistence;
using MyAfricaMyNarrative.Infrastructure.Storage;
using MyAfricaMyNarrative.Infrastructure.Ai;

namespace MyAfricaMyNarrative.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required. Configure a dedicated SQL Server database; credentials belong in user secrets or environment variables.");

        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
        services.AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlServer(connectionString), ServiceLifetime.Scoped);
        services.AddScoped<IProjectRepository>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IJobStore, SqlJobStore>();
        services.AddScoped<IExportJobHandler, FfmpegExportJobHandler>();
        services.AddSingleton<IPrivateMediaStorage, LocalPrivateMediaStorage>();
        services.AddSingleton<IMediaInspector, SignatureMediaInspector>();
        services.AddSingleton<IMediaProbe, FfprobeMediaProbe>();
        services.AddSingleton<IExportReadinessService, ExportReadinessService>();
        services.AddSingleton<IStoryAssistant>(provider =>
        {
            var ai = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiOptions>>().Value;
            var client = new HttpClient
            {
                BaseAddress = new Uri("https://api.openai.com/"),
                Timeout = TimeSpan.FromSeconds(Math.Clamp(ai.RequestTimeoutSeconds, 5, 120))
            };
            return new OpenAiStoryAssistant(client, provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiOptions>>());
        });
        services.AddSingleton<IStoryCoach>(provider =>
        {
            var ai = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiOptions>>().Value;
            var client = new HttpClient { BaseAddress = new Uri("https://api.openai.com/"), Timeout = TimeSpan.FromSeconds(Math.Clamp(ai.RequestTimeoutSeconds, 5, 120)) };
            return new OpenAiStoryCoach(client, provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiOptions>>());
        });
        services.AddSingleton<IGlossaryAssistant>(provider =>
        {
            var ai = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiOptions>>().Value;
            var client = new HttpClient { BaseAddress = new Uri("https://api.openai.com/"), Timeout = TimeSpan.FromSeconds(Math.Clamp(ai.RequestTimeoutSeconds, 5, 120)) };
            return new OpenAiGlossaryAssistant(client, provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiOptions>>());
        });
        services.AddSingleton<IProductionPlanAssistant>(provider =>
        {
            var ai = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiOptions>>().Value;
            var client = new HttpClient { BaseAddress = new Uri("https://api.openai.com/"), Timeout = TimeSpan.FromSeconds(Math.Clamp(ai.RequestTimeoutSeconds, 5, 120)) };
            return new OpenAiProductionPlanner(client, provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiOptions>>());
        });
        services.AddSingleton<IAudioTranscriber>(provider =>
        {
            var ai = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiOptions>>().Value;
            var client = new HttpClient { BaseAddress = new Uri("https://api.openai.com/"), Timeout = TimeSpan.FromSeconds(Math.Clamp(ai.RequestTimeoutSeconds, 5, 120)) };
            return new OpenAiAudioTranscriber(client, provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiOptions>>());
        });
        services.AddOptions<MediaOptions>().Bind(configuration.GetSection(MediaOptions.SectionName)).Validate(x => x.MaxUploadBytes > 0, "Media:MaxUploadBytes must be positive.").ValidateOnStart();
        services.AddOptions<AiOptions>().Bind(configuration.GetSection(AiOptions.SectionName))
            .Validate(x => x.Mode is "Manual" or "Live" or "Fixture", "Ai:Mode must be Manual, Live, or Fixture.")
            .Validate(x => x.Mode != "Live" || (!string.IsNullOrWhiteSpace(x.ApiKey) && !string.IsNullOrWhiteSpace(x.StoryModel) && !string.IsNullOrWhiteSpace(x.TranscriptionModel)), "Live AI requires a key and selected story/transcription models.")
            .Validate(x => x.MaxCallsPerProject is > 0 and <= 100 && x.RequestTimeoutSeconds is >= 5 and <= 120, "AI call and timeout bounds are invalid.").ValidateOnStart();
        services.AddSingleton<Email.IAccountEmailSender, Email.SmtpAccountEmailSender>();
        services.AddOptions<Email.EmailOptions>().Bind(configuration.GetSection(Email.EmailOptions.SectionName))
            .Validate(x => x.Mode is "Pickup" or "Smtp", "Email:Mode must be Pickup or Smtp.")
            .Validate(x => x.Mode != "Smtp" || (!string.IsNullOrWhiteSpace(x.SmtpHost) && x.SmtpPort is > 0 and <= 65535), "Email:Mode Smtp requires Email:SmtpHost and a valid Email:SmtpPort.")
            .Validate(x => System.Net.Mail.MailAddress.TryCreate(x.FromAddress, out _), "Email:FromAddress must be a valid address.")
            .Validate(x => string.IsNullOrWhiteSpace(x.PublicBaseUrl) || (Uri.TryCreate(x.PublicBaseUrl, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps), "Email:PublicBaseUrl must be an absolute https URL.")
            .Validate(x => x.ResetLinkLifetimeMinutes is >= 5 and <= 1440 && x.TimeoutSeconds is >= 5 and <= 120, "Email link lifetime or timeout bounds are invalid.").ValidateOnStart();
        services.AddOptions<JobOptions>().Bind(configuration.GetSection(JobOptions.SectionName)).Validate(x => x.MaxAttempts is > 0 and <= 10 && x.LeaseSeconds is >= 30 and <= 3600, "Jobs:MaxAttempts must be 1–10 and Jobs:LeaseSeconds 30–3600.").ValidateOnStart();
        return services;
    }
}
