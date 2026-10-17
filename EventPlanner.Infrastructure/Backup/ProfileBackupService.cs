using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using EventPlanner.Contracts.Backup;
using EventPlanner.Contracts.Results;
using EventPlanner.Infrastructure.Data;
using EventPlanner.Infrastructure.Profiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventPlanner.Infrastructure.Backup;

/// <summary>
/// Creates consistent profile backups and restores them into a new isolated profile.
/// Restore never overwrites the active profile; switching is a separate, explicit step.
/// </summary>
public sealed class ProfileBackupService(DataRoot dataRoot, ProfileOpener opener, TimeProvider clock, ILogger<ProfileBackupService> logger)
{
    public const string ArchiveExtension = ".epbackup";

    private static readonly JsonSerializerOptions ManifestJsonOptions = new() { WriteIndented = true };

    public async Task<Result<BackupCreated>> CreateAsync(OpenedProfile profile, string targetPath, CancellationToken cancellationToken)
    {
        var staging = dataRoot.CreateStagingDirectory();
        var partialPath = targetPath + ".partial";
        try
        {
            var snapshotPath = Path.Combine(staging, "snapshot", ProfileDatabase.FileName);
            await SqliteMaintenance.BackupToFileAsync(profile.Database.ConnectionString, snapshotPath, cancellationToken);
            if (!await SqliteMaintenance.IsHealthyAsync(ReadOnly(snapshotPath), cancellationToken))
            {
                return OperationError.Of(ErrorCode.CorruptData, "Snapshot failed integrity check.");
            }

            var createdUtc = clock.GetUtcNow();
            var manifest = new BackupManifest(
                BackupManifest.CurrentFormatVersion,
                BackupManifest.ExpectedAppId,
                Guid.CreateVersion7(),
                createdUtc,
                BackupManifest.ProfileBackupScope,
                profile.SchemaVersion,
                typeof(ProfileBackupService).Assembly.GetName().Version?.ToString() ?? "0.0.0",
                [new BackupFileEntry(
                    BackupManifest.DatabasePath,
                    new FileInfo(snapshotPath).Length,
                    await BackupArchiveReader.ComputeSha256Async(snapshotPath, cancellationToken))]);

            await WriteArchiveAsync(partialPath, manifest, snapshotPath, cancellationToken);

            // Read the archive back through the same validation used by restore before publishing it.
            var verifyDir = Directory.CreateDirectory(Path.Combine(staging, "verify")).FullName;
            var verified = await BackupArchiveReader.ExtractAsync(partialPath, verifyDir, cancellationToken);
            if (!verified.IsSuccess)
            {
                return verified.Error!;
            }

            File.Move(partialPath, targetPath, overwrite: true);
            logger.LogInformation("Profile backup {ExportId} created", manifest.ExportId);
            return new BackupCreated(targetPath, new FileInfo(targetPath).Length, createdUtc, profile.SchemaVersion);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogError(ex, "Backup target is not writable");
            return OperationError.Of(ErrorCode.PermissionDenied, "Backup target is not writable.");
        }
        catch (IOException ex) when (IsDiskFull(ex))
        {
            logger.LogError(ex, "Not enough space for backup");
            return OperationError.Of(ErrorCode.InsufficientSpace, "Not enough space for backup.");
        }
        finally
        {
            TryDelete(partialPath);
            TryDeleteDirectory(staging);
        }
    }

    /// <summary>
    /// Validates the archive, creates a new profile from it, opens it through the normal startup
    /// sequence (migrating an older schema) and reads its entities. The new profile is not activated.
    /// </summary>
    public async Task<Result<RestoredProfile>> RestoreAsNewProfileAsync(string archivePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(archivePath))
        {
            return OperationError.Of(ErrorCode.MissingSource, "Backup file does not exist.");
        }

        var staging = dataRoot.CreateStagingDirectory();
        string? profileDirectory = null;
        var activated = false;
        try
        {
            var extracted = await BackupArchiveReader.ExtractAsync(archivePath, staging, cancellationToken);
            if (!extracted.IsSuccess)
            {
                logger.LogWarning("Backup archive rejected: {Code}", extracted.Error!.Code);
                return extracted.Error!;
            }

            var profileName = "restored-" + clock.GetUtcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                + "-" + Guid.NewGuid().ToString("N")[..6];
            profileDirectory = dataRoot.ProfileDirectory(profileName);
            Directory.CreateDirectory(profileDirectory);
            File.Move(extracted.Value.DatabasePath, Path.Combine(profileDirectory, ProfileDatabase.FileName));

            var open = await opener.OpenAsync(profileName, profileDirectory, cancellationToken);
            if (open.Profile is null)
            {
                return OperationError.Of(
                    open.Status == ProfileOpenStatus.NewerSchema ? ErrorCode.UnsupportedVersion : ErrorCode.CorruptData,
                    $"Restored profile could not be opened: {open.Status}.");
            }

            using (open.Profile)
            {
                // Prove the restored data is usable by reading real entities, not only archive CRCs.
                await using var context = open.Profile.Database.CreateContext();
                var events = await context.EventProjects.CountAsync(cancellationToken);
                var guests = await context.Guests.CountAsync(cancellationToken);
                activated = true;
                logger.LogInformation("Backup restored into profile {Profile}", profileName);
                return new RestoredProfile(profileName, extracted.Value.Manifest.CreatedUtc, events, guests);
            }
        }
        catch (IOException ex) when (IsDiskFull(ex))
        {
            logger.LogError(ex, "Not enough space for restore");
            return OperationError.Of(ErrorCode.InsufficientSpace, "Not enough space for restore.");
        }
        finally
        {
            TryDeleteDirectory(staging);

            // The half-created profile lives under our own profiles root and was created by this call.
            if (!activated && profileDirectory is not null)
            {
                TryDeleteDirectory(profileDirectory);
            }
        }
    }

    private static async Task WriteArchiveAsync(string path, BackupManifest manifest, string databasePath, CancellationToken cancellationToken)
    {
        await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await using var archive = new ZipArchive(file, ZipArchiveMode.Create);

        var manifestEntry = archive.CreateEntry(BackupManifest.ManifestPath, CompressionLevel.Optimal);
        await using (var stream = await manifestEntry.OpenAsync(cancellationToken))
        {
            await JsonSerializer.SerializeAsync(stream, manifest, ManifestJsonOptions, cancellationToken);
        }

        var dbEntry = archive.CreateEntry(BackupManifest.DatabasePath, CompressionLevel.Optimal);
        await using (var target = await dbEntry.OpenAsync(cancellationToken))
        await using (var source = File.OpenRead(databasePath))
        {
            await source.CopyToAsync(target, cancellationToken);
        }
    }

    private static string ReadOnly(string path) => ProfileDatabase.BuildConnectionString(path, Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly);

    // ERROR_DISK_FULL / ERROR_HANDLE_DISK_FULL on Windows, ENOSPC on Unix.
    private static bool IsDiskFull(IOException ex) => (ex.HResult & 0xFFFF) is 0x70 or 0x27 || ex.HResult == 28;

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Leftover .partial files are harmless and never mistaken for a finished archive.
        }
    }

    private void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Temporary directory could not be removed");
        }
    }
}
