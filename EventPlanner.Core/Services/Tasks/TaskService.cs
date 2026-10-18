using EventPlanner.Contracts.Results;
using EventPlanner.Contracts.Tasks;
using EventPlanner.Core.Models.Events;
using EventPlanner.Core.Models.Tasks;
using EventPlanner.Core.Ports;
using EventPlanner.Core.Services.Events;
using EventPlanner.Core.Validation;
using EventPlanner.Core.Validation.Tasks;
using Microsoft.Extensions.Logging;

namespace EventPlanner.Core.Services.Tasks;

/// <summary>
/// To-do list of an event: what is still needed, who does it and by when. Deadlines are dates in
/// the project's time zone; dependencies stay inside one event and never form a cycle.
/// </summary>
public sealed class TaskService(IDataSessionFactory sessions, TimeProvider clock, ILogger<TaskService> logger)
{
    private const string EventEntity = "EventProject";
    private const string TaskEntity = "Task";

    public async Task<Result<TaskListDto>> ListAsync(Guid eventId, CancellationToken cancellationToken)
    {
        await using var session = sessions.Open();
        var project = await session.Events.FindAsync(eventId, cancellationToken);
        if (project is null)
        {
            return OperationError.NotFound(EventEntity);
        }

        var tasks = await session.Tasks.ListAsync(eventId, cancellationToken);
        var today = TaskPlanning.Today(clock.GetUtcNow(), project.TimeZoneId);
        var stateOf = tasks.ToDictionary(t => t.Id, t => t.State);
        var dtos = TaskPlanning.Order(tasks).Select(t => t.ToDto(today, stateOf)).ToList();
        return new TaskListDto(dtos, today, dtos.Count(t => t.State != TaskState.Done), dtos.Count(t => t.IsOverdue));
    }

    public async Task<Result<TaskDto>> CreateAsync(CreateTask command, CancellationToken cancellationToken)
    {
        var input = Normalize(command.Task);
        var issues = TaskValidator.Validate(input);
        if (issues.Count > 0)
        {
            return OperationError.Validation(issues);
        }

        await using var session = sessions.Open();
        var writable = await WritableEvent.FindAsync(session, command.EventId, cancellationToken);
        if (writable.Error is not null)
        {
            return writable.Error;
        }

        var task = EventTask.Create(Guid.CreateVersion7(), command.EventId, input);
        var tasks = await session.Tasks.ListAsync(command.EventId, cancellationToken);
        var dependencyError = CheckPredecessor(task.Id, input.PredecessorId, tasks);
        if (dependencyError is not null)
        {
            return dependencyError;
        }

        session.Tasks.Add(task);
        if (await session.SaveChangesAsync(cancellationToken) == SaveOutcome.Conflict)
        {
            return OperationError.Conflict(TaskEntity);
        }

        return ToResult(task, writable.Value, [.. tasks, task]);
    }

    public async Task<Result<TaskDto>> UpdateAsync(UpdateTask command, CancellationToken cancellationToken)
    {
        var input = Normalize(command.Task);
        var issues = TaskValidator.Validate(input);
        if (issues.Count > 0)
        {
            return OperationError.Validation(issues);
        }

        await using var session = sessions.Open();
        var (task, project, error) = await FindWritableAsync(session, command.TaskId, command.ExpectedRevision, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var tasks = await session.Tasks.ListAsync(task!.EventId, cancellationToken);
        var dependencyError = CheckPredecessor(task.Id, input.PredecessorId, tasks);
        if (dependencyError is not null)
        {
            return dependencyError;
        }

        task.Update(input);
        if (await session.SaveChangesAsync(cancellationToken) == SaveOutcome.Conflict)
        {
            return OperationError.Conflict(TaskEntity);
        }

        return ToResult(task, project!, [.. tasks.Where(t => t.Id != task.Id), task]);
    }

    /// <summary>Finishing a task whose predecessor is still open is allowed; it carries <see cref="WarningCodes.PredecessorNotDone"/>.</summary>
    public async Task<Result<TaskDto>> SetStateAsync(SetTaskState command, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(command.State))
        {
            return OperationError.Validation(nameof(TaskDto.State), ValidationRules.InvalidValue);
        }

        await using var session = sessions.Open();
        var (task, project, error) = await FindWritableAsync(session, command.TaskId, command.ExpectedRevision, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        task!.SetState(command.State);
        if (await session.SaveChangesAsync(cancellationToken) == SaveOutcome.Conflict)
        {
            return OperationError.Conflict(TaskEntity);
        }

        var tasks = await session.Tasks.ListAsync(task.EventId, cancellationToken);
        var result = ToResult(task, project!, [.. tasks.Where(t => t.Id != task.Id), task]);
        var predecessorOpen = task.State == TaskState.Done && task.PredecessorId is { } predecessorId
            && tasks.FirstOrDefault(t => t.Id == predecessorId) is { State: not TaskState.Done };
        return predecessorOpen
            ? Result<TaskDto>.Success(result.Value, [new Warning(WarningCodes.PredecessorNotDone, nameof(TaskDto.PredecessorId))])
            : result;
    }

    public async Task<Result<Guid>> DeleteAsync(DeleteTask command, CancellationToken cancellationToken)
    {
        await using var session = sessions.Open();
        var (task, _, error) = await FindWritableAsync(session, command.TaskId, command.ExpectedRevision, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        session.Tasks.Remove(task!);
        if (await session.SaveChangesAsync(cancellationToken) == SaveOutcome.Conflict)
        {
            return OperationError.Conflict(TaskEntity);
        }

        logger.LogInformation("Task {TaskId} deleted", task!.Id);
        return task.Id;
    }

    /// <summary>The predecessor must be another task of the same event and must not close a loop.</summary>
    private static OperationError? CheckPredecessor(Guid taskId, Guid? predecessorId, IReadOnlyList<EventTask> eventTasks)
    {
        if (predecessorId is not { } id)
        {
            return null;
        }

        // Cross-event links are forbidden: an id outside this event's tasks is unknown here.
        if (eventTasks.All(t => t.Id != id))
        {
            return OperationError.Validation(nameof(TaskDto.PredecessorId), ValidationRules.UnknownReference);
        }

        var predecessorOf = eventTasks.ToDictionary(t => t.Id, t => t.PredecessorId);
        return TaskPlanning.WouldCreateCycle(taskId, id, predecessorOf)
            ? OperationError.Validation(nameof(TaskDto.PredecessorId), ValidationRules.DependencyCycle)
            : null;
    }

    private static async Task<(EventTask? Task, EventProject? Project, OperationError? Error)> FindWritableAsync(
        IDataSession session, Guid taskId, long expectedRevision, CancellationToken cancellationToken)
    {
        var task = await session.Tasks.FindAsync(taskId, cancellationToken);
        if (task is null)
        {
            return (null, null, OperationError.NotFound(TaskEntity));
        }

        var writable = await WritableEvent.FindAsync(session, task.EventId, cancellationToken);
        if (writable.Error is not null)
        {
            return (null, null, writable.Error);
        }

        return task.Revision != expectedRevision
            ? (null, null, OperationError.Conflict(TaskEntity))
            : (task, writable.Value, null);
    }

    private Result<TaskDto> ToResult(EventTask task, EventProject project, IReadOnlyList<EventTask> eventTasks)
    {
        var today = TaskPlanning.Today(clock.GetUtcNow(), project.TimeZoneId);
        return task.ToDto(today, eventTasks.ToDictionary(t => t.Id, t => t.State));
    }

    // Blank optional text is "not stated" (null), never an empty value.
    private static TaskInput Normalize(TaskInput input) => input with
    {
        OwnerLabel = TextValidation.NullIfBlank(input.OwnerLabel)?.Trim(),
        Notes = TextValidation.NullIfBlank(input.Notes),
    };
}
