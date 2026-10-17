using EventPlanner.Desktop.Platform;

namespace EventPlanner.Tests.Ui;

/// <summary>Replaces native pickers with predetermined paths; records shutdown requests.</summary>
internal sealed class FakePlatformServices : IPlatformServices
{
    public string? SaveTarget { get; set; }

    public string? RestoreSource { get; set; }

    public int? ShutdownCode { get; private set; }

    public Task<string?> PickBackupSaveTargetAsync(string suggestedFileName) => Task.FromResult(SaveTarget);

    public Task<string?> PickBackupToRestoreAsync() => Task.FromResult(RestoreSource);

    public Task OpenFolderAsync(string folderPath) => Task.CompletedTask;

    public void Shutdown(int exitCode) => ShutdownCode = exitCode;
}
