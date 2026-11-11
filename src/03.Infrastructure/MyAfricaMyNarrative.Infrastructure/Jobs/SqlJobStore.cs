using System.Data;
using Microsoft.EntityFrameworkCore;
using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;
using MyAfricaMyNarrative.Infrastructure.Persistence;

namespace MyAfricaMyNarrative.Infrastructure.Jobs;

/// <summary>
/// Durable SQL job store. Every change re-reads the job row first: while a render runs, lease renewals and progress
/// updates change the row (and its row version), so a copy tracked since the claim would be stale.
/// The attempt token on the row still decides who may complete, fail, renew or report.
/// </summary>
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
        for (var attempt = 0; ; attempt++)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var job = await CurrentAsync(claim.Id, ct) ?? throw new InvalidOperationException("The job no longer exists.");
            job.Complete(claim.AttemptToken, DateTimeOffset.UtcNow);   // throws if another attempt owns the lease
            db.ExportArtifacts.AddRange(artifacts);
            try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return; }
            catch (DbUpdateConcurrencyException) when (attempt == 0)
            {
                // A last progress or renewal write landed between the read and the save: forget both changes and retry once.
                await tx.RollbackAsync(ct);
                foreach (var artifact in artifacts) db.Entry(artifact).State = EntityState.Detached;
                db.Entry(job).State = EntityState.Detached;
            }
        }
    }

    public async Task<bool> RenewAsync(ClaimedJob claim, TimeSpan lease, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var job = await CurrentAsync(claim.Id, ct);
            if (job is null || !job.TryRenew(claim.AttemptToken, DateTimeOffset.UtcNow, lease)) return false;   // really lost: another attempt owns it
            try { await db.SaveChangesAsync(ct); return true; }
            catch (DbUpdateConcurrencyException) { }   // a progress update changed the row; reload and retry
        }
        return true;   // still owned on every reload; the next renewal will extend it
    }

    public async Task ReportProgressAsync(ClaimedJob claim, int step, int total, string note, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var job = await CurrentAsync(claim.Id, ct);
            if (job is null || !job.TryReportProgress(claim.AttemptToken, step, total, note)) return;
            try { await db.SaveChangesAsync(ct); return; }
            catch (DbUpdateConcurrencyException) { }   // a lease renewal changed the row; reload once
        }
    }

    public async Task FailAsync(ClaimedJob claim, string failure, DateTimeOffset retryAt, int maxAttempts, CancellationToken ct)
    {
        var job = await CurrentAsync(claim.Id, ct) ?? throw new InvalidOperationException("The job no longer exists.");
        job.Fail(claim.AttemptToken, failure, retryAt, maxAttempts, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Reads the job row as it is now, dropping any copy this context tracked earlier.</summary>
    private async Task<Job?> CurrentAsync(Guid id, CancellationToken ct)
    {
        foreach (var entry in db.ChangeTracker.Entries<Job>().Where(x => x.Entity.Id == id).ToList()) entry.State = EntityState.Detached;
        return await db.Jobs.SingleOrDefaultAsync(x => x.Id == id, ct);
    }
}
