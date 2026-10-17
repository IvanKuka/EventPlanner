namespace EventPlanner.Desktop.Platform;

/// <summary>
/// OS integration used by view models: native file pickers, opening folders via the platform
/// launcher (never a shell command) and application shutdown.
/// </summary>
public interface IPlatformServices
{
    Task<string?> PickBackupSaveTargetAsync(string suggestedFileName);

    Task<string?> PickBackupToRestoreAsync();

    Task OpenFolderAsync(string folderPath);

    void Shutdown(int exitCode);
}
