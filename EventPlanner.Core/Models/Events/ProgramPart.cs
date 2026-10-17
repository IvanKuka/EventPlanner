namespace EventPlanner.Core.Models.Events;

/// <summary>
/// A project-defined part of the program (e.g. ceremony, dinner). Guests attend explicit parts;
/// a single RSVP never implies attendance of every part.
/// </summary>
public sealed class ProgramPart
{
    private ProgramPart()
    {
    }

    internal ProgramPart(Guid id, Guid eventId, string label, int order)
    {
        Id = id;
        EventId = eventId;
        Label = label;
        Order = order;
    }

    public Guid Id { get; private set; }

    public Guid EventId { get; private set; }

    public string Label { get; internal set; } = string.Empty;

    public int Order { get; internal set; }
}
