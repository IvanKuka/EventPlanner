using System.Globalization;
using EventPlanner.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventPlanner.Infrastructure.Profiles;

/// <summary>
/// Startup sequence from 09-storage-portability.md: lock → check schema compatibility →
/// pre-migration backup (with space check) → migrate → integrity and FK check → Ready.
/// A profile that fails any step is never returned for normal writing.
/// </summary>
public sealed class ProfileOpener(TimeProvider clock, ILogger<ProfileOpener> logger)
{
    public const int PreMigrationBackupsToKeep = 5;
    private const string BackupsDirectoryName = "backups";
    private const long SpaceMarginBytes = 20L * 1024 * 1024;

    public async Task<ProfileOpenResult> OpenAsync(string profileName, string profileDirectory, CancellationToken cancellationToken)
    {
        var profileLock = ProfileLock.TryAcquire(profileDirectory);
        if (profileLock is null)
        {
            logger.LogWarning("Profile {Profile} is locked by another instance", profileName);
            return new ProfileOpenResult(ProfileOpenStatus.Locked, null, null);
        }

        try
        {
            var result = await OpenLockedAsync(profileName, profileDirectory, profileLock, cancellationToken);
            if (result.Profile is null)
            {
                profileLock.Dispose();
            }

            return result;
        }
        catch (SqliteException ex)
        {
            // e.g. "file is not a database": treat as damaged data, never as a fresh profile.
            logger.LogError(ex, "Profile {Profile} could not be read (SQLite error {Code})", profileName, ex.SqliteErrorCode);
            profileLock.Dispose();
            return new ProfileOpenResult(ProfileOpenStatus.Corrupt, null, null);
        }
        catch
        {
            profileLock.Dispose();
            throw;
        }
    }

    private async Task<ProfileOpenResult> OpenLockedAsync(
        string profileName, string profileDirectory, ProfileLock profileLock, CancellationToken cancellationToken)
    {
        var database = new ProfileDatabase(Path.Combine(profileDirectory, ProfileDatabase.FileName));
        var existed = File.Exists(database.DatabasePath);
        string? backupPath = null;

        await using (var context = database.CreateContext())
        {
            var applied = existed ? (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList() : [];
            if (applied.Except(SqliteMaintenance.KnownMigrations).Any())
            {
                logger.LogWarning("Profile {Profile} has a newer schema than this application", profileName);
                return new ProfileOpenResult(ProfileOpenStatus.NewerSchema, null, null);
            }

            var pending = SqliteMaintenance.KnownMigrations.Except(applied).ToList();
            if (pending.Count > 0 && applied.Count > 0)
            {
                if (!HasSpaceFor(database.DatabasePath))
                {
                    return new ProfileOpenResult(ProfileOpenStatus.InsufficientSpace, null, null);
                }

                backupPath = await CreatePreMigrationBackupAsync(database, profileDirectory, applied[^1], cancellationToken);
            }

            if (pending.Count > 0)
            {
                logger.LogInformation("Applying {Count} migrations to profile {Profile}", pending.Count, profileName);
                await context.Database.MigrateAsync(cancellationToken);
            }
        }

        await SqliteMaintenance.EnableWalAsync(database.ConnectionString, cancellationToken);
        if (!await SqliteMaintenance.IsHealthyAsync(database.ConnectionString, cancellationToken))
        {
            logger.LogError("Profile {Profile} failed integrity check", profileName);
            return new ProfileOpenResult(ProfileOpenStatus.Corrupt, null, backupPath);
        }

        var schemaVersion = SqliteMaintenance.KnownMigrations[^1];
        logger.LogInformation("Profile {Profile} ready at schema {Schema}", profileName, schemaVersion);
        return new ProfileOpenResult(
            ProfileOpenStatus.Ready,
            new OpenedProfile(profileName, profileDirectory, database, schemaVersion, profileLock),
            backupPath);
    }

    private async Task<string> CreatePreMigrationBackupAsync(
        ProfileDatabase database, string profileDirectory, string fromSchema, CancellationToken cancellationToken)
    {
        var backupsDirectory = Path.Combine(profileDirectory, BackupsDirectoryName);
        var stamp = clock.GetUtcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var backupPath = Path.Combine(backupsDirectory, $"pre-migration-{stamp}-{fromSchema}.db");
        await SqliteMaintenance.BackupToFileAsync(database.ConnectionString, backupPath, cancellationToken);

        if (!await SqliteMaintenance.IsHealthyAsync(
                ProfileDatabase.BuildConnectionString(backupPath, SqliteOpenMode.ReadOnly), cancellationToken))
        {
            throw new IOException("Pre-migration backup failed verification; migration was not started.");
        }

        PruneOldBackups(backupsDirectory);
        logger.LogInformation("Pre-migration backup created");
        return backupPath;
    }

    private static void PruneOldBackups(string backupsDirectory)
    {
        // Only files this class created (fixed prefix inside the profile's own backups folder) are removed.
        var old = new DirectoryInfo(backupsDirectory)
            .GetFiles("pre-migration-*.db")
            .OrderByDescending(f => f.Name, StringComparer.Ordinal)
            .Skip(PreMigrationBackupsToKeep);
        foreach (var file in old)
        {
            file.Delete();
        }
    }

    private static bool HasSpaceFor(string databasePath)
    {
        var size = new FileInfo(databasePath).Length;
        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(databasePath))!);
        return drive.AvailableFreeSpace > size + SpaceMarginBytes;
    }
}
