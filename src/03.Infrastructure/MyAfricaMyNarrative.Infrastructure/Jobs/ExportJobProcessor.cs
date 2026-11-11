using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyAfricaMyNarrative.Application;

namespace MyAfricaMyNarrative.Infrastructure.Jobs;

/// <summary>
/// Claims queued export jobs from the durable SQL store and renders them, one at a time per process.
/// Hosted by the web app (so exports run without anyone starting a separate process) and by the standalone Worker.
/// While a render runs its lease is renewed; if the lease is lost the render is cancelled so the job never renders twice.
/// </summary>
public sealed class ExportJobProcessor(ILogger<ExportJobProcessor> logger, IServiceScopeFactory scopes, IOptions<JobOptions> options) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(3);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workerId = $"{Environment.MachineName}:{Environment.ProcessId}";
        // The app validates 30–3600 s at startup (see AddInfrastructure); tests pass shorter leases directly.
        var lease = TimeSpan.FromSeconds(Math.Clamp(options.Value.LeaseSeconds, 1, 3600));
        while (!stoppingToken.IsCancellationRequested)
        {
            ClaimedJob? claim = null;
            try
            {
                using var scope = scopes.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<IJobStore>();
                claim = await store.ClaimAsync(workerId, lease, stoppingToken);
                if (claim is null) { await Task.Delay(IdleDelay, stoppingToken); continue; }
                logger.LogInformation("Rendering job {JobId} for project {ProjectId}.", claim.Id, claim.ProjectId);

                using var render = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                var renewal = KeepLeaseAsync(claim, lease, render, stoppingToken);
                IReadOnlyList<MyAfricaMyNarrative.Domain.ExportArtifact> artifacts;
                try { artifacts = await scope.ServiceProvider.GetRequiredService<IExportJobHandler>().RenderAsync(claim, render.Token); }
                finally { await render.CancelAsync(); await renewal; }
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
                    catch (Exception failure) { logger.LogWarning(failure, "Could not record failure for job {JobId}; it may have been taken over.", claim.Id); }
                }
                try { await Task.Delay(IdleDelay, stoppingToken); } catch (OperationCanceledException) { break; }
            }
        }
    }

    /// <summary>Renews the lease at a third of its length until the render ends; cancels the render if the lease is lost.</summary>
    private async Task KeepLeaseAsync(ClaimedJob claim, TimeSpan lease, CancellationTokenSource render, CancellationToken stoppingToken)
    {
        var interval = lease / 3;
        try
        {
            while (!render.IsCancellationRequested)
            {
                await Task.Delay(interval, render.Token);
                using var scope = scopes.CreateScope();
                if (!await scope.ServiceProvider.GetRequiredService<IJobStore>().RenewAsync(claim, lease, stoppingToken))
                {
                    logger.LogWarning("Job {JobId} lost its lease; stopping this render.", claim.Id);
                    await render.CancelAsync(); return;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { logger.LogWarning(exception, "Could not renew the lease for job {JobId}.", claim.Id); }
    }
}
