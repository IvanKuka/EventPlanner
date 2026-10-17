using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EventPlanner.Infrastructure.Data;

/// <summary>
/// Connection settings for one profile database file. Foreign keys are always on; pooling is off
/// so that backup, restore and tests can reliably release the file.
/// </summary>
public sealed class ProfileDatabase
{
    public const string FileName = "eventplanner.db";

    public ProfileDatabase(string databasePath)
    {
        DatabasePath = databasePath;
        ConnectionString = BuildConnectionString(databasePath, SqliteOpenMode.ReadWriteCreate);
        Options = new DbContextOptionsBuilder<EventPlannerDbContext>()
            .UseSqlite(ConnectionString)
            .Options;
    }

    public string DatabasePath { get; }

    public string ConnectionString { get; }

    public DbContextOptions<EventPlannerDbContext> Options { get; }

    public EventPlannerDbContext CreateContext() => new(Options);

    public static string BuildConnectionString(string databasePath, SqliteOpenMode mode) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = mode,
            ForeignKeys = true,
            Pooling = false,

            // Microsoft.Data.Sqlite retries SQLITE_BUSY until this timeout (seconds).
            DefaultTimeout = 5,
        }.ToString();
}
