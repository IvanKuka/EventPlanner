using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    }

    public ObservableCollection<IPage> Pages { get; } = [];

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

    public bool IsLoading => State == ShellState.Loading;

    public bool IsReady => State == ShellState.Ready;

    public bool IsFailed => State == ShellState.Failed;

    /// <summary>Completes when startup either succeeded or failed; never throws.</summary>
    public async Task<bool> InitializeAsync(CancellationToken cancellationToken)
    {
        var profileName = _options.DataRoot.ReadActiveProfileName();
        ProfileName = profileName;
        try
        {
            var opener = new ProfileOpener(TimeProvider.System, _loggerFactory.CreateLogger<ProfileOpener>());

            // Migrations and integrity checks must not run on the UI thread.
            var result = await Task.Run(
                () => opener.OpenAsync(profileName, _options.DataRoot.ProfileDirectory(profileName), cancellationToken),
                cancellationToken);
            if (result.Profile is null)
            {
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
            _services = AppServices.BuildForProfile(_options, _loggerFactory, _profile, _platform);
            _activeProject = _services.GetRequiredService<ActiveProjectContext>();
            _activeProject.PropertyChanged += OnActiveProjectChanged;

            Pages.Add(_services.GetRequiredService<EventsViewModel>());
            Pages.Add(_services.GetRequiredService<OverviewViewModel>());
            Pages.Add(_services.GetRequiredService<GuestsViewModel>());
            Pages.Add(_services.GetRequiredService<VendorsViewModel>());
            Pages.Add(_services.GetRequiredService<BudgetViewModel>());
            Pages.Add(_services.GetRequiredService<BackupViewModel>());

            State = ShellState.Ready;
            SelectedPage = Pages[0];
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

    partial void OnStateChanged(ShellState value)
    {
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(IsReady));
        OnPropertyChanged(nameof(IsFailed));
    }

    partial void OnSelectedPageChanged(IPage? value)
    {
        if (value is null)
        {
            return;
        }

        _activationCts?.Cancel();
        _activationCts = new CancellationTokenSource();
        _ = ActivateSafelyAsync(value, _activationCts.Token);
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
