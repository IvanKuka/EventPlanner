using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EventPlanner.Infrastructure.Data;

/// <summary>Low-level SQLite operations that EF Core does not cover.</summary>
internal static class SqliteMaintenance
{
    /// <summary>Migration ids compiled into this application, oldest first.</summary>
    public static IReadOnlyList<string> KnownMigrations { get; } = LoadKnownMigrations();

    /// <summary>
    /// Consistent copy through the online backup API. Copying the .db file directly is unsafe in WAL mode.
    /// </summary>
    public static async Task BackupToFileAsync(string sourceConnectionString, string destinationPath, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        await using var source = new SqliteConnection(sourceConnectionString);
        await source.OpenAsync(cancellationToken);
        await using var destination = new SqliteConnection(
            ProfileDatabase.BuildConnectionString(destinationPath, SqliteOpenMode.ReadWriteCreate));
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);

        // The snapshot is a standalone file: switch it out of WAL so it is a single self-contained file.
        await ExecuteAsync(destination, "PRAGMA journal_mode=DELETE;", cancellationToken);
    }

    /// <summary>True when integrity_check reports "ok" and foreign_key_check finds no violations.</summary>
    public static async Task<bool> IsHealthyAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var integrity = connection.CreateCommand())
        {
            integrity.CommandText = "PRAGMA integrity_check;";
            var result = await integrity.ExecuteScalarAsync(cancellationToken) as string;
            if (!string.Equals(result, "ok", StringComparison.Ordinal))
            {
                return false;
            }
        }

        await using var foreignKeys = connection.CreateCommand();
        foreignKeys.CommandText = "PRAGMA foreign_key_check;";
        await using var reader = await foreignKeys.ExecuteReaderAsync(cancellationToken);
        return !await reader.ReadAsync(cancellationToken);
    }

    public static async Task EnableWalAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await ExecuteAsync(connection, "PRAGMA journal_mode=WAL;", cancellationToken);
    }

    private static string[] LoadKnownMigrations()
    {
        // Reading the migration list does not open a connection.
        using var context = new ProfileDatabase(":memory:").CreateContext();
        return [.. context.Database.GetMigrations()];
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
