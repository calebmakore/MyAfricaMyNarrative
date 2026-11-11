using Microsoft.EntityFrameworkCore;
using MyAfricaMyNarrative.Domain;

namespace MyAfricaMyNarrative.IntegrationTests.Jobs;

/// <summary>
/// Job safety against real SQL Server: with several processors (the web app plus a Worker, or two app instances),
/// every film is rendered by exactly one attempt at a time and saved exactly once, long renders keep their claim,
/// a lost claim stops the render without saving, and a crashed render is picked up again.
/// Runs sequentially in one throwaway database (xunit runs a class's tests one after another).
/// </summary>
public sealed class JobSafetyTests(SqlJobDatabase database) : IClassFixture<SqlJobDatabase>
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(45);

    [Fact]
    public async Task Two_processors_racing_render_each_job_once_and_save_it_once()
    {
        await database.ResetAsync();
        var renderer = new FakeRenderer { Duration = _ => TimeSpan.FromMilliseconds(800) };
        var jobs = new List<Guid>(); for (var i = 0; i < 6; i++) jobs.Add(await database.QueueAsync());
        await using var web = new ProcessorHost(database, renderer, leaseSeconds: 5);
        await using var worker = new ProcessorHost(database, renderer, leaseSeconds: 5);
        await Task.WhenAll(web.StartAsync(), worker.StartAsync());

        await SqlJobDatabase.WaitForAsync(async () => (await Task.WhenAll(jobs.Select(database.JobAsync))).All(x => x.State == JobState.Succeeded), Timeout, "all six jobs to succeed");

        Assert.Equal(6, renderer.Started.Count);           // no job was started twice
        Assert.Equal(1, renderer.MaxOverlapPerJob);         // never two attempts on one job at the same moment
        foreach (var id in jobs)
        {
            var job = await database.JobAsync(id);
            Assert.Equal(1, job.Attempts);
            Assert.Null(job.LastFailure);
            Assert.Equal(1, await database.ArtifactCountAsync(id));
        }
    }

    [Fact]
    public async Task A_render_longer_than_its_claim_keeps_it_by_renewing()
    {
        await database.ResetAsync();
        var renderer = new FakeRenderer { Duration = _ => TimeSpan.FromSeconds(7), ProgressSteps = 7 };
        var id = await database.QueueAsync();
        await using var web = new ProcessorHost(database, renderer, leaseSeconds: 2);
        await using var worker = new ProcessorHost(database, renderer, leaseSeconds: 2);
        await Task.WhenAll(web.StartAsync(), worker.StartAsync());

        await SqlJobDatabase.WaitForAsync(async () => (await database.JobAsync(id)).State == JobState.Running, Timeout, "the render to start");
        var started = (await database.JobAsync(id)).StartedAtUtc!.Value;
        await Task.Delay(TimeSpan.FromSeconds(4));   // twice the claim length
        var mid = await database.JobAsync(id);
        Assert.Equal(JobState.Running, mid.State);
        Assert.True(mid.LeaseExpiresAtUtc > started.AddSeconds(3), "the lease should have been renewed beyond its original 2 s");

        await SqlJobDatabase.WaitForAsync(async () => (await database.JobAsync(id)).State == JobState.Succeeded, Timeout, "the long render to succeed");
        Assert.Single(renderer.Started);                    // the other processor never took it over
        Assert.Equal(1, (await database.JobAsync(id)).Attempts);
        Assert.Equal(1, await database.ArtifactCountAsync(id));
    }

    [Fact]
    public async Task A_render_that_loses_its_claim_stops_and_saves_nothing()
    {
        await database.ResetAsync();
        var renderer = new FakeRenderer { Duration = _ => TimeSpan.FromSeconds(20), ProgressSteps = 40 };
        var id = await database.QueueAsync();
        await using var web = new ProcessorHost(database, renderer, leaseSeconds: 2);
        await web.StartAsync();
        await SqlJobDatabase.WaitForAsync(() => Task.FromResult(!renderer.Started.IsEmpty), Timeout, "the render to start");
        var original = renderer.Started.Single();

        // Another processor takes the job over (as if this one had stalled past its lease).
        var takeover = Guid.NewGuid();
        await using (var db = database.NewContext())
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Jobs SET AttemptToken = {takeover}, LeaseOwner = {"other"}, LeaseExpiresAtUtc = {DateTimeOffset.UtcNow.AddMinutes(5)} WHERE Id = {id}");

        await SqlJobDatabase.WaitForAsync(() => Task.FromResult(renderer.Cancelled.Contains(original)), Timeout, "the displaced render to be cancelled");
        await Task.Delay(500);
        var job = await database.JobAsync(id);
        Assert.Equal(JobState.Running, job.State);          // the old attempt could not fail or finish the job
        Assert.Equal(takeover, job.AttemptToken);
        Assert.DoesNotContain(original, renderer.Finished);
        Assert.Equal(0, await database.ArtifactCountAsync(id));
    }

    [Fact]
    public async Task A_render_cut_off_by_a_crash_is_picked_up_again_and_finished_once()
    {
        await database.ResetAsync();
        var renderer = new FakeRenderer { Duration = n => n == 1 ? TimeSpan.FromSeconds(30) : TimeSpan.FromMilliseconds(600) };
        var id = await database.QueueAsync();
        var crashing = new ProcessorHost(database, renderer, leaseSeconds: 2);
        await crashing.StartAsync();
        await SqlJobDatabase.WaitForAsync(() => Task.FromResult(!renderer.Started.IsEmpty), Timeout, "the first render to start");
        await crashing.DisposeAsync();                       // the process goes away mid-render; nothing is recorded
        Assert.Equal(JobState.Running, (await database.JobAsync(id)).State);

        await using var survivor = new ProcessorHost(database, renderer, leaseSeconds: 2);
        await survivor.StartAsync();
        await SqlJobDatabase.WaitForAsync(async () => (await database.JobAsync(id)).State == JobState.Succeeded, Timeout, "the job to be recovered");

        var job = await database.JobAsync(id);
        Assert.Equal(2, job.Attempts);
        Assert.Equal(2, renderer.Started.Count);
        Assert.Single(renderer.Finished);
        Assert.Equal(1, await database.ArtifactCountAsync(id));
    }

    [Fact]
    public async Task Frequent_progress_and_renewals_never_make_finishing_fail()
    {
        await database.ResetAsync();
        // A 1 s claim renews every ~333 ms while progress is written every 100 ms: the writes constantly overlap.
        var renderer = new FakeRenderer { Duration = _ => TimeSpan.FromSeconds(3), ProgressSteps = 30 };
        var jobs = new List<Guid>(); for (var i = 0; i < 3; i++) jobs.Add(await database.QueueAsync());
        await using var web = new ProcessorHost(database, renderer, leaseSeconds: 1);
        await web.StartAsync();

        await SqlJobDatabase.WaitForAsync(async () => (await Task.WhenAll(jobs.Select(database.JobAsync))).All(x => x.State == JobState.Succeeded), TimeSpan.FromSeconds(60), "all three jobs to succeed");
        foreach (var id in jobs)
        {
            var job = await database.JobAsync(id);
            Assert.Equal(1, job.Attempts);
            Assert.Null(job.LastFailure);
            Assert.Equal(1, await database.ArtifactCountAsync(id));
        }
        Assert.Empty(renderer.Cancelled);
    }
}
