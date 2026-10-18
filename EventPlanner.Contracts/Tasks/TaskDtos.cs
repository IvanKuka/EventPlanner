namespace EventPlanner.Contracts.Tasks;

/// <param name="IsOverdue">Not done and the deadline is before today in the project's time zone.</param>
/// <param name="IsWaiting">The predecessor is not done yet.</param>
public sealed record TaskDto(
    Guid Id,
    Guid EventId,
    string Title,
    string? OwnerLabel,
    DateOnly? DueLocalDate,
    TaskPriority Priority,
    TaskState State,
    Guid? PredecessorId,
    string? Notes,
    long Revision,
    bool IsOverdue,
    bool IsWaiting);

/// <summary>Tasks of one project, open ones first by deadline, plus the date the overdue flags refer to.</summary>
public sealed record TaskListDto(IReadOnlyList<TaskDto> Tasks, DateOnly Today, int OpenCount, int OverdueCount);
