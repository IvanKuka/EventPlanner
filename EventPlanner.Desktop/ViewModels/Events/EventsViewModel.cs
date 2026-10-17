using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EventPlanner.Contracts.Events;
using EventPlanner.Contracts.Results;
using EventPlanner.Core.Models.Events;
using EventPlanner.Core.Services.Events;
using EventPlanner.Desktop.Localization;
using EventPlanner.Desktop.ViewModels.Shell;
using Microsoft.Extensions.Logging;

namespace EventPlanner.Desktop.ViewModels.Events;

public sealed partial class ProgramPartItem(Guid id, string label) : ObservableObject
{
    public Guid Id { get; } = id;

    [ObservableProperty]
    public partial string Label { get; set; } = label;
}

/// <summary>An allowed next lifecycle state offered as a button.</summary>
public sealed record StatusTransition(EventStatus Value, string Label)
{
    public string ButtonText => string.Format(CultureInfo.CurrentCulture, Strings.Events_MoveTo, Label);
}

/// <summary>Project list and the editor of the selected (or new) project, including lifecycle and program parts.</summary>
public sealed partial class EventsViewModel : ViewModelBase, IPage
{
    private readonly EventProjectService _events;
    private readonly ActiveProjectContext _active;
    private readonly ILogger<EventsViewModel> _logger;
    private EventProjectDto? _loaded;

    public EventsViewModel(EventProjectService events, ActiveProjectContext active, ILogger<EventsViewModel> logger)
    {
        _events = events;
        _active = active;
        _logger = logger;
        SelectedEventType = EventTypeOptions[0];
        SelectedTemplate = TemplateOptions[0];
        StartNewProject();
    }

    public string Title => Strings.Nav_Projects;

    public ObservableCollection<EventSummaryDto> Projects { get; } = [];

    public ObservableCollection<ProgramPartItem> ProgramParts { get; } = [];

    public ObservableCollection<StatusTransition> NextStatuses { get; } = [];

    public IReadOnlyList<DisplayOption<EventType>> EventTypeOptions { get; } = EnumText.Options<EventType>(EnumText.For);

    public IReadOnlyList<DisplayOption<EventTemplate>> TemplateOptions { get; } = EnumText.Options<EventTemplate>(EnumText.For);

    public IReadOnlyList<string> TimeZones { get; } = TimeZoneCatalog.IanaIds;

    [ObservableProperty]
    public partial EventSummaryDto? SelectedProject { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExisting))]
    public partial bool IsNew { get; set; }

    [ObservableProperty]
    public partial bool IsReadOnly { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DisplayOption<EventType> SelectedEventType { get; set; }

    [ObservableProperty]
    public partial DisplayOption<EventTemplate> SelectedTemplate { get; set; }

    [ObservableProperty]
    public partial DateTime? StartDate { get; set; }

    [ObservableProperty]
    public partial DateTime? EndDate { get; set; }

    [ObservableProperty]
    public partial string TimeZoneId { get; set; } = TimeZoneCatalog.LocalIanaId;

    [ObservableProperty]
    public partial string? VenueText { get; set; }

    [ObservableProperty]
    public partial string Currency { get; set; } = "EUR";

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ReadyChecklistConfirmed { get; set; }

    [ObservableProperty]
    public partial string NewPartLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial FieldErrors Errors { get; set; } = FieldErrors.None;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string? InfoMessage { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public bool IsExisting => !IsNew;

    public async Task ActivateAsync(CancellationToken cancellationToken)
    {
        var projects = await _events.ListAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        Projects.Clear();
        foreach (var project in projects)
        {
            Projects.Add(project);
        }

        if (_active.Project is { } current)
        {
            SelectedProject = Projects.FirstOrDefault(p => p.Id == current.Id);
        }
        else if (Projects.Count > 0)
        {
            SelectedProject = Projects[0];
        }
    }

    partial void OnSelectedProjectChanged(EventSummaryDto? value)
    {
        // Re-selecting the project that is already shown (e.g. after a save) needs no reload.
        if (value is not null && value.Id != _loaded?.Id)
        {
            _ = LoadProjectAsync(value.Id);
        }
    }

    [RelayCommand]
    private void NewProject()
    {
        SelectedProject = null;
        StartNewProject();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (StartDate is null || EndDate is null)
        {
            var missing = new List<KeyValuePair<string, string>>();
            if (StartDate is null)
            {
                missing.Add(KeyValuePair.Create(nameof(EventProjectDto.LocalStartDate), Strings.Validation_Required));
            }

            if (EndDate is null)
            {
                missing.Add(KeyValuePair.Create(nameof(EventProjectDto.LocalEndDate), Strings.Validation_Required));
            }

            Errors = new FieldErrors(missing);
            ErrorMessage = Strings.Error_ValidationFailed;
            return;
        }

        var start = DateOnly.FromDateTime(StartDate.Value);
        var end = DateOnly.FromDateTime(EndDate.Value);
        await RunAsync(() => IsNew || _loaded is null
            ? _events.CreateAsync(
                new CreateEventProject(Name, SelectedEventType.Value, start, end, TimeZoneId.Trim(), VenueText, Currency.Trim(), SelectedTemplate.Value),
                CancellationToken.None)
            : _events.UpdateAsync(
                new UpdateEventProject(_loaded.Id, _loaded.Revision, Name, SelectedEventType.Value, start, end, TimeZoneId.Trim(), VenueText, Currency.Trim()),
                CancellationToken.None),
            Strings.Info_Saved);
    }

    [RelayCommand]
    private Task ChangeStatusAsync(StatusTransition target) =>
        _loaded is null
            ? Task.CompletedTask
            : RunAsync(
                () => _events.ChangeStatusAsync(
                    new ChangeEventStatus(_loaded.Id, _loaded.Revision, target.Value, ReadyChecklistConfirmed), CancellationToken.None),
                string.Format(CultureInfo.CurrentCulture, Strings.Info_StatusChanged, target.Label));

    [RelayCommand]
    private Task AddPartAsync() =>
        _loaded is null
            ? Task.CompletedTask
            : RunAsync(
                () => _events.AddProgramPartAsync(new AddProgramPart(_loaded.Id, _loaded.Revision, NewPartLabel), CancellationToken.None),
                Strings.Info_Saved,
                onSuccess: () => NewPartLabel = string.Empty);

    [RelayCommand]
    private Task RenamePartAsync(ProgramPartItem part) =>
        _loaded is null
            ? Task.CompletedTask
            : RunAsync(
                () => _events.RenameProgramPartAsync(new RenameProgramPart(_loaded.Id, _loaded.Revision, part.Id, part.Label), CancellationToken.None),
                Strings.Info_Saved);

    [RelayCommand]
    private Task RemovePartAsync(ProgramPartItem part) =>
        _loaded is null
            ? Task.CompletedTask
            : RunAsync(
                () => _events.RemoveProgramPartAsync(new RemoveProgramPart(_loaded.Id, _loaded.Revision, part.Id), CancellationToken.None),
                Strings.Info_PartRemoved);

    private async Task LoadProjectAsync(Guid id)
    {
        Result<EventProjectDto> result;
        try
        {
            result = await _events.GetAsync(id, CancellationToken.None);
        }
        catch (Exception ex) when (ex is IOException or Microsoft.Data.Sqlite.SqliteException)
        {
            // Started from a selection change (fire-and-forget), so the failure is logged and shown here.
            _logger.LogError(ex, "Event project {EventId} could not be loaded", id);
            ErrorMessage = Strings.Error_Unexpected;
            return;
        }

        if (SelectedProject?.Id != id)
        {
            return; // The selection moved on while loading.
        }

        if (result.IsSuccess)
        {
            Show(result.Value);
            ErrorMessage = null;
        }
        else
        {
            ErrorMessage = ErrorText.For(result.Error!);
        }
    }

    private async Task RunAsync(Func<Task<Result<EventProjectDto>>> command, string successMessage, Action? onSuccess = null)
    {
        IsBusy = true;
        InfoMessage = null;
        try
        {
            var result = await command();
            Errors = ErrorText.FieldsOf(result.Error);
            if (!result.IsSuccess)
            {
                ErrorMessage = ErrorText.For(result.Error!);
                return;
            }

            ErrorMessage = null;
            InfoMessage = successMessage;
            onSuccess?.Invoke();
            Show(result.Value);
            UpsertSummary(result.Value);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Show(EventProjectDto project)
    {
        _loaded = project;
        IsNew = false;
        IsReadOnly = project.IsReadOnly;
        Name = project.Name;
        SelectedEventType = EventTypeOptions.First(o => o.Value == project.EventType);
        StartDate = project.LocalStartDate.ToDateTime(TimeOnly.MinValue);
        EndDate = project.LocalEndDate.ToDateTime(TimeOnly.MinValue);
        TimeZoneId = project.TimeZoneId;
        VenueText = project.VenueText;
        Currency = project.Currency;
        StatusText = EnumText.For(project.Status);
        ReadyChecklistConfirmed = false;

        ProgramParts.Clear();
        foreach (var part in project.ProgramParts)
        {
            ProgramParts.Add(new ProgramPartItem(part.Id, part.Label));
        }

        NextStatuses.Clear();
        foreach (var status in EventStatusRules.NextStates(project.Status))
        {
            NextStatuses.Add(new StatusTransition(status, EnumText.For(status)));
        }

        _active.Project = project;
    }

    private void UpsertSummary(EventProjectDto project)
    {
        var summary = new EventSummaryDto(project.Id, project.Name, project.EventType, project.LocalStartDate, project.Status);
        var index = Projects.ToList().FindIndex(p => p.Id == project.Id);
        if (index >= 0)
        {
            Projects[index] = summary;
        }
        else
        {
            Projects.Add(summary);
        }

        SelectedProject = summary;
    }

    private void StartNewProject()
    {
        _loaded = null;
        IsNew = true;
        IsReadOnly = false;
        Name = string.Empty;
        SelectedEventType = EventTypeOptions[0];
        SelectedTemplate = TemplateOptions[0];
        StartDate = null;
        EndDate = null;
        TimeZoneId = TimeZoneCatalog.LocalIanaId;
        VenueText = null;
        Currency = "EUR";
        StatusText = EnumText.For(EventStatus.Draft);
        ProgramParts.Clear();
        NextStatuses.Clear();
        Errors = FieldErrors.None;
        ErrorMessage = null;
        InfoMessage = null;
    }
}
