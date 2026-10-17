namespace EventPlanner.Contracts.Events;

/// <summary>Lifecycle: Draft → Planning → Ready → Completed → Archived (archived is read-only until unlocked).</summary>
public enum EventStatus
{
    Draft,
    Planning,
    Ready,
    Completed,
    Archived,
}

public enum EventType
{
    Wedding,
    Celebration,
    Tournament,
    Other,
}

/// <summary>
/// Optional starter templates. They only pre-fill program parts; they never assume dates,
/// headcounts or vendors.
/// </summary>
public enum EventTemplate
{
    Blank,
    Wedding,
    Celebration,
}
