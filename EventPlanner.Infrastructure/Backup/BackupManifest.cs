using System.Text.Json.Serialization;

namespace EventPlanner.Infrastructure.Backup;

/// <summary>
/// manifest.json of a profile backup archive. Field names are explicit so the format stays stable
/// independently of C# renames. A profile backup holds a DB snapshot and is meant for restore by a
/// compatible app version; it is not the public project transfer format.
/// </summary>
internal sealed record BackupManifest(
    [property: JsonPropertyName("formatVersion")] int FormatVersion,
    [property: JsonPropertyName("appId")] string AppId,
    [property: JsonPropertyName("exportId")] Guid ExportId,
    [property: JsonPropertyName("createdUtc")] DateTimeOffset CreatedUtc,
    [property: JsonPropertyName("exportScope")] string ExportScope,
    [property: JsonPropertyName("dataSchemaVersion")] string DataSchemaVersion,
    [property: JsonPropertyName("appVersion")] string AppVersion,
    [property: JsonPropertyName("files")] IReadOnlyList<BackupFileEntry> Files)
{
    public const int CurrentFormatVersion = 1;
    public const string ExpectedAppId = "EventPlanner";
    public const string ProfileBackupScope = "profile-backup";
    public const string ManifestPath = "manifest.json";
    public const string DatabasePath = "data/eventplanner.db";
}

internal sealed record BackupFileEntry(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("sha256")] string Sha256);
