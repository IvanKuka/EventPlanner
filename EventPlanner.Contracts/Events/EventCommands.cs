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

/// <summary>
/// Deletes the project permanently with everything it owns (program, guests, groups, vendors, quotes,
/// decision history, budget and payments). Allowed in every status, archived included.
/// </summary>
/// <param name="ConfirmationName">The project name typed by the user; it must match, so a stray click cannot delete.</param>
public sealed record DeleteEventProject(Guid Id, long ExpectedRevision, string ConfirmationName);
