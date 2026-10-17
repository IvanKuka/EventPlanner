using EventPlanner.Contracts.Events;
using EventPlanner.Core.Models.Events;

namespace EventPlanner.Core.Services.Events;

internal static class EventMapping
{
    public static EventProjectDto ToDto(this EventProject project) => new(
        project.Id,
        project.Name,
        project.EventType,
        project.LocalStartDate,
        project.LocalEndDate,
        project.TimeZoneId,
        project.VenueText,
        project.Currency,
        project.Status,
        project.Revision,
        [.. project.ProgramParts.OrderBy(p => p.Order).Select(p => new ProgramPartDto(p.Id, p.Label, p.Order))]);

    public static EventSummaryDto ToSummary(this EventProject project) =>
        new(project.Id, project.Name, project.EventType, project.LocalStartDate, project.Status);
}
