using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using EventPlanner.Contracts.Results;
using EventPlanner.Infrastructure.Data;

namespace EventPlanner.Infrastructure.Backup;

/// <summary>
/// Validates an untrusted backup archive and extracts it into a staging directory. Everything is
/// checked (entry names, counts, sizes, manifest version, schema, hashes) before any profile is created.
/// </summary>
internal static class BackupArchiveReader
{
    public const long MaxDatabaseBytes = 4L * 1024 * 1024 * 1024;
    private const int MaxEntries = 16;
    private const long MaxManifestBytes = 1024 * 1024;

    private static readonly HashSet<string> AllowedEntries =
        new(StringComparer.Ordinal) { BackupManifest.ManifestPath, BackupManifest.DatabasePath };

    public sealed record Extracted(BackupManifest Manifest, string DatabasePath);

    public static async Task<Result<Extracted>> ExtractAsync(string archivePath, string stagingDirectory, CancellationToken cancellationToken)
    {
        ZipArchive archive;
        try
        {
            archive = ZipFile.OpenRead(archivePath);
        }
        catch (InvalidDataException)
        {
            return OperationError.Of(ErrorCode.CorruptData, "Archive is not a readable ZIP file.");
        }

        using (archive)
        {
            var structureError = ValidateStructure(archive);
            if (structureError is not null)
            {
                return structureError;
            }

            var manifestResult = await ReadManifestAsync(archive.GetEntry(BackupManifest.ManifestPath)!, cancellationToken);
            if (!manifestResult.IsSuccess)
            {
                return manifestResult.Error!;
            }

            var manifest = manifestResult.Value;
            var dbEntry = manifest.Files.Single(f => f.Path == BackupManifest.DatabasePath);
            var targetPath = Path.Combine(stagingDirectory, ProfileDatabase.FileName);

            var copied = await CopyBoundedAsync(archive.GetEntry(BackupManifest.DatabasePath)!, targetPath, dbEntry.Size, cancellationToken);
            if (copied != dbEntry.Size)
            {
                return OperationError.Of(ErrorCode.CorruptData, "Database size does not match the manifest.");
            }

            if (!string.Equals(await ComputeSha256Async(targetPath, cancellationToken), dbEntry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return OperationError.Of(ErrorCode.CorruptData, "Database hash does not match the manifest.");
            }

            return new Extracted(manifest, targetPath);
        }
    }

    public static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hash);
    }

    private static OperationError? ValidateStructure(ZipArchive archive)
    {
        if (archive.Entries.Count > MaxEntries)
        {
            return OperationError.Of(ErrorCode.UnsupportedInput, "Archive has too many entries.");
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            // Reject absolute paths, traversal, backslashes and anything outside the fixed layout.
            if (!AllowedEntries.Contains(entry.FullName) || !seen.Add(entry.FullName))
            {
                return OperationError.Of(ErrorCode.UnsupportedInput, "Archive contains an unexpected entry.");
            }
        }

        return seen.Count == AllowedEntries.Count
            ? null
            : OperationError.Of(ErrorCode.MissingSource, "Archive is missing required entries.");
    }

    private static async Task<Result<BackupManifest>> ReadManifestAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        if (entry.Length > MaxManifestBytes)
        {
            return OperationError.Of(ErrorCode.UnsupportedInput, "Manifest is too large.");
        }

        BackupManifest? manifest;
        try
        {
            await using var stream = await entry.OpenAsync(cancellationToken);
            manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(stream, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return OperationError.Of(ErrorCode.CorruptData, "Manifest is not valid JSON.");
        }

        if (manifest is null || manifest.Files is null)
        {
            return OperationError.Of(ErrorCode.CorruptData, "Manifest is empty.");
        }

        if (manifest.AppId != BackupManifest.ExpectedAppId || manifest.ExportScope != BackupManifest.ProfileBackupScope)
        {
            return OperationError.Of(ErrorCode.UnsupportedInput, "Archive is not an EventPlanner profile backup.");
        }

        if (manifest.FormatVersion != BackupManifest.CurrentFormatVersion)
        {
            return OperationError.Of(ErrorCode.UnsupportedVersion, $"Backup format {manifest.FormatVersion} is not supported.");
        }

        if (!SqliteMaintenance.KnownMigrations.Contains(manifest.DataSchemaVersion))
        {
            return OperationError.Of(ErrorCode.UnsupportedVersion, "Backup was created by a newer or unknown schema.");
        }

        var files = manifest.Files.Where(f => f is not null).ToList();
        if (files.Count != 1
            || files[0].Path != BackupManifest.DatabasePath
            || files[0].Size is < 0 or > MaxDatabaseBytes
            || string.IsNullOrEmpty(files[0].Sha256))
        {
            return OperationError.Of(ErrorCode.CorruptData, "Manifest file list is invalid.");
        }

        return manifest;
    }

    /// <summary>Copies at most <paramref name="expectedSize"/> + 1 bytes so a lying entry cannot fill the disk.</summary>
    private static async Task<long> CopyBoundedAsync(ZipArchiveEntry entry, string targetPath, long expectedSize, CancellationToken cancellationToken)
    {
        await using var source = await entry.OpenAsync(cancellationToken);
        await using var target = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > expectedSize)
            {
                return total;
            }

            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return total;
    }
}
