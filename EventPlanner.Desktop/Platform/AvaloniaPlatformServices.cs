using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using EventPlanner.Desktop.Composition;
using EventPlanner.Desktop.Localization;
using EventPlanner.Infrastructure.Backup;
using Microsoft.Extensions.Logging;

namespace EventPlanner.Desktop.Platform;

internal sealed class AvaloniaPlatformServices(Window window, IClassicDesktopStyleApplicationLifetime lifetime, ILogger logger)
    : IPlatformServices, IDisposable
{
    private static readonly FilePickerFileType BackupFileType = new(Strings.Backup_FileTypeName)
    {
        Patterns = ["*" + ProfileBackupService.ArchiveExtension],
    };

    private SingleInstanceChannel? _channel;

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

    public void ApplyTheme(AppTheme theme) =>
        Application.Current!.RequestedThemeVariant = theme switch
        {
            AppTheme.Light => ThemeVariant.Light,
            AppTheme.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };

    public async Task<bool> TryActivateRunningInstanceAsync(string profileDirectory)
    {
        using var channel = new SingleInstanceChannel(profileDirectory, logger);
        return await channel.TryActivateRunningInstanceAsync(CancellationToken.None);
    }

    public void ListenForActivation(string profileDirectory)
    {
        _channel?.Dispose();
        _channel = new SingleInstanceChannel(profileDirectory, logger);
        _channel.Listen(() => Dispatcher.UIThread.Post(BringToFront));
    }

    public void Shutdown(int exitCode) => lifetime.Shutdown(exitCode);

    public void Dispose() => _channel?.Dispose();

    private void BringToFront()
    {
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        // The OS may refuse focus to a background process; a brief Topmost makes the window visible anyway.
        window.Topmost = true;
        window.Activate();
        window.Topmost = false;
    }
}
