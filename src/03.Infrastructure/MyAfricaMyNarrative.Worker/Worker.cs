using Microsoft.Extensions.Options;
using MyAfricaMyNarrative.Application;

namespace MyAfricaMyNarrative.Worker;

public class Worker(ILogger<Worker> logger, IServiceScopeFactory scopes, IOptions<JobOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workerId = $"{Environment.MachineName}:{Environment.ProcessId}";
        while (!stoppingToken.IsCancellationRequested)
        {
            ClaimedJob? claim = null;
            try
            {
                using var scope = scopes.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<IJobStore>();
                claim = await store.ClaimAsync(workerId, TimeSpan.FromSeconds(options.Value.LeaseSeconds), stoppingToken);
                if (claim is null) { await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken); continue; }
                logger.LogInformation("Rendering job {JobId} for project {ProjectId}.", claim.Id, claim.ProjectId);
                var artifacts = await scope.ServiceProvider.GetRequiredService<IExportJobHandler>().RenderAsync(claim, stoppingToken);
                await store.CompleteAsync(claim, artifacts, stoppingToken);
                logger.LogInformation("Completed job {JobId} with {ArtifactCount} artifacts.", claim.Id, artifacts.Count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogError(exception, "Export job {JobId} failed.", claim?.Id);
                if (claim is not null)
                {
                    try
                    {
                        using var failureScope = scopes.CreateScope();
                        await failureScope.ServiceProvider.GetRequiredService<IJobStore>().FailAsync(claim, exception.Message,
                            DateTimeOffset.UtcNow.AddSeconds(Math.Min(60, 5 * Math.Max(1, options.Value.MaxAttempts))), options.Value.MaxAttempts, stoppingToken);
                    }
                    catch (Exception failure) { logger.LogError(failure, "Could not record failure for job {JobId}.", claim.Id); }
                }
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }
}
