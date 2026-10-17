using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using EventPlanner.Contracts.Budget;
using EventPlanner.Contracts.Vendors;
using EventPlanner.Core.Services.Budget;
using EventPlanner.Core.Services.Events;
using EventPlanner.Core.Services.Guests;
using EventPlanner.Core.Services.Vendors;
using EventPlanner.Infrastructure.Backup;
using EventPlanner.Infrastructure.Data;
using EventPlanner.Infrastructure.Profiles;
using EventPlanner.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventPlanner.Tests.Integration.Storage;

/// <summary>
/// Opening a profile created by an older version (frozen schemas of each released migration, with
/// data): the opener must make a verified pre-migration backup, migrate, and keep every existing record.
/// </summary>
public sealed class ProfileMigrationTests : IDisposable
{
    private const string InitialProfileMigration = "20261017073256_InitialProfile";
    private const string VendorsAndBudgetMigration = "20261017082808_VendorsAndBudget";
    private static readonly Guid SeedEventId = Guid.Parse("0192a000-0000-7000-8000-000000000001");
    private static readonly Guid SeedVendorId = Guid.Parse("0192a000-0000-7000-8000-000000000010");
    private static readonly string[] InitialProfileScripts = ["InitialProfile.sql", "InitialProfile.seed.sql"];
    private static readonly string[] VendorsAndBudgetScripts =
        ["VendorsAndBudget.sql", "InitialProfile.seed.sql", "VendorsAndBudget.seed.sql"];

    private readonly DataRoot _root = TestProfile.NewRoot();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string ProfileDir => _root.ProfileDirectory(DataRoot.DefaultProfileName);

    private string DatabasePath => Path.Combine(ProfileDir, ProfileDatabase.FileName);

    [Fact]
    public async Task Initial_profile_is_backed_up_then_migrated_with_data_preserved()
    {
        await CreateInitialProfileAsync();
        var originalHash = await FileHashAsync(DatabasePath);

        var result = await TestProfile.Opener().OpenAsync(DataRoot.DefaultProfileName, ProfileDir, Ct);

        Assert.Equal(ProfileOpenStatus.Ready, result.Status);
        using var profile = result.Profile!;
        Assert.Equal(SqliteMaintenance.KnownMigrations[^1], profile.SchemaVersion);

        // The backup is a standalone copy of the old schema, taken before anything changed.
        Assert.NotNull(result.PreMigrationBackupPath);
        Assert.True(File.Exists(result.PreMigrationBackupPath));
        Assert.Contains(InitialProfileMigration, Path.GetFileName(result.PreMigrationBackupPath), StringComparison.Ordinal);
        var backupConnection = ProfileDatabase.BuildConnectionString(result.PreMigrationBackupPath!, SqliteOpenMode.ReadOnly);
        Assert.Equal([InitialProfileMigration], await ReadColumnAsync(backupConnection, "SELECT MigrationId FROM __EFMigrationsHistory;"));
        Assert.Equal(["Hosť Prvý"], await ReadColumnAsync(backupConnection, "SELECT DisplayName FROM Guests;"));
        Assert.Empty(await ReadColumnAsync(backupConnection, "SELECT name FROM sqlite_master WHERE name = 'Payments';"));
        Assert.NotEqual(originalHash, await FileHashAsync(DatabasePath));

        // Existing records survive and the new modules work on the migrated profile.
        var sessions = new DataSessionFactory(profile.Database);
        var events = new EventProjectService(sessions, TimeProvider.System, NullLogger<EventProjectService>.Instance);
        var guests = new GuestService(sessions, NullLogger<GuestService>.Instance);
        var budget = new BudgetService(sessions, NullLogger<BudgetService>.Instance);

        var project = (await events.GetAsync(SeedEventId, Ct)).Value;
        Assert.Equal("Stará svadba", project.Name);
        var guest = Assert.Single(await guests.ListGuestsAsync(SeedEventId, Ct));
        Assert.Equal([project.ProgramParts[0].Id], guest.ProgramPartIds);
        Assert.Equal("bezlepková", (await guests.GetGuestDetailAsync(guest.Id, Ct)).Value.PrivateNote);

        var item = await budget.CreateItemAsync(new CreateBudgetItem(SeedEventId, "Catering", "Večera", 1000_00, 900_00, "EUR"), Ct);
        Assert.True(item.IsSuccess);
    }

    [Fact]
    public async Task VendorsAndBudget_profile_migrates_with_vendors_budget_and_payments_preserved()
    {
        await CreateProfileAsync(VendorsAndBudgetScripts);

        var result = await TestProfile.Opener().OpenAsync(DataRoot.DefaultProfileName, ProfileDir, Ct);

        Assert.Equal(ProfileOpenStatus.Ready, result.Status);
        using var profile = result.Profile!;
        Assert.Equal(SqliteMaintenance.KnownMigrations[^1], profile.SchemaVersion);
        Assert.Contains(VendorsAndBudgetMigration, Path.GetFileName(result.PreMigrationBackupPath), StringComparison.Ordinal);

        var sessions = new DataSessionFactory(profile.Database);
        var vendors = new VendorService(sessions, TimeProvider.System);
        var vendor = Assert.Single(await vendors.ListVendorsAsync(SeedEventId, Ct));
        Assert.Equal((SeedVendorId, 1L), (vendor.Id, vendor.Revision));
        var quote = Assert.Single(await vendors.ListQuotesAsync(SeedEventId, Ct));
        Assert.Equal((QuoteStatus.Selected, 32500L), (quote.Status, quote.KnownTotalMinor));

        var overview = (await new BudgetService(sessions, NullLogger<BudgetService>.Instance).GetOverviewAsync(SeedEventId, Ct)).Value;
        var item = Assert.Single(overview.Items);
        Assert.Equal(quote.Id, item.Item.SelectedQuoteId);
        Assert.Equal(10000, overview.ProjectTotals.PaidNetMinor);

        // Migrated vendors take part in optimistic concurrency like new ones.
        var update = new UpdateVendor(vendor.Id, vendor.Revision, vendor.Category, "Nová výzdoba", vendor.Contact, vendor.Notes);
        Assert.Equal(2, (await vendors.UpdateVendorAsync(update, Ct)).Value.Revision);
    }

    [Fact]
    public async Task Migrated_profile_opens_again_without_another_backup()
    {
        await CreateInitialProfileAsync();
        (await TestProfile.Opener().OpenAsync(DataRoot.DefaultProfileName, ProfileDir, Ct)).Profile!.Dispose();

        var second = await TestProfile.Opener().OpenAsync(DataRoot.DefaultProfileName, ProfileDir, Ct);
        using var profile = second.Profile!;

        Assert.Equal(ProfileOpenStatus.Ready, second.Status);
        Assert.Null(second.PreMigrationBackupPath);
        Assert.Single(Directory.GetFiles(Path.Combine(ProfileDir, "backups"), "pre-migration-*.db"));
    }

    [Fact]
    public async Task Backup_archive_with_initial_schema_restores_and_migrates_into_new_profile()
    {
        var archive = await CreateInitialProfileArchiveAsync();
        var service = new ProfileBackupService(_root, TestProfile.Opener(), TimeProvider.System, NullLogger<ProfileBackupService>.Instance);

        var restored = await service.RestoreAsNewProfileAsync(archive, Ct);

        Assert.True(restored.IsSuccess, restored.Error?.Detail);
        Assert.Equal(1, restored.Value.EventCount);
        Assert.Equal(1, restored.Value.GuestCount);

        // The restored profile went through the standard start sequence: backed up, then migrated.
        var restoredDir = _root.ProfileDirectory(restored.Value.ProfileName);
        Assert.Single(Directory.GetFiles(Path.Combine(restoredDir, "backups"), $"pre-migration-*{InitialProfileMigration}.db"));
        var open = await TestProfile.Opener().OpenAsync(restored.Value.ProfileName, restoredDir, Ct);
        using var profile = open.Profile!;
        Assert.Equal(SqliteMaintenance.KnownMigrations[^1], profile.SchemaVersion);

        var sessions = new DataSessionFactory(profile.Database);
        var guests = new GuestService(sessions, NullLogger<GuestService>.Instance);
        Assert.Equal("Hosť Prvý", Assert.Single(await guests.ListGuestsAsync(SeedEventId, Ct)).DisplayName);
        var budget = new BudgetService(sessions, NullLogger<BudgetService>.Instance);
        Assert.True((await budget.CreateItemAsync(new CreateBudgetItem(SeedEventId, "Catering", "Večera", 1000_00, null, "EUR"), Ct)).IsSuccess);
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

    private Task CreateInitialProfileAsync() => CreateProfileAsync(InitialProfileScripts);

    /// <summary>Builds the old profile only from the frozen SQL fixtures, never from current migrations.</summary>
    private Task CreateProfileAsync(string[] scripts)
    {
        Directory.CreateDirectory(ProfileDir);
        return CreateDatabaseAsync(DatabasePath, scripts);
    }

    private static async Task CreateDatabaseAsync(string path, string[] scripts)
    {
        var schemas = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Schemas");
        await using var connection = new SqliteConnection(ProfileDatabase.BuildConnectionString(path, SqliteOpenMode.ReadWriteCreate));
        await connection.OpenAsync(Ct);
        foreach (var file in scripts)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = await File.ReadAllTextAsync(Path.Combine(schemas, file), Ct);
            await command.ExecuteNonQueryAsync(Ct);
        }
    }

    /// <summary>
    /// A .epbackup as the InitialProfile release wrote it. The manifest is spelled out literally so the
    /// test pins the on-disk format rather than reusing the current C# record.
    /// </summary>
    private async Task<string> CreateInitialProfileArchiveAsync()
    {
        var dir = Path.Combine(_root.Path, "old-export");
        Directory.CreateDirectory(dir);
        var databasePath = Path.Combine(dir, "eventplanner.db");
        await CreateDatabaseAsync(databasePath, InitialProfileScripts);

        var manifest = new JsonObject
        {
            ["formatVersion"] = 1,
            ["appId"] = "EventPlanner",
            ["exportId"] = Guid.NewGuid(),
            ["createdUtc"] = DateTimeOffset.UtcNow,
            ["exportScope"] = "profile-backup",
            ["dataSchemaVersion"] = InitialProfileMigration,
            ["appVersion"] = "0.1.0",
            ["files"] = new JsonArray(new JsonObject
            {
                ["path"] = "data/eventplanner.db",
                ["size"] = new FileInfo(databasePath).Length,
                ["sha256"] = await FileHashAsync(databasePath),
            }),
        };

        var archive = Path.Combine(dir, "stara-zaloha" + ProfileBackupService.ArchiveExtension);
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            await using (var stream = await zip.CreateEntry("manifest.json").OpenAsync(Ct))
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes(manifest.ToJsonString()), Ct);
            }

            zip.CreateEntryFromFile(databasePath, "data/eventplanner.db");
        }

        return archive;
    }

    private static async Task<List<string>> ReadColumnAsync(string connectionString, string sql)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var values = new List<string>();
        while (await reader.ReadAsync(Ct))
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    private static async Task<string> FileHashAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(stream, Ct));
    }
}
