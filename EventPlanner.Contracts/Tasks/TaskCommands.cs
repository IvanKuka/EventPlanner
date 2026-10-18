namespace EventPlanner.Contracts.Tasks;

/// <param name="Title">What needs to be done.</param>
/// <param name="OwnerLabel">Who takes care of it, as free text (no user accounts).</param>
/// <param name="DueLocalDate">Deadline as a date in the project's time zone; null = no deadline.</param>
/// <param name="PredecessorId">Task of the same project that has to be finished first; dependencies never form a cycle.</param>
/// <param name="Notes">Details such as what is still missing.</param>
public sealed record TaskInput(
    string Title,
    string? OwnerLabel,
    DateOnly? DueLocalDate,
    TaskPriority Priority,
    Guid? PredecessorId,
    string? Notes);

public sealed record CreateTask(Guid EventId, TaskInput Task);

public sealed record UpdateTask(Guid TaskId, long ExpectedRevision, TaskInput Task);

/// <summary>Marks progress; finishing a task whose predecessor is not done is allowed but warned about.</summary>
public sealed record SetTaskState(Guid TaskId, long ExpectedRevision, TaskState State);

/// <summary>Tasks that followed the deleted one keep existing and only lose the dependency.</summary>
public sealed record DeleteTask(Guid TaskId, long ExpectedRevision);
