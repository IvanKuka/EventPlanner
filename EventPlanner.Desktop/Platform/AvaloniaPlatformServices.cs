using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using EventPlanner.Desktop.Localization;
using EventPlanner.Infrastructure.Backup;

namespace EventPlanner.Desktop.Platform;

internal sealed class AvaloniaPlatformServices(Window window, IClassicDesktopStyleApplicationLifetime lifetime) : IPlatformServices
{
    private static readonly FilePickerFileType BackupFileType = new(Strings.Backup_FileTypeName)
    {
        Patterns = ["*" + ProfileBackupService.ArchiveExtension],
    };

    public async Task<string?> PickBackupSaveTargetAsync(string suggestedFileName)
    {
        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedFileName,
            DefaultExtension = ProfileBackupService.ArchiveExtension.TrimStart('.'),
            FileTypeChoices = [BackupFileType],
            ShowOverwritePrompt = true,
        });
        return file?.TryGetLocalPath();
    }

    public async Task<string?> PickBackupToRestoreAsync()
    {
        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = [BackupFileType],
        });
        return files.Count == 1 ? files[0].TryGetLocalPath() : null;
    }

    public async Task OpenFolderAsync(string folderPath)
    {
        var launcher = TopLevel.GetTopLevel(window)?.Launcher;
        if (launcher is not null && Directory.Exists(folderPath))
        {
            await launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(folderPath));
        }
    }

    public void Shutdown(int exitCode) => lifetime.Shutdown(exitCode);
}
