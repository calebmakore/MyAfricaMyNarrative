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
    /// <summary>Real render progress reported by the running attempt: step N of a known total, with a short plain note.</summary>
    public int ProgressStep { get; private set; }
    public int ProgressTotal { get; private set; }
    public string ProgressNote { get; private set; } = string.Empty;
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public const int MaxProgressNoteLength = 120;
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
        LastFailure = null; StartedAtUtc = now; ProgressStep = 0; ProgressTotal = 0; ProgressNote = "Getting your recordings and pictures ready"; return true;
    }

    /// <summary>Extends the lease for the attempt that still owns it. Returns false if another attempt took over or the job ended.</summary>
    public bool TryRenew(Guid token, DateTimeOffset now, TimeSpan lease)
    {
        if (State != JobState.Running || AttemptToken != token) return false;
        LeaseExpiresAtUtc = now.Add(lease); return true;
    }

    /// <summary>Records progress for the attempt that owns the lease. Ignored (false) for any other attempt.</summary>
    public bool TryReportProgress(Guid token, int step, int total, string note)
    {
        if (State != JobState.Running || AttemptToken != token || total < 1 || step < 0 || step > total) return false;
        note = note.Trim();
        ProgressStep = step; ProgressTotal = total; ProgressNote = note.Length > MaxProgressNoteLength ? note[..MaxProgressNoteLength] : note; return true;
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
