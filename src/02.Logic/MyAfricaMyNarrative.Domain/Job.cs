namespace MyAfricaMyNarrative.Domain;

public enum JobState { Queued, Running, Succeeded, Failed, Cancelled }

public sealed class Job
{
    private Job() { }
    public Job(Guid projectId, Guid ownerUserId, string kind, string targetHash, string payloadJson, DateTimeOffset now)
    {
        if (projectId == Guid.Empty || ownerUserId == Guid.Empty) throw new ArgumentException("Project and owner are required.");
        Id = Guid.NewGuid(); ProjectId = projectId; OwnerUserId = ownerUserId; Kind = kind; TargetHash = targetHash;
        PayloadJson = payloadJson; State = JobState.Queued; CreatedAtUtc = now; NextAttemptAtUtc = now;
    }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public string TargetHash { get; private set; } = string.Empty;
    public JobState State { get; private set; }
    public int Attempts { get; private set; }
    public string PayloadJson { get; private set; } = string.Empty;
    public string? LeaseOwner { get; private set; }
    public Guid? AttemptToken { get; private set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; private set; }
    public DateTimeOffset NextAttemptAtUtc { get; private set; }
    public string? LastFailure { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool TryClaim(string worker, Guid token, DateTimeOffset now, TimeSpan lease)
    {
        if (State == JobState.Cancelled || State == JobState.Succeeded || NextAttemptAtUtc > now ||
            (State == JobState.Running && LeaseExpiresAtUtc > now)) return false;
        State = JobState.Running; LeaseOwner = worker; AttemptToken = token; LeaseExpiresAtUtc = now.Add(lease); Attempts++;
        LastFailure = null; return true;
    }

    public void Complete(Guid token, DateTimeOffset now)
    {
        EnsureAttempt(token); State = JobState.Succeeded; CompletedAtUtc = now; ClearLease();
    }

    public void Fail(Guid token, string failure, DateTimeOffset retryAt, int maxAttempts, DateTimeOffset now)
    {
        EnsureAttempt(token); LastFailure = failure.Length > 1000 ? failure[..1000] : failure;
        State = Attempts >= maxAttempts ? JobState.Failed : JobState.Queued;
        NextAttemptAtUtc = retryAt; if (State == JobState.Failed) CompletedAtUtc = now; ClearLease();
    }

    public void Retry(DateTimeOffset now)
    {
        if (State is not (JobState.Failed or JobState.Cancelled)) throw new InvalidOperationException("Only failed or cancelled jobs can be retried.");
        State = JobState.Queued; Attempts = 0; NextAttemptAtUtc = now; CompletedAtUtc = null; LastFailure = null; ClearLease();
    }

    private void EnsureAttempt(Guid token)
    { if (State != JobState.Running || AttemptToken != token) throw new InvalidOperationException("The job attempt no longer owns this lease."); }
    private void ClearLease() { LeaseOwner = null; AttemptToken = null; LeaseExpiresAtUtc = null; }

    public void Cancel(DateTimeOffset now) { if (State is JobState.Queued or JobState.Running) { State = JobState.Cancelled; CompletedAtUtc = now; ClearLease(); } }
}
