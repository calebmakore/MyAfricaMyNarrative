using System.Data;
using Microsoft.EntityFrameworkCore;
using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;
using MyAfricaMyNarrative.Infrastructure.Persistence;

namespace MyAfricaMyNarrative.Infrastructure.Jobs;

public sealed class SqlJobStore(ApplicationDbContext db) : IJobStore
{
    public async Task<ClaimedJob?> ClaimAsync(string worker, TimeSpan lease, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var now = DateTimeOffset.UtcNow;
        var job = await db.Jobs.FromSqlInterpolated($@"
SELECT TOP(1) * FROM Jobs WITH (UPDLOCK, READPAST, ROWLOCK)
WHERE (State = {(int)JobState.Queued} OR (State = {(int)JobState.Running} AND LeaseExpiresAtUtc <= {now}))
  AND NextAttemptAtUtc <= {now}
ORDER BY CreatedAtUtc").SingleOrDefaultAsync(ct);
        if (job is null) return null;
        var token = Guid.NewGuid();
        if (!job.TryClaim(worker, token, now, lease)) return null;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new ClaimedJob(job.Id, job.ProjectId, job.OwnerUserId, job.Kind, job.PayloadJson, token);
    }

    public async Task CompleteAsync(ClaimedJob claim, IReadOnlyList<ExportArtifact> artifacts, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var job = await db.Jobs.SingleAsync(x => x.Id == claim.Id, ct);
        job.Complete(claim.AttemptToken, DateTimeOffset.UtcNow);
        db.ExportArtifacts.AddRange(artifacts);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task FailAsync(ClaimedJob claim, string failure, DateTimeOffset retryAt, int maxAttempts, CancellationToken ct)
    {
        var job = await db.Jobs.SingleAsync(x => x.Id == claim.Id, ct);
        job.Fail(claim.AttemptToken, failure, retryAt, maxAttempts, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);
    }
}
