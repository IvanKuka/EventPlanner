using System.Globalization;
using EventPlanner.Contracts.Tasks;
using EventPlanner.Desktop.Localization;

namespace EventPlanner.Desktop.ViewModels.Tasks;

/// <summary>One line of the to-do list. Overdue and waiting states are spelled out, never shown by colour alone.</summary>
public sealed record TaskRow(TaskDto Task, string? PredecessorTitle)
{
    public string Title => Task.Title;

    public bool IsDone => Task.State == TaskState.Done;

    public bool IsOverdue => Task.IsOverdue;

    public bool IsHighPriority => Task.Priority == TaskPriority.High && !IsDone;

    public string StateText => EnumText.For(Task.State);

    public string DueText => Task.DueLocalDate is { } due
        ? string.Format(CultureInfo.CurrentCulture, Strings.Tasks_Due, due.ToString("d", CultureInfo.CurrentCulture))
        : Strings.Tasks_NoDeadline;

    public string? OwnerText => Task.OwnerLabel is { } owner ? string.Format(CultureInfo.CurrentCulture, Strings.Tasks_Owner, owner) : null;

    public string? WaitingText => Task.IsWaiting && PredecessorTitle is not null
        ? string.Format(CultureInfo.CurrentCulture, Strings.Tasks_WaitingFor, PredecessorTitle)
        : null;
}
