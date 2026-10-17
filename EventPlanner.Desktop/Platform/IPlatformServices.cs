using EventPlanner.Desktop.Composition;

namespace EventPlanner.Desktop.Platform;

/// <summary>
/// OS integration used by view models: native file pickers, opening folders via the platform
/// launcher (never a shell command), the app-wide theme, single-instance activation and shutdown.
/// </summary>
public interface IPlatformServices
{
    Task<string?> PickBackupSaveTargetAsync(string suggestedFileName);

    Task<string?> PickBackupToRestoreAsync();

    Task OpenFolderAsync(string folderPath);

    void ApplyTheme(AppTheme theme);

    /// <summary>Brings the window already holding this profile to the front; false when none answers.</summary>
    Task<bool> TryActivateRunningInstanceAsync(string profileDirectory);

    /// <summary>From now on, later starts for this profile bring this window to the front.</summary>
    void ListenForActivation(string profileDirectory);

    void Shutdown(int exitCode);
}
