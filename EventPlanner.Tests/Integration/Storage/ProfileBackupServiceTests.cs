using System.IO.Compression;
using System.Text.Json.Nodes;
using EventPlanner.Contracts.Guests;
using EventPlanner.Contracts.Results;
using EventPlanner.Infrastructure.Backup;
using EventPlanner.Infrastructure.Data;
using EventPlanner.Infrastructure.Profiles;
using EventPlanner.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventPlanner.Tests.Integration.Storage;

public sealed class ProfileBackupServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Backup_restores_into_isolated_profile_with_entities()
    {
        await using var profile = await TestProfile.CreateAsync();
        var project = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;
        await profile.Guests.CreateGuestAsync(Commands.Guest(project.Id, "Anna", RsvpState.Accepted, [project.ProgramParts[0].Id]), Ct);
        var service = Service(profile.Root);
        var archive = ArchivePath(profile.Root);

        var created = await service.CreateAsync(profile.Profile, archive, Ct);
        Assert.True(created.IsSuccess, created.Error?.Detail);

        var restored = await service.RestoreAsNewProfileAsync(archive, Ct);
        Assert.True(restored.IsSuccess, restored.Error?.Detail);
        Assert.Equal(1, restored.Value.EventCount);
        Assert.Equal(1, restored.Value.GuestCount);

        // Open the restored profile through the services and read concrete entities.
        Assert.Equal(DataRoot.DefaultProfileName, profile.Root.ReadActiveProfileName());
        var open = await TestProfile.Opener().OpenAsync(
            restored.Value.ProfileName, profile.Root.ProfileDirectory(restored.Value.ProfileName), Ct);
        using var restoredProfile = open.Profile!;
        var guests = new Core.Services.Guests.GuestService(
            new DataSessionFactory(restoredProfile.Database), NullLogger<Core.Services.Guests.GuestService>.Instance);
        var guest = Assert.Single(await guests.ListGuestsAsync(project.Id, Ct));
        Assert.Equal("Anna", guest.DisplayName);
        Assert.Equal([project.ProgramParts[0].Id], guest.ProgramPartIds);
    }

    [Fact]
    public async Task Archive_with_traversal_entry_is_rejected_before_any_profile_is_created()
    {
        await using var profile = await TestProfile.CreateAsync();
        var archive = await CreateValidArchiveAsync(profile);
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Update))
        {
            zip.CreateEntry("../../evil.txt");
        }

        var result = await Service(profile.Root).RestoreAsNewProfileAsync(archive, Ct);

        Assert.Equal(ErrorCode.UnsupportedInput, result.Error!.Code);
        Assert.Single(Directory.GetDirectories(profile.Root.ProfilesDirectory));
    }

    [Fact]
    public async Task Tampered_database_fails_hash_check()
    {
        await using var profile = await TestProfile.CreateAsync();
        var archive = await CreateValidArchiveAsync(profile);
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Update))
        {
            zip.GetEntry(BackupManifest.DatabasePath)!.Delete();
            var entry = zip.CreateEntry(BackupManifest.DatabasePath);
            await using var stream = await entry.OpenAsync(Ct);
            await stream.WriteAsync(new byte[] { 1, 2, 3 }, Ct);
        }

        var result = await Service(profile.Root).RestoreAsNewProfileAsync(archive, Ct);

        Assert.Equal(ErrorCode.CorruptData, result.Error!.Code);
        Assert.Single(Directory.GetDirectories(profile.Root.ProfilesDirectory));
    }

    [Theory]
    [InlineData("formatVersion", 2, ErrorCode.UnsupportedVersion)]
    [InlineData("dataSchemaVersion", "29991231000000_FromTheFuture", ErrorCode.UnsupportedVersion)]
    [InlineData("appId", "SomethingElse", ErrorCode.UnsupportedInput)]
    public async Task Unknown_manifest_values_are_rejected(string field, object value, ErrorCode expected)
    {
        await using var profile = await TestProfile.CreateAsync();
        var archive = await CreateValidArchiveAsync(profile);
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Update))
        {
            var entry = zip.GetEntry(BackupManifest.ManifestPath)!;
            JsonNode manifest;
            await using (var read = await entry.OpenAsync(Ct))
            {
                manifest = (await JsonNode.ParseAsync(read, cancellationToken: Ct))!;
            }

            manifest[field] = JsonValue.Create(value);
            entry.Delete();
            await using var write = await zip.CreateEntry(BackupManifest.ManifestPath).OpenAsync(Ct);
            await write.WriteAsync(System.Text.Encoding.UTF8.GetBytes(manifest.ToJsonString()), Ct);
        }

        var result = await Service(profile.Root).RestoreAsNewProfileAsync(archive, Ct);

        Assert.Equal(expected, result.Error!.Code);
    }

    [Fact]
    public async Task Missing_or_non_zip_file_is_reported()
    {
        await using var profile = await TestProfile.CreateAsync();
        var service = Service(profile.Root);
        var notZip = Path.Combine(profile.Root.Path, "not-a-backup.epbackup");
        await File.WriteAllTextAsync(notZip, "hello", Ct);

        Assert.Equal(ErrorCode.MissingSource, (await service.RestoreAsNewProfileAsync(notZip + ".missing", Ct)).Error!.Code);
        Assert.Equal(ErrorCode.CorruptData, (await service.RestoreAsNewProfileAsync(notZip, Ct)).Error!.Code);
    }

    private static async Task<string> CreateValidArchiveAsync(TestProfile profile)
    {
        await profile.Events.CreateAsync(Commands.Wedding(), Ct);
        var archive = ArchivePath(profile.Root);
        Assert.True((await Service(profile.Root).CreateAsync(profile.Profile, archive, Ct)).IsSuccess);
        return archive;
    }

    private static string ArchivePath(DataRoot root)
    {
        var dir = Path.Combine(root.Path, "exports");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "záloha s medzerou" + ProfileBackupService.ArchiveExtension);
    }

    private static ProfileBackupService Service(DataRoot root) =>
        new(root, TestProfile.Opener(), TimeProvider.System, NullLogger<ProfileBackupService>.Instance);
}
