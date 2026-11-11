namespace MyAfricaMyNarrative.Domain;

public enum IntentMatch { NotYet, Partly, Mostly, Fully }

[Flags]
public enum HelpArea
{
    None = 0, Recording = 1, Words = 2, StoryShape = 4, Planning = 8, Captions = 16, Export = 32, Technical = 64
}

/// <summary>
/// The creator's own reflection after making a film, used to evaluate the tool in a pilot. One per project; it can be updated.
/// It contains no media and no story text unless the creator writes it here.
/// </summary>
public sealed class PilotFeedback
{
    private PilotFeedback() { }
    public PilotFeedback(Guid projectId, IntentMatch intentMatch, HelpArea neededHelp, string helpNote, string comment, DateTimeOffset now)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("A project is required.");
        Id = Guid.NewGuid(); ProjectId = projectId; CreatedAtUtc = now; Update(intentMatch, neededHelp, helpNote, comment, now);
    }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public IntentMatch IntentMatch { get; private set; }
    public HelpArea NeededHelp { get; private set; }
    public string HelpNote { get; private set; } = string.Empty;
    public string Comment { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void Update(IntentMatch intentMatch, HelpArea neededHelp, string? helpNote, string? comment, DateTimeOffset now)
    {
        helpNote = (helpNote ?? string.Empty).Trim(); comment = (comment ?? string.Empty).Trim();
        if (!Enum.IsDefined(intentMatch)) throw new ArgumentException("Choose how well the film matches what you meant.");
        if (((int)neededHelp & ~127) != 0) throw new ArgumentException("Unknown help area.");
        if (helpNote.Length > 500 || comment.Length > 1000) throw new ArgumentException("Keep notes under 500 and comments under 1000 characters.");
        IntentMatch = intentMatch; NeededHelp = neededHelp; HelpNote = helpNote; Comment = comment; UpdatedAtUtc = now;
    }
}
