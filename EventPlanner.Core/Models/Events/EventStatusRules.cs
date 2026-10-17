using EventPlanner.Contracts.Events;

namespace EventPlanner.Core.Models.Events;

/// <summary>
/// Allowed lifecycle transitions. Ready can go back to Planning (open tasks are not lost),
/// and Archived only returns to Completed through an explicit unlock.
/// </summary>
public static class EventStatusRules
{
    private static readonly Dictionary<EventStatus, EventStatus[]> Allowed = new()
    {
        [EventStatus.Draft] = [EventStatus.Planning],
        [EventStatus.Planning] = [EventStatus.Draft, EventStatus.Ready],
        [EventStatus.Ready] = [EventStatus.Planning, EventStatus.Completed],
        [EventStatus.Completed] = [EventStatus.Ready, EventStatus.Archived],
        [EventStatus.Archived] = [EventStatus.Completed],
    };

    public static bool CanTransition(EventStatus from, EventStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static IReadOnlyList<EventStatus> NextStates(EventStatus from) =>
        Allowed.TryGetValue(from, out var targets) ? targets : [];

    /// <summary>Ready is a manual checkpoint confirmed by the organizer after the checklist.</summary>
    public static bool RequiresConfirmation(EventStatus to) => to == EventStatus.Ready;
}
