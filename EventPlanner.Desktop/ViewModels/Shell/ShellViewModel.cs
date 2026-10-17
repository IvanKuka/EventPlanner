using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EventPlanner.Contracts.Events;
using EventPlanner.Desktop.Composition;
using EventPlanner.Desktop.Localization;
using EventPlanner.Desktop.Platform;
using EventPlanner.Desktop.ViewModels.Backup;
using EventPlanner.Desktop.ViewModels.Budget;
using EventPlanner.Desktop.ViewModels.Events;
using EventPlanner.Desktop.ViewModels.Guests;
using EventPlanner.Desktop.ViewModels.Overview;
using EventPlanner.Desktop.ViewModels.Vendors;
using EventPlanner.Infrastructure.Backup;
using EventPlanner.Infrastructure.Profiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventPlanner.Desktop.ViewModels.Shell;

public enum ShellState
{
    Loading,
    Ready,
    Failed,
}

/// <summary>
/// Owns startup: opens the active profile through the safe startup sequence, then builds the
/// profile-bound services and pages. Failures leave data untouched and offer recovery.
/// </summary>
public sealed partial class ShellViewModel : ViewModelBase, IDisposable
{
    private readonly AppOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IPlatformServices _platform;
    private readonly ILogger<ShellViewModel> _logger;
    private readonly ThemePreferenceStore _themes;
    private OpenedProfile? _profile;
    private ServiceProvider? _services;
    private ActiveProjectContext? _activeProject;
    private CancellationTokenSource? _activationCts;

    public ShellViewModel(AppOptions options, ILoggerFactory loggerFactory, IPlatformServices platform)
    {
        _options = options;
        _loggerFactory = loggerFactory;
        _platform = platform;
        _logger = loggerFactory.CreateLogger<ShellViewModel>();

        // The theme is applied before any profile opens, so start-up and recovery screens use it too.
        _themes = new ThemePreferenceStore(options.DataRoot, loggerFactory.CreateLogger<ThemePreferenceStore>());
        Theme = _themes.Read();
        _platform.ApplyTheme(Theme);
    }

    public AppTheme Theme { get; private set; }

    public bool IsSystemTheme => Theme == AppTheme.System;

    public bool IsLightTheme => Theme == AppTheme.Light;

    public bool IsDarkTheme => Theme == AppTheme.Dark;

    /// <summary>Every page, including the ones reached from the header (projects, backup).</summary>
    public ObservableCollection<IPage> Pages { get; } = [];

    /// <summary>Pages of the active project, shown as tabs.</summary>
    public ObservableCollection<IPage> TabPages { get; } = [];

    /// <summary>Project list and editor; opened from the project switcher, not a tab.</summary>
    public EventsViewModel? ProjectsPage { get; private set; }

    public BackupViewModel? BackupPage { get; private set; }

    /// <summary>The selected tab, or null while a header page (projects, backup) is shown.</summary>
    public IPage? SelectedTab
    {
        get => SelectedPage is { } page && TabPages.Contains(page) ? page : null;
        set
        {
            // The tab strip clears its selection when a header page opens; that is not a navigation.
            if (value is not null)
            {
                SelectedPage = value;
            }
        }
    }

    [ObservableProperty]
    public partial ShellState State { get; set; } = ShellState.Loading;

    [ObservableProperty]
    public partial string? FailureMessage { get; set; }

    [ObservableProperty]
    public partial bool CanRecoverFromBackup { get; set; }

    [ObservableProperty]
    public partial string? RecoveryMessage { get; set; }

    [ObservableProperty]
    public partial IPage? SelectedPage { get; set; }

    [ObservableProperty]
    public partial string ProfileName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ProjectTitle { get; set; } = Strings.Shell_NoProject;

    // The active project's bar under the tabs (the "open document" row of the AdobeNew layout).
    [ObservableProperty]
    public partial string? ProjectName { get; set; }

    [ObservableProperty]
    public partial string? ProjectStatusText { get; set; }

    [ObservableProperty]
    public partial string? ProjectDetailsText { get; set; }

    [ObservableProperty]
    public partial string WindowTitle { get; set; } = Strings.App_Title;

    /// <summary>Banner after an unexpected failure that the app survived (see <see cref="UnexpectedErrors"/>).</summary>
    [ObservableProperty]
    public partial string? UnexpectedErrorMessage { get; set; }

    /// <summary>The project bar belongs to the project's tabs, not to the projects or backup screens.</summary>
    public bool ShowProjectBar => ProjectName is not null && SelectedTab is not null;

    public bool IsLoading => State == ShellState.Loading;

    public bool IsReady => State == ShellState.Ready;

    public bool IsFailed => State == ShellState.Failed;

    public bool IsProjectsOpen => SelectedPage is not null && SelectedPage == ProjectsPage;

    public bool IsBackupOpen => SelectedPage is not null && SelectedPage == BackupPage;

    /// <summary>Completes when startup either succeeded or failed; never throws.</summary>
    public async Task<bool> InitializeAsync(CancellationToken cancellationToken)
    {
        var profileName = _options.DataRoot.ReadActiveProfileName();
        var profileDirectory = _options.DataRoot.ProfileDirectory(profileName);
        ProfileName = profileName;
        try
        {
            var opener = new ProfileOpener(TimeProvider.System, _loggerFactory.CreateLogger<ProfileOpener>());

            // Migrations and integrity checks must not run on the UI thread.
            var result = await Task.Run(() => opener.OpenAsync(profileName, profileDirectory, cancellationToken), cancellationToken);
            if (result.Profile is null)
            {
                // A second start of the same profile shows the window that already has it open
                // (e.g. minimized) instead of an error; the locked message is only for no answer.
                if (result.Status == ProfileOpenStatus.Locked && await _platform.TryActivateRunningInstanceAsync(profileDirectory))
                {
                    _logger.LogInformation("Profile {Profile} is open in another window; brought it to the front", profileName);
                    _platform.Shutdown(0);
                    return false;
                }

                Fail(result.Status switch
                {
                    ProfileOpenStatus.Locked => Strings.Startup_Locked,
                    ProfileOpenStatus.NewerSchema => Strings.Startup_NewerSchema,
                    ProfileOpenStatus.InsufficientSpace => Strings.Startup_InsufficientSpace,
                    _ => Strings.Startup_Corrupt,
                }, canRecover: result.Status == ProfileOpenStatus.Corrupt);
                return false;
            }

            _profile = result.Profile;

            // From the moment this instance holds the profile lock, a later start must find it.
            _platform.ListenForActivation(profileDirectory);
            _services = AppServices.BuildForProfile(_options, _loggerFactory, _profile, _platform);
            _activeProject = _services.GetRequiredService<ActiveProjectContext>();
            _activeProject.PropertyChanged += OnActiveProjectChanged;

            ProjectsPage = _services.GetRequiredService<EventsViewModel>();
            BackupPage = _services.GetRequiredService<BackupViewModel>();
            TabPages.Add(_services.GetRequiredService<OverviewViewModel>());
            TabPages.Add(_services.GetRequiredService<GuestsViewModel>());
            TabPages.Add(_services.GetRequiredService<VendorsViewModel>());
            TabPages.Add(_services.GetRequiredService<BudgetViewModel>());
            Pages.Add(ProjectsPage);
            foreach (var tab in TabPages)
            {
                Pages.Add(tab);
            }

            Pages.Add(BackupPage);
            OnPropertyChanged(nameof(ProjectsPage));
            OnPropertyChanged(nameof(BackupPage));

            // The project list is loaded first so the switcher is filled and the first project becomes
            // active; without any project the app opens on the project editor instead of empty tabs.
            await ProjectsPage.ActivateAsync(cancellationToken);
            State = ShellState.Ready;
            SelectedPage = ProjectsPage.Projects.Count > 0 ? TabPages[0] : ProjectsPage;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Profile could not be opened");
            Fail(Strings.Startup_Io, canRecover: false);
            return false;
        }
    }

    /// <summary>Recovery when the active profile cannot be opened: restore a backup into a new profile and switch to it.</summary>
    [RelayCommand]
    private async Task RecoverFromBackupAsync()
    {
        var archive = await _platform.PickBackupToRestoreAsync();
        if (archive is null)
        {
            return;
        }

        var opener = new ProfileOpener(TimeProvider.System, _loggerFactory.CreateLogger<ProfileOpener>());
        var backup = new ProfileBackupService(_options.DataRoot, opener, TimeProvider.System, _loggerFactory.CreateLogger<ProfileBackupService>());
        var restored = await Task.Run(() => backup.RestoreAsNewProfileAsync(archive, CancellationToken.None));
        if (!restored.IsSuccess)
        {
            RecoveryMessage = ErrorText.For(restored.Error!);
            return;
        }

        _options.DataRoot.SetActiveProfile(restored.Value.ProfileName);
        RecoveryMessage = string.Format(CultureInfo.CurrentCulture, Strings.Backup_RestoredRestartRequired, restored.Value.ProfileName);
        CanRecoverFromBackup = false;
    }

    [RelayCommand]
    private void Exit() => _platform.Shutdown(0);

    /// <summary>
    /// Called for an unhandled UI exception the app survives: the user is told the action did not
    /// finish, and the shown page reloads so it no longer displays a half-applied state.
    /// </summary>
    public void ReportUnexpectedError()
    {
        UnexpectedErrorMessage = Strings.Error_UnexpectedRecovered;
        if (SelectedPage is { } page)
        {
            Activate(page);
        }
    }

    [RelayCommand]
    private void DismissUnexpectedError() => UnexpectedErrorMessage = null;

    [RelayCommand]
    private void OpenProjects() => SelectedPage = ProjectsPage;

    [RelayCommand]
    private void NewProject()
    {
        ProjectsPage?.NewProjectCommand.Execute(null);
        SelectedPage = ProjectsPage;
    }

    [RelayCommand]
    private void OpenBackup() => SelectedPage = BackupPage;

    [RelayCommand]
    private void SetTheme(AppTheme theme)
    {
        if (!Enum.IsDefined(theme) || theme == Theme)
        {
            return;
        }

        Theme = theme;
        _platform.ApplyTheme(theme);
        _themes.Write(theme);
        OnPropertyChanged(nameof(Theme));
        OnPropertyChanged(nameof(IsSystemTheme));
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(IsDarkTheme));
    }

    partial void OnStateChanged(ShellState value)
    {
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(IsFailed));
    }

    partial void OnSelectedPageChanged(IPage? value)
    {
        OnPropertyChanged(nameof(SelectedTab));
        OnPropertyChanged(nameof(ShowProjectBar));
        OnPropertyChanged(nameof(IsProjectsOpen));
        OnPropertyChanged(nameof(IsBackupOpen));
        if (value is not null)
        {
            Activate(value);
        }
    }

    private void Activate(IPage page)
    {
        _activationCts?.Cancel();
        _activationCts = new CancellationTokenSource();
        _ = ActivateSafelyAsync(page, _activationCts.Token);
    }

    private async Task ActivateSafelyAsync(IPage page, CancellationToken cancellationToken)
    {
        try
        {
            await page.ActivateAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Navigation moved on; the stale load is intentionally dropped.
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
        {
            _logger.LogError(ex, "Page {Page} failed to load", page.GetType().Name);
        }
    }

    private void OnActiveProjectChanged(object? sender, PropertyChangedEventArgs e)
    {
        var project = _activeProject?.Project;
        ProjectTitle = project is null
            ? Strings.Shell_NoProject
            : $"{project.Name} · {EnumText.For(project.Status)}";
        ProjectName = project?.Name;
        ProjectStatusText = project is null ? null : EnumText.For(project.Status);
        ProjectDetailsText = project is null ? null : DescribeProject(project);
        WindowTitle = project is null ? Strings.App_Title : $"{project.Name} – {Strings.App_Title}";
        OnPropertyChanged(nameof(ShowProjectBar));

        // Pages load their data on activation, so the shown tab is reloaded for the new project. The
        // project editor itself is what changed the project and keeps its state.
        if (SelectedPage is { } page && page != ProjectsPage)
        {
            Activate(page);
        }
    }

    /// <summary>Type, dates and venue in one line, e.g. "Svadba · 12. 6. 2027 – 13. 6. 2027 · Sála".</summary>
    private static string DescribeProject(EventProjectDto project)
    {
        var culture = CultureInfo.CurrentCulture;
        var dates = project.LocalStartDate == project.LocalEndDate
            ? project.LocalStartDate.ToString("d", culture)
            : $"{project.LocalStartDate.ToString("d", culture)} – {project.LocalEndDate.ToString("d", culture)}";
        string?[] parts = [EnumText.For(project.EventType), dates, project.VenueText];
        return string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private void Fail(string message, bool canRecover)
    {
        FailureMessage = message;
        CanRecoverFromBackup = canRecover;
        State = ShellState.Failed;
    }

    /// <summary>Releases services and the profile writer lock. Safe to call more than once.</summary>
    public void Dispose()
    {
        _activationCts?.Cancel();
        _activationCts?.Dispose();
        _activationCts = null;
        if (_activeProject is not null)
        {
            _activeProject.PropertyChanged -= OnActiveProjectChanged;
            _activeProject = null;
        }

        _services?.Dispose();
        _services = null;
        _profile?.Dispose();
        _profile = null;
    }
}
