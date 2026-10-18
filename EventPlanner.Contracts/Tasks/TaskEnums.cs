namespace EventPlanner.Contracts.Tasks;

/// <summary>Progress of a to-do item. Stable English codes are stored; the UI shows localized text.</summary>
public enum TaskState
{
    Todo,
    InProgress,
    Done,
}

public enum TaskPriority
{
    Low,
    Normal,
    High,
}
