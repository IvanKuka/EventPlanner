using EventPlanner.Infrastructure.Data;

namespace EventPlanner.Infrastructure.Profiles;

/// <summary>A profile that passed the startup sequence and is held under its writer lock.</summary>
public sealed class OpenedProfile : IDisposable
{
    private readonly ProfileLock _lock;

    internal OpenedProfile(string name, string directory, ProfileDatabase database, string schemaVersion, ProfileLock profileLock)
    {
        Name = name;
        Directory = directory;
        Database = database;
        SchemaVersion = schemaVersion;
        _lock = profileLock;
    }

    public string Name { get; }

    public string Directory { get; }

    public ProfileDatabase Database { get; }

    /// <summary>Id of the last applied migration.</summary>
    public string SchemaVersion { get; }

    public void Dispose() => _lock.Dispose();
}

public enum ProfileOpenStatus
{
    Ready,

    /// <summary>Another instance holds the writer lock.</summary>
    Locked,

    /// <summary>The profile was created by a newer app version; it is left untouched.</summary>
    NewerSchema,

    /// <summary>Integrity or foreign key check failed; the profile is not opened for writing.</summary>
    Corrupt,

    InsufficientSpace,
}

public sealed record ProfileOpenResult(ProfileOpenStatus Status, OpenedProfile? Profile, string? PreMigrationBackupPath);
