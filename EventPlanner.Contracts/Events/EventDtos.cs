namespace EventPlanner.Contracts.Events;

public sealed record ProgramPartDto(Guid Id, string Label, int Order);

public sealed record EventProjectDto(
    Guid Id,
    string Name,
    EventType EventType,
    DateOnly LocalStartDate,
    DateOnly LocalEndDate,
    string TimeZoneId,
    string? VenueText,
    string Currency,
    EventStatus Status,
    long Revision,
    IReadOnlyList<ProgramPartDto> ProgramParts)
{
    public bool IsReadOnly => Status == EventStatus.Archived;
}

public sealed record EventSummaryDto(Guid Id, string Name, EventType EventType, DateOnly LocalStartDate, EventStatus Status);
