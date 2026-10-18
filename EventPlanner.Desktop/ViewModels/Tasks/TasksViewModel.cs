using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EventPlanner.Contracts.Events;
using EventPlanner.Contracts.Results;
using EventPlanner.Contracts.Tasks;
using EventPlanner.Core.Services.Tasks;
using EventPlanner.Desktop.Localization;
using EventPlanner.Desktop.ViewModels.Shell;

namespace EventPlanner.Desktop.ViewModels.Tasks;

/// <summary>
/// To-do list of the active project: what is still needed, who does it and by when. Open tasks come
/// first by deadline; finished ones can be hidden. Deadlines are dates in the project's time zone.
/// </summary>
public sealed partial class TasksViewModel : ViewModelBase, IPage
{
    private readonly TaskService _tasks;
    private readonly ActiveProjectContext _active;
    private EventProjectDto? _project;
    private List<TaskDto> _all = [];
    private TaskDto? _editing;

    public TasksViewModel(TaskService tasks, ActiveProjectContext active)
    {
        _tasks = tasks;
        _active = active;
        EditPriority = PriorityOptions[1];
        EditPredecessor = NoPredecessor;
    }

    public string Title => Strings.Nav_Tasks;

    private static DisplayOption<Guid?> NoPredecessor { get; } = new(null, Strings.Tasks_NoPredecessor);

    public IReadOnlyList<DisplayOption<TaskPriority>> PriorityOptions { get; } = EnumText.Options<TaskPriority>(EnumText.For);

    public ObservableCollection<TaskRow> Tasks { get; } = [];

    public ObservableCollection<DisplayOption<Guid?>> PredecessorOptions { get; } = [];

    [ObservableProperty]
    public partial bool HasProject { get; set; }

    [ObservableProperty]
    public partial bool IsReadOnly { get; set; }

    [ObservableProperty]
    public partial bool ShowDone { get; set; } = true;

    [ObservableProperty]
    public partial string? SummaryText { get; set; }

    [ObservableProperty]
    public partial TaskRow? SelectedTask { get; set; }

    [ObservableProperty]
    public partial bool IsEditing { get; set; }

    [ObservableProperty]
    public partial bool IsNewTask { get; set; }

    [ObservableProperty]
    public partial bool IsConfirmingDelete { get; set; }

    [ObservableProperty]
    public partial string EditTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? EditOwner { get; set; }

    [ObservableProperty]
    public partial DateTime? EditDue { get; set; }

    [ObservableProperty]
    public partial DisplayOption<TaskPriority> EditPriority { get; set; }

    [ObservableProperty]
    public partial DisplayOption<Guid?> EditPredecessor { get; set; }

    [ObservableProperty]
    public partial string? EditNotes { get; set; }

    [ObservableProperty]
    public partial FieldErrors Errors { get; set; } = FieldErrors.None;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string? InfoMessage { get; set; }

    [ObservableProperty]
    public partial string? WarningMessage { get; set; }

    public bool CanChangeState => SelectedTask is not null && !IsReadOnly;

    public async Task ActivateAsync(CancellationToken cancellationToken)
    {
        _project = _active.Project;
        HasProject = _project is not null;
        IsReadOnly = _project?.IsReadOnly ?? false;
        IsEditing = false;
        ClearMessages();
        if (_project is null)
        {
            _all = [];
            Tasks.Clear();
            SummaryText = null;
            return;
        }

        await ReloadAsync(_project, cancellationToken);
    }

    partial void OnSelectedTaskChanged(TaskRow? value)
    {
        OnPropertyChanged(nameof(CanChangeState));
        if (value is not null)
        {
            Edit(value.Task);
        }
    }

    partial void OnShowDoneChanged(bool value) => ApplyFilter();

    [RelayCommand]
    private void NewTask()
    {
        SelectedTask = null;
        _editing = null;
        IsNewTask = true;
        IsEditing = true;
        EditTitle = string.Empty;
        EditOwner = null;
        EditDue = null;
        EditPriority = PriorityOptions[1];
        EditNotes = null;
        RebuildPredecessorOptions(excludeTaskId: null, selected: null);
        ClearMessages();
    }

    [RelayCommand]
    private async Task SaveTaskAsync()
    {
        if (_project is null)
        {
            return;
        }

        var input = new TaskInput(
            EditTitle,
            EditOwner,
            EditDue is { } due ? DateOnly.FromDateTime(due) : null,
            EditPriority.Value,
            EditPredecessor?.Value,
            EditNotes);
        var result = _editing is { } editing
            ? await _tasks.UpdateAsync(new UpdateTask(editing.Id, editing.Revision, input), CancellationToken.None)
            : await _tasks.CreateAsync(new CreateTask(_project.Id, input), CancellationToken.None);
        if (!ShowResult(result))
        {
            return;
        }

        InfoMessage = Strings.Info_Saved;
        await ReloadAsync(_project, CancellationToken.None, selectTaskId: result.Value.Id);
    }

    [RelayCommand]
    private Task MarkTodoAsync() => SetStateAsync(TaskState.Todo);

    [RelayCommand]
    private Task MarkInProgressAsync() => SetStateAsync(TaskState.InProgress);

    [RelayCommand]
    private Task MarkDoneAsync() => SetStateAsync(TaskState.Done);

    [RelayCommand]
    private void RequestDelete() => IsConfirmingDelete = _editing is not null;

    [RelayCommand]
    private void CancelDelete() => IsConfirmingDelete = false;

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        IsConfirmingDelete = false;
        if (_editing is not { } task || _project is null)
        {
            return;
        }

        var result = await _tasks.DeleteAsync(new DeleteTask(task.Id, task.Revision), CancellationToken.None);
        if (!ShowResult(result))
        {
            return;
        }

        IsEditing = false;
        _editing = null;
        InfoMessage = Strings.Info_Deleted;
        await ReloadAsync(_project, CancellationToken.None);
    }

    private async Task SetStateAsync(TaskState state)
    {
        if (SelectedTask?.Task is not { } task || _project is null)
        {
            return;
        }

        var result = await _tasks.SetStateAsync(new SetTaskState(task.Id, task.Revision, state), CancellationToken.None);
        if (!ShowResult(result))
        {
            return;
        }

        InfoMessage = string.Format(CultureInfo.CurrentCulture, Strings.Tasks_StateChanged, EnumText.For(state));
        await ReloadAsync(_project, CancellationToken.None, selectTaskId: task.Id);
    }

    private async Task ReloadAsync(EventProjectDto project, CancellationToken cancellationToken, Guid? selectTaskId = null)
    {
        var result = await _tasks.ListAsync(project.Id, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (_active.Project?.Id != project.Id)
        {
            return; // Results for a project that is no longer active are dropped.
        }

        if (!result.IsSuccess)
        {
            ErrorMessage = ErrorText.For(result.Error!);
            return;
        }

        var list = result.Value;
        _all = [.. list.Tasks];
        SummaryText = list.Tasks.Count == 0
            ? null
            : string.Format(CultureInfo.CurrentCulture, Strings.Tasks_Summary, list.OpenCount, list.OverdueCount);
        ApplyFilter();

        // DTOs are records: an unchanged reloaded task equals the old one and the setter would not
        // fire, so the open editor is refreshed explicitly.
        if (selectTaskId is { } id && Tasks.FirstOrDefault(t => t.Task.Id == id) is { } row)
        {
            SelectedTask = row;
            Edit(row.Task);
        }
        else if (_editing is { } editing && _all.FirstOrDefault(t => t.Id == editing.Id) is { } fresh)
        {
            Edit(fresh);
        }
    }

    private void ApplyFilter()
    {
        var titles = _all.ToDictionary(t => t.Id, t => t.Title);
        var selectedId = SelectedTask?.Task.Id;
        Tasks.Clear();
        foreach (var task in _all.Where(t => ShowDone || t.State != TaskState.Done))
        {
            Tasks.Add(new TaskRow(task, task.PredecessorId is { } p ? titles.GetValueOrDefault(p) : null));
        }

        SelectedTask = Tasks.FirstOrDefault(t => t.Task.Id == selectedId);
    }

    private void Edit(TaskDto task)
    {
        _editing = task;
        IsNewTask = false;
        IsEditing = true;
        IsConfirmingDelete = false;
        EditTitle = task.Title;
        EditOwner = task.OwnerLabel;
        EditDue = task.DueLocalDate?.ToDateTime(TimeOnly.MinValue);
        EditPriority = PriorityOptions.First(o => o.Value == task.Priority);
        EditNotes = task.Notes;
        RebuildPredecessorOptions(excludeTaskId: task.Id, selected: task.PredecessorId);
        Errors = FieldErrors.None;
    }

    /// <summary>
    /// Every other task can be chosen; the service rejects cycles. The bound ComboBox writes null back
    /// when the list is cleared, so the choice is set again afterwards.
    /// </summary>
    private void RebuildPredecessorOptions(Guid? excludeTaskId, Guid? selected)
    {
        PredecessorOptions.Clear();
        PredecessorOptions.Add(NoPredecessor);
        foreach (var task in _all.Where(t => t.Id != excludeTaskId).OrderBy(t => t.Title, StringComparer.CurrentCulture))
        {
            PredecessorOptions.Add(new DisplayOption<Guid?>(task.Id, task.Title));
        }

        EditPredecessor = PredecessorOptions.FirstOrDefault(o => o.Value == selected) ?? NoPredecessor;
    }

    /// <summary>Applies the result to messages and field errors; returns true on success.</summary>
    private bool ShowResult<T>(Result<T> result)
    {
        Errors = ErrorText.FieldsOf(result.Error);
        if (!result.IsSuccess)
        {
            ErrorMessage = ErrorText.For(result.Error!);
            InfoMessage = null;
            return false;
        }

        ErrorMessage = null;
        WarningMessage = result.Warnings.Count > 0 ? string.Join(" ", result.Warnings.Select(ErrorText.For)) : null;
        return true;
    }

    private void ClearMessages()
    {
        IsConfirmingDelete = false;
        Errors = FieldErrors.None;
        ErrorMessage = null;
        InfoMessage = null;
        WarningMessage = null;
    }
}
