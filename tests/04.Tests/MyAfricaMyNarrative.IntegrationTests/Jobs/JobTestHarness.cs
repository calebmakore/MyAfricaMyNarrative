using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;
using MyAfricaMyNarrative.Infrastructure.Jobs;
using MyAfricaMyNarrative.Infrastructure.Persistence;

namespace MyAfricaMyNarrative.IntegrationTests.Jobs;

/// <summary>
/// A throwaway SQL Server database created from the real migrations and dropped afterwards. It never touches the dev database.
/// Server: MAMN_TEST_SQL_SERVER, defaulting to localhost\SQLEXPRESS. If no server is reachable the tests fail with a clear
/// message rather than silently passing.
/// </summary>
public sealed class SqlJobDatabase : IAsyncLifetime
{
    private static readonly string Server = Environment.GetEnvironmentVariable("MAMN_TEST_SQL_SERVER") is { Length: > 0 } s ? s : @"localhost\SQLEXPRESS";

    public string ConnectionString { get; } =
        $"Server={Server};Database=MAMN_JobTests_{Guid.NewGuid():N};Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=10";

    public ApplicationDbContext NewContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(ConnectionString).Options);

    public async Task InitializeAsync()
    {
        try { await using var db = NewContext(); await db.Database.MigrateAsync(); }
        catch (SqlException exception)
        {
            throw new InvalidOperationException("Job safety tests need a SQL Server they can create a throwaway database on. " +
                "Set MAMN_TEST_SQL_SERVER (default localhost\\SQLEXPRESS). " + exception.Message, exception);
        }
    }

    public async Task DisposeAsync() { await using var db = NewContext(); await db.Database.EnsureDeletedAsync(); }

    public async Task ResetAsync()
    {
        await using var db = NewContext();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM ExportArtifacts; DELETE FROM Jobs;");
    }

    public async Task<Guid> QueueAsync()
    {
        await using var db = NewContext();
        var job = new Job(Guid.NewGuid(), Guid.NewGuid(), "RenderPreview", Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"), "{}", DateTimeOffset.UtcNow);
        db.Jobs.Add(job); await db.SaveChangesAsync(); return job.Id;
    }

    public async Task<Job> JobAsync(Guid id) { await using var db = NewContext(); return await db.Jobs.AsNoTracking().SingleAsync(x => x.Id == id); }

    public async Task<int> ArtifactCountAsync(Guid jobId) { await using var db = NewContext(); return await db.ExportArtifacts.CountAsync(x => x.JobId == jobId); }

    public static async Task WaitForAsync(Func<Task<bool>> condition, TimeSpan timeout, string what)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until) { if (await condition()) return; await Task.Delay(150); }
        throw new TimeoutException($"Timed out after {timeout.TotalSeconds:0} s waiting for: {what}");
    }
}

/// <summary>
/// Stands in for FFmpeg: waits, reports progress through the real job store, and saves one artifact.
/// Records every attempt so tests can prove a job was never rendered twice at once and never saved twice.
/// </summary>
public sealed class FakeRenderer
{
    public Func<int, TimeSpan> Duration { get; set; } = _ => TimeSpan.FromSeconds(1);
    public int ProgressSteps { get; set; } = 4;
    public ConcurrentBag<Guid> Started { get; } = [];
    public ConcurrentBag<Guid> Cancelled { get; } = [];
    public ConcurrentBag<Guid> Finished { get; } = [];
    private readonly ConcurrentDictionary<Guid, int> active = new();
    private int renders;
    public int MaxOverlapPerJob;

    public async Task<IReadOnlyList<ExportArtifact>> RenderAsync(ClaimedJob claim, IServiceScopeFactory scopes, CancellationToken ct)
    {
        var number = Interlocked.Increment(ref renders);
        Started.Add(claim.AttemptToken);
        var overlap = active.AddOrUpdate(claim.Id, 1, (_, n) => n + 1);
        InterlockedMax(ref MaxOverlapPerJob, overlap);
        try
        {
            var step = Duration(number) / ProgressSteps;
            for (var i = 0; i < ProgressSteps; i++)
            {
                await Task.Delay(step, ct);
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IJobStore>().ReportProgressAsync(claim, i, ProgressSteps, $"Step {i + 1}", ct);
            }
            Finished.Add(claim.AttemptToken);
            return [new ExportArtifact(claim.ProjectId, claim.Id, Guid.NewGuid(), new string('a', 64), ArtifactKind.PreviewVideo,
                $"test/{claim.Id:N}/{claim.AttemptToken:N}.mp4", new string('b', 64), 10, DateTimeOffset.UtcNow)];
        }
        catch (OperationCanceledException) { Cancelled.Add(claim.AttemptToken); throw; }
        finally { active.AddOrUpdate(claim.Id, 0, (_, n) => n - 1); }
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int seen;
        while ((seen = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, seen) != seen) { }
    }
}

internal sealed class FakeExportJobHandler(FakeRenderer renderer, IServiceScopeFactory scopes) : IExportJobHandler
{
    public Task<IReadOnlyList<ExportArtifact>> RenderAsync(ClaimedJob claim, CancellationToken ct) => renderer.RenderAsync(claim, scopes, ct);
}

/// <summary>One job processor with its own services, standing in for one running app or Worker process.</summary>
public sealed class ProcessorHost : IAsyncDisposable
{
    private readonly ServiceProvider services;
    private readonly IHostedService processor;
    private bool stopped;

    public ProcessorHost(SqlJobDatabase database, FakeRenderer renderer, int leaseSeconds)
    {
        var collection = new ServiceCollection();
        collection.AddLogging(x => x.SetMinimumLevel(LogLevel.Warning));
        collection.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(database.ConnectionString));
        collection.AddScoped<IJobStore, SqlJobStore>();
        collection.AddSingleton(renderer);
        collection.AddScoped<IExportJobHandler, FakeExportJobHandler>();
        services = collection.BuildServiceProvider();
        processor = new ExportJobProcessor(services.GetRequiredService<ILogger<ExportJobProcessor>>(), services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new JobOptions { LeaseSeconds = leaseSeconds, MaxAttempts = 3 }));
    }

    public Task StartAsync() => processor.StartAsync(CancellationToken.None);

    /// <summary>Stops the processor the way a crash or shutdown would: the running render is cancelled and nothing is recorded.</summary>
    public async Task StopAsync() { if (stopped) return; stopped = true; await processor.StopAsync(CancellationToken.None); }

    public async ValueTask DisposeAsync() { await StopAsync(); await services.DisposeAsync(); }
}
