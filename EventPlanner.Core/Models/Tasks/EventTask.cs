using EventPlanner.Contracts.Tasks;

namespace EventPlanner.Core.Models.Tasks;

/// <summary>
/// A to-do item of one event (the "Task" entity of the data model; named EventTask so it does not
/// clash with System.Threading.Tasks.Task). The optional predecessor must belong to the same event
/// and never close a cycle; both are checked by the service before a change is applied.
/// </summary>
public sealed class EventTask
{
    private EventTask()
    {
    }

    public Guid Id { get; private set; }

    public Guid EventId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? OwnerLabel { get; private set; }

    public DateOnly? DueLocalDate { get; private set; }

    public TaskPriority Priority { get; private set; }

    public TaskState State { get; private set; }

    public Guid? PredecessorId { get; private set; }

    public string? Notes { get; private set; }

    public long Revision { get; private set; }

    public static EventTask Create(Guid id, Guid eventId, TaskInput input)
    {
        var task = new EventTask { Id = id, EventId = eventId, State = TaskState.Todo, Revision = 1 };
        task.Apply(input);
        return task;
    }

    public void Update(TaskInput input)
    {
        Apply(input);
        Revision++;
    }

    public void SetState(TaskState state)
    {
        if (State == state)
        {
            return;
        }

        State = state;
        Revision++;
    }

    private void Apply(TaskInput input)
    {
        Title = input.Title.Trim();
        OwnerLabel = input.OwnerLabel?.Trim();
        DueLocalDate = input.DueLocalDate;
        Priority = input.Priority;
        PredecessorId = input.PredecessorId;
        Notes = input.Notes;
    }
}
