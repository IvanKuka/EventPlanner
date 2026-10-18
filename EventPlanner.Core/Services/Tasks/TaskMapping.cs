using EventPlanner.Contracts.Tasks;
using EventPlanner.Core.Models.Tasks;

namespace EventPlanner.Core.Services.Tasks;

internal static class TaskMapping
{
    public static TaskDto ToDto(this EventTask task, DateOnly today, IReadOnlyDictionary<Guid, TaskState> stateOf) => new(
        task.Id,
        task.EventId,
        task.Title,
        task.OwnerLabel,
        task.DueLocalDate,
        task.Priority,
        task.State,
        task.PredecessorId,
        task.Notes,
        task.Revision,
        TaskPlanning.IsOverdue(task, today),
        task.State != TaskState.Done && task.PredecessorId is { } predecessor
            && stateOf.TryGetValue(predecessor, out var state) && state != TaskState.Done);
}
