using EventPlanner.Infrastructure.Data;
using EventPlanner.Infrastructure.Profiles;
using EventPlanner.Tests.Fixtures;
using Microsoft.Data.Sqlite;

namespace EventPlanner.Tests.Integration.Storage;

public sealed class ProfileOpenerTests : IDisposable
{
    private readonly DataRoot _root = TestProfile.NewRoot();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string ProfileDir => _root.ProfileDirectory(DataRoot.DefaultProfileName);

    [Fact]
    public async Task New_profile_is_migrated_with_foreign_keys_and_wal()
    {
        using var profile = (await TestProfile.Opener().OpenAsync("default", ProfileDir, Ct)).Profile!;

        await using var connection = new SqliteConnection(profile.Database.ConnectionString);
        await connection.OpenAsync(Ct);
        Assert.Equal(1L, await Scalar(connection, "PRAGMA foreign_keys;"));
        Assert.Equal("wal", await Scalar(connection, "PRAGMA journal_mode;"));
        Assert.Equal(SqliteMaintenance.KnownMigrations[^1], profile.SchemaVersion);
    }

    [Fact]
    public async Task Second_writer_is_refused_while_profile_is_open()
    {
        using var first = (await TestProfile.Opener().OpenAsync("default", ProfileDir, Ct)).Profile!;

        var second = await TestProfile.Opener().OpenAsync("default", ProfileDir, Ct);

        Assert.Equal(ProfileOpenStatus.Locked, second.Status);
        Assert.Null(second.Profile);
    }

    [Fact]
    public async Task Newer_schema_is_left_untouched()
    {
        (await TestProfile.Opener().OpenAsync("default", ProfileDir, Ct)).Profile!.Dispose();
        var dbPath = Path.Combine(ProfileDir, ProfileDatabase.FileName);
        await using (var connection = new SqliteConnection(ProfileDatabase.BuildConnectionString(dbPath, SqliteOpenMode.ReadWrite)))
        {
            await connection.OpenAsync(Ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('29991231000000_FromTheFuture', '99.0.0');";
            await command.ExecuteNonQueryAsync(Ct);
        }

        var hashBefore = await FileHash(dbPath);
        var result = await TestProfile.Opener().OpenAsync("default", ProfileDir, Ct);

        Assert.Equal(ProfileOpenStatus.NewerSchema, result.Status);
        Assert.Equal(hashBefore, await FileHash(dbPath));
    }

    [Fact]
    public async Task Damaged_database_file_is_reported_as_corrupt()
    {
        Directory.CreateDirectory(ProfileDir);
        var dbPath = Path.Combine(ProfileDir, ProfileDatabase.FileName);
        await File.WriteAllBytesAsync(dbPath, [.. Enumerable.Repeat((byte)0x42, 8192)], Ct);

        var result = await TestProfile.Opener().OpenAsync("default", ProfileDir, Ct);

        Assert.Equal(ProfileOpenStatus.Corrupt, result.Status);
        Assert.Null(result.Profile);

        // The lock must be released so the user can choose another profile or restore.
        using var relock = ProfileLock.TryAcquire(ProfileDir);
        Assert.NotNull(relock);
    }

    [Fact]
    public void Active_profile_pointer_rejects_unsafe_names()
    {
        Assert.False(DataRoot.IsValidProfileName("../evil"));
        Assert.False(DataRoot.IsValidProfileName("C:\\x"));
        Assert.True(DataRoot.IsValidProfileName("restored-20270101-000000-abcdef"));
        Assert.Equal(DataRoot.DefaultProfileName, _root.ReadActiveProfileName());
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root.Path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static async Task<object?> Scalar(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(Ct);
    }

    private static async Task<string> FileHash(string path) =>
        Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(File.OpenRead(path), Ct));
}
