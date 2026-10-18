using EventPlanner.Contracts.Tasks;
using EventPlanner.Core.Models.Tasks;

namespace EventPlanner.Core.Services.Tasks;

/// <summary>Pure rules of the task list: dependency cycles, overdue and waiting flags, and ordering.</summary>
public static class TaskPlanning
{
    /// <summary>
    /// True when making <paramref name="predecessorId"/> the predecessor of <paramref name="taskId"/>
    /// would close a loop (including a task following itself). Walks the existing chain upwards.
    /// </summary>
    public static bool WouldCreateCycle(Guid taskId, Guid? predecessorId, IReadOnlyDictionary<Guid, Guid?> predecessorOf)
    {
        var visited = new HashSet<Guid>();
        for (var current = predecessorId; current is { } id; current = predecessorOf.GetValueOrDefault(id))
        {
            if (id == taskId || !visited.Add(id))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsOverdue(EventTask task, DateOnly today) =>
        task.State != TaskState.Done && task.DueLocalDate is { } due && due < today;

    /// <summary>
    /// Open tasks first; among them the earliest deadline first (no deadline last), then higher
    /// priority, then title. Done tasks follow in the same order.
    /// </summary>
    public static IOrderedEnumerable<EventTask> Order(IEnumerable<EventTask> tasks) => tasks
        .OrderBy(t => t.State == TaskState.Done)
        .ThenBy(t => t.DueLocalDate is null)
        .ThenBy(t => t.DueLocalDate)
        .ThenByDescending(t => t.Priority)
        .ThenBy(t => t.Title, StringComparer.CurrentCulture);

    /// <summary>"Today" for deadlines is the project's local date, not the computer's.</summary>
    public static DateOnly Today(DateTimeOffset utcNow, string timeZoneId) =>
        DateOnly.FromDateTime(TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone)
            ? TimeZoneInfo.ConvertTime(utcNow, zone).DateTime
            : utcNow.UtcDateTime);
}
