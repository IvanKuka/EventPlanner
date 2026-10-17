namespace EventPlanner.Contracts.Events;

public sealed record CreateEventProject(
    string Name,
    EventType EventType,
    DateOnly LocalStartDate,
    DateOnly LocalEndDate,
    string TimeZoneId,
    string? VenueText,
    string Currency,
    EventTemplate Template);

public sealed record UpdateEventProject(
    Guid Id,
    long ExpectedRevision,
    string Name,
    EventType EventType,
    DateOnly LocalStartDate,
    DateOnly LocalEndDate,
    string TimeZoneId,
    string? VenueText,
    string Currency);

/// <param name="ChecklistConfirmed">Moving to Ready requires the organizer's explicit confirmation.</param>
public sealed record ChangeEventStatus(Guid Id, long ExpectedRevision, EventStatus Target, bool ChecklistConfirmed);

public sealed record AddProgramPart(Guid EventId, long ExpectedRevision, string Label);

public sealed record RenameProgramPart(Guid EventId, long ExpectedRevision, Guid PartId, string Label);

/// <summary>Removing a part also removes it from every guest's attendance scope.</summary>
public sealed record RemoveProgramPart(Guid EventId, long ExpectedRevision, Guid PartId);
