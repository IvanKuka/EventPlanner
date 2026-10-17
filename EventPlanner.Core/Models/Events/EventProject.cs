using EventPlanner.Contracts.Events;

namespace EventPlanner.Core.Models.Events;

/// <summary>
/// Root of one event. Dates are local to <see cref="TimeZoneId"/> (IANA id) regardless of the
/// computer's own zone. <see cref="Revision"/> is the optimistic concurrency token.
/// </summary>
public sealed class EventProject
{
    private readonly List<ProgramPart> _programParts = [];

    private EventProject()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public EventType EventType { get; private set; }

    public DateOnly LocalStartDate { get; private set; }

    public DateOnly LocalEndDate { get; private set; }

    public string TimeZoneId { get; private set; } = string.Empty;

    public string? VenueText { get; private set; }

    /// <summary>ISO 4217 code. MVP has a single project currency.</summary>
    public string Currency { get; private set; } = string.Empty;

    public EventStatus Status { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }

    public long Revision { get; private set; }

    public IReadOnlyList<ProgramPart> ProgramParts => _programParts;

    public bool IsReadOnly => Status == EventStatus.Archived;

    public static EventProject Create(
        Guid id,
        string name,
        EventType eventType,
        DateOnly localStartDate,
        DateOnly localEndDate,
        string timeZoneId,
        string? venueText,
        string currency,
        DateTimeOffset createdUtc)
    {
        var project = new EventProject
        {
            Id = id,
            Status = EventStatus.Draft,
            CreatedUtc = createdUtc,
            Revision = 1,
        };
        project.ApplyDetails(name, eventType, localStartDate, localEndDate, timeZoneId, venueText, currency);
        return project;
    }

    public void UpdateDetails(
        string name,
        EventType eventType,
        DateOnly localStartDate,
        DateOnly localEndDate,
        string timeZoneId,
        string? venueText,
        string currency)
    {
        EnsureWritable();
        ApplyDetails(name, eventType, localStartDate, localEndDate, timeZoneId, venueText, currency);
        Revision++;
    }

    /// <remarks>Callers validate the transition with <see cref="EventStatusRules"/> first.</remarks>
    public void ChangeStatus(EventStatus target)
    {
        if (!EventStatusRules.CanTransition(Status, target))
        {
            throw new InvalidOperationException($"Transition {Status} -> {target} is not allowed.");
        }

        Status = target;
        Revision++;
    }

    public ProgramPart AddProgramPart(Guid partId, string label)
    {
        EnsureWritable();
        var order = _programParts.Count == 0 ? 0 : _programParts.Max(p => p.Order) + 1;
        var part = new ProgramPart(partId, Id, label, order);
        _programParts.Add(part);
        Revision++;
        return part;
    }

    public void RenameProgramPart(ProgramPart part, string label)
    {
        EnsureWritable();
        part.Label = label;
        Revision++;
    }

    public void RemoveProgramPart(ProgramPart part)
    {
        EnsureWritable();
        _programParts.Remove(part);
        Revision++;
    }

    private void ApplyDetails(
        string name,
        EventType eventType,
        DateOnly localStartDate,
        DateOnly localEndDate,
        string timeZoneId,
        string? venueText,
        string currency)
    {
        Name = name.Trim();
        EventType = eventType;
        LocalStartDate = localStartDate;
        LocalEndDate = localEndDate;
        TimeZoneId = timeZoneId;
        VenueText = venueText;
        Currency = currency;
    }

    private void EnsureWritable()
    {
        if (IsReadOnly)
        {
            throw new InvalidOperationException("Archived event is read-only.");
        }
    }
}
