using MyAfricaMyNarrative.Domain;

namespace MyAfricaMyNarrative.UnitTests;

/// <summary>Background renders keep their lease while working, report real progress, and a replaced attempt can do neither.</summary>
public sealed class JobProgressTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
    private static Job NewJob() => new(Guid.NewGuid(), Guid.NewGuid(), "RenderPreview", new string('a', 64), "{}", Now);

    [Fact]
    public void Renewal_keeps_the_owning_attempt_alive_so_no_one_else_can_claim_it()
    {
        var job = NewJob(); var token = Guid.NewGuid();
        Assert.True(job.TryClaim("web", token, Now, TimeSpan.FromMinutes(2)));
        Assert.True(job.TryRenew(token, Now.AddMinutes(1.5), TimeSpan.FromMinutes(2)));
        // Without renewal the lease would have ended at 2 min; it now runs to 3.5 min.
        Assert.False(job.TryClaim("worker", Guid.NewGuid(), Now.AddMinutes(3), TimeSpan.FromMinutes(2)));
        Assert.True(job.TryClaim("worker", Guid.NewGuid(), Now.AddMinutes(4), TimeSpan.FromMinutes(2)));
        Assert.False(job.TryRenew(token, Now.AddMinutes(4), TimeSpan.FromMinutes(2)));   // the old attempt has lost it
    }

    [Fact]
    public void Progress_is_bounded_reset_on_each_attempt_and_ignored_from_a_replaced_attempt()
    {
        var job = NewJob(); var first = Guid.NewGuid();
        Assert.True(job.TryClaim("web", first, Now, TimeSpan.FromSeconds(30)));
        Assert.Equal(Now, job.StartedAtUtc);
        Assert.True(job.TryReportProgress(first, 2, 6, "Making scene 3 of 4: The first dress"));
        Assert.Equal((2, 6), (job.ProgressStep, job.ProgressTotal));
        Assert.False(job.TryReportProgress(first, 7, 6, "past the end"));
        Assert.False(job.TryReportProgress(first, 1, 0, "no total"));
        Assert.True(job.TryReportProgress(first, 3, 6, new string('x', 500)));
        Assert.Equal(Job.MaxProgressNoteLength, job.ProgressNote.Length);

        var second = Guid.NewGuid();
        Assert.True(job.TryClaim("worker", second, Now.AddMinutes(1), TimeSpan.FromSeconds(30)));
        Assert.Equal((0, 0), (job.ProgressStep, job.ProgressTotal));
        Assert.False(job.TryReportProgress(first, 5, 6, "stale attempt"));
        Assert.True(job.TryReportProgress(second, 1, 6, "Making scene 2 of 4"));
        job.Complete(second, Now.AddMinutes(2));
        Assert.False(job.TryReportProgress(second, 6, 6, "after completion"));
    }
}
