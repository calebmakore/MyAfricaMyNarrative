using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;

namespace MyAfricaMyNarrative.UnitTests;

public sealed class StudioTests
{
    [Fact]
    public void Approval_is_bound_to_exact_immutable_snapshot()
    {
        var projectId = Guid.NewGuid(); var audio = Guid.NewGuid(); var visual = Guid.NewGuid();
        var first = new StoryRevision(projectId, 1, [new SceneDraft("Opening", "Original words", TextOrigin.CreatorWritten, audio, visual, null, 0, 1000, "Original words")], DateTimeOffset.UtcNow);
        var changed = new StoryRevision(projectId, 2, [new SceneDraft("Opening", "Changed words", TextOrigin.CreatorWritten, audio, visual, null, 0, 1000, "Changed words")], DateTimeOffset.UtcNow);
        Assert.NotEqual(first.SnapshotHash, changed.SnapshotHash);
        Assert.False(first.Scenes.First().GetType().GetProperty(nameof(StoryScene.Text))!.SetMethod!.IsPublic);
    }

    [Fact]
    public void Expired_job_can_be_reclaimed_but_old_attempt_cannot_complete()
    {
        var now = DateTimeOffset.UtcNow; var job = new Job(Guid.NewGuid(), Guid.NewGuid(), "RenderPreview", new string('a', 64), "{}", now);
        var old = Guid.NewGuid(); Assert.True(job.TryClaim("one", old, now, TimeSpan.FromSeconds(1)));
        var current = Guid.NewGuid(); Assert.True(job.TryClaim("two", current, now.AddSeconds(2), TimeSpan.FromMinutes(1)));
        Assert.Throws<InvalidOperationException>(() => job.Complete(old, now.AddSeconds(3)));
        job.Complete(current, now.AddSeconds(3)); Assert.Equal(JobState.Succeeded, job.State);
    }

    [Fact]
    public void Active_lease_cannot_be_claimed_twice()
    {
        var now = DateTimeOffset.UtcNow; var job = new Job(Guid.NewGuid(), Guid.NewGuid(), "RenderPreview", new string('b', 64), "{}", now);
        Assert.True(job.TryClaim("one", Guid.NewGuid(), now, TimeSpan.FromMinutes(1)));
        Assert.False(job.TryClaim("two", Guid.NewGuid(), now.AddSeconds(1), TimeSpan.FromMinutes(1)));
    }
}
