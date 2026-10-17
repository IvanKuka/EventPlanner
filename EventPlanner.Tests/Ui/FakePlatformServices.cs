using EventPlanner.Desktop.Composition;
using EventPlanner.Desktop.Platform;

namespace EventPlanner.Tests.Ui;

/// <summary>Replaces native pickers with predetermined paths; records theme, activation and shutdown requests.</summary>
internal sealed class FakePlatformServices : IPlatformServices
{
    public string? SaveTarget { get; set; }

    public string? RestoreSource { get; set; }

    public int? ShutdownCode { get; private set; }

    public AppTheme? AppliedTheme { get; private set; }

    /// <summary>Whether a simulated earlier instance answers an activation request.</summary>
    public bool RunningInstanceAnswers { get; set; }

    public string? ListeningFor { get; private set; }

    public Task<string?> PickBackupSaveTargetAsync(string suggestedFileName) => Task.FromResult(SaveTarget);

    public Task<string?> PickBackupToRestoreAsync() => Task.FromResult(RestoreSource);

    public Task OpenFolderAsync(string folderPath) => Task.CompletedTask;

    public void ApplyTheme(AppTheme theme) => AppliedTheme = theme;

    public Task<bool> TryActivateRunningInstanceAsync(string profileDirectory) => Task.FromResult(RunningInstanceAnswers);

    public void ListenForActivation(string profileDirectory) => ListeningFor = profileDirectory;

    public void Shutdown(int exitCode) => ShutdownCode = exitCode;
}
