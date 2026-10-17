using EventPlanner.Desktop.Localization;
using EventPlanner.Desktop.ViewModels.Backup;
using EventPlanner.Infrastructure.Backup;
using EventPlanner.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventPlanner.Tests.Ui;

public sealed class BackupViewModelTests
{
    /// <summary>An unwritable target (removed drive, locked file) is a message, not an app crash.</summary>
    [Fact]
    public async Task Unwritable_backup_target_is_reported_and_the_page_stays_usable()
    {
        await using var profile = await TestProfile.CreateAsync();
        var notADirectory = Path.Combine(profile.Root.Path, "plain-file.txt");
        await File.WriteAllTextAsync(notADirectory, "x", TestContext.Current.CancellationToken);
        var platform = new FakePlatformServices { SaveTarget = Path.Combine(notADirectory, "backup" + ProfileBackupService.ArchiveExtension) };
        var backups = new ProfileBackupService(profile.Root, TestProfile.Opener(), TimeProvider.System, NullLogger<ProfileBackupService>.Instance);
        var page = new BackupViewModel(backups, profile.Profile, profile.Root, platform, TimeProvider.System, NullLogger<BackupViewModel>.Instance);

        await page.CreateBackupCommand.ExecuteAsync(null);

        Assert.Equal(Strings.Backup_FileError, page.ErrorMessage);
        Assert.False(page.IsBusy);
        Assert.Null(page.LastBackupPath);
    }
}
