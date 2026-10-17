namespace EventPlanner.Contracts.Backup;

public sealed record BackupCreated(string ArchivePath, long SizeBytes, DateTimeOffset CreatedUtc, string SchemaVersion);

/// <summary>
/// A backup restored into a new isolated profile. The active profile is not touched until the
/// user explicitly switches to <paramref name="ProfileName"/>.
/// </summary>
public sealed record RestoredProfile(string ProfileName, DateTimeOffset BackupCreatedUtc, int EventCount, int GuestCount);
