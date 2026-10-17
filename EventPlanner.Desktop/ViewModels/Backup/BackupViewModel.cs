using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EventPlanner.Contracts.Backup;
using EventPlanner.Desktop.Localization;
using EventPlanner.Desktop.Platform;
using EventPlanner.Infrastructure.Backup;
using EventPlanner.Infrastructure.Profiles;
using Microsoft.Extensions.Logging;

namespace EventPlanner.Desktop.ViewModels.Backup;

public sealed record DiagnosticItem(string Label, string Value);

/// <summary>
/// Manual backup, restore into a new isolated profile with an explicit switch, and basic diagnostics.
/// A backup on the same disk does not protect against losing the device; the UI says so.
/// </summary>
public sealed partial class BackupViewModel(
    ProfileBackupService backups,
    OpenedProfile profile,
    DataRoot dataRoot,
    IPlatformServices platform,
    TimeProvider clock,
    ILogger<BackupViewModel> logger) : ViewModelBase, IPage
{
    public string Title => Strings.Nav_Backup;

    public ObservableCollection<DiagnosticItem> Diagnostics { get; } = [];

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? LastBackupPath { get; set; }

    [ObservableProperty]
    public partial RestoredProfile? Restored { get; set; }

    [ObservableProperty]
    public partial string? InfoMessage { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public Task ActivateAsync(CancellationToken cancellationToken)
    {
        Diagnostics.Clear();
        Diagnostics.Add(new(Strings.Diag_Version, typeof(BackupViewModel).Assembly.GetName().Version?.ToString() ?? "?"));
        Diagnostics.Add(new(Strings.Diag_Runtime, RuntimeInformation.RuntimeIdentifier));
        Diagnostics.Add(new(Strings.Diag_Os, RuntimeInformation.OSDescription));
        Diagnostics.Add(new(Strings.Diag_Profile, profile.Name));
        Diagnostics.Add(new(Strings.Diag_Schema, profile.SchemaVersion));
        Diagnostics.Add(new(Strings.Diag_DataFolder, dataRoot.Path));
        Diagnostics.Add(new(Strings.Diag_Logs, dataRoot.LogsDirectory));
        Diagnostics.Add(new(Strings.Diag_FreeSpace, FreeSpaceText()));
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        var suggested = $"EventPlanner-{clock.GetLocalNow():yyyy-MM-dd-HHmm}{ProfileBackupService.ArchiveExtension}";
        var target = await platform.PickBackupSaveTargetAsync(suggested);
        if (target is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var result = await Task.Run(() => backups.CreateAsync(profile, target, CancellationToken.None));
            if (!result.IsSuccess)
            {
                ErrorMessage = ErrorText.For(result.Error!);
                return;
            }

            LastBackupPath = result.Value.ArchivePath;
            InfoMessage = string.Format(CultureInfo.CurrentCulture, Strings.Backup_Created, result.Value.ArchivePath, result.Value.SizeBytes / 1024.0);
        });
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        var archive = await platform.PickBackupToRestoreAsync();
        if (archive is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            Restored = null;
            var result = await Task.Run(() => backups.RestoreAsNewProfileAsync(archive, CancellationToken.None));
            if (!result.IsSuccess)
            {
                ErrorMessage = ErrorText.For(result.Error!);
                return;
            }

            Restored = result.Value;
            InfoMessage = string.Format(
                CultureInfo.CurrentCulture, Strings.Backup_Restored, result.Value.EventCount, result.Value.GuestCount, result.Value.BackupCreatedUtc.ToLocalTime());
        });
    }

    /// <summary>
    /// Switches the active profile and closes the app; the current profile stays on disk untouched,
    /// so returning to it remains possible.
    /// </summary>
    [RelayCommand]
    private void SwitchToRestored()
    {
        if (Restored is null)
        {
            return;
        }

        try
        {
            dataRoot.SetActiveProfile(Restored.ProfileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Active profile could not be switched");
            ErrorMessage = Strings.Backup_FileError;
            return;
        }

        platform.Shutdown(0);
    }

    [RelayCommand]
    private Task OpenBackupFolderAsync() =>
        LastBackupPath is null ? Task.CompletedTask : platform.OpenFolderAsync(Path.GetDirectoryName(LastBackupPath)!);

    private async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        ErrorMessage = null;
        InfoMessage = null;
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // e.g. the chosen drive was removed or the file is locked: reported here instead of
            // crashing the app; the active profile is never touched by these operations.
            logger.LogError(ex, "Backup operation failed");
            ErrorMessage = Strings.Backup_FileError;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string FreeSpaceText()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(dataRoot.Path)!);
            return string.Format(CultureInfo.CurrentCulture, "{0:N1} GB", drive.AvailableFreeSpace / 1024.0 / 1024 / 1024);
        }
        catch (IOException)
        {
            return "?";
        }
    }
}
