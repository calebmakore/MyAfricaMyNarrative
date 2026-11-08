namespace MyAfricaMyNarrative.Domain;

public enum JobState { Queued, Running, Succeeded, Failed, Cancelled }

public sealed class Job
{
    private Job() { }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public string TargetHash { get; private set; } = string.Empty;
    public JobState State { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
