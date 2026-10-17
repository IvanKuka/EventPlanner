using EventPlanner.Contracts.Events;
using EventPlanner.Contracts.Guests;
using EventPlanner.Contracts.Results;
using EventPlanner.Tests.Fixtures;

namespace EventPlanner.Tests.Integration.Storage;

public sealed class EventProjectServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Created_project_survives_reopen_with_template_parts()
    {
        await using var profile = await TestProfile.CreateAsync();
        var created = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;

        await profile.ReopenAsync();
        var loaded = (await profile.Events.GetAsync(created.Id, Ct)).Value;

        Assert.Equal("Svadba test", loaded.Name);
        Assert.Equal("Europe/Bratislava", loaded.TimeZoneId);
        Assert.Equal(new DateOnly(2027, 6, 13), loaded.LocalEndDate);
        Assert.Equal(EventStatus.Draft, loaded.Status);
        Assert.Equal(["Obrad", "Hostina", "Večerná zábava"], loaded.ProgramParts.Select(p => p.Label));
        Assert.Single(await profile.Events.ListAsync(Ct));
    }

    [Fact]
    public async Task Stale_revision_is_rejected_without_writing()
    {
        await using var profile = await TestProfile.CreateAsync();
        var created = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;

        var first = await profile.Events.UpdateAsync(Update(created, "Prvá zmena"), Ct);
        var second = await profile.Events.UpdateAsync(Update(created, "Druhá zmena"), Ct);

        Assert.True(first.IsSuccess);
        Assert.Equal(ErrorCode.Conflict, second.Error!.Code);
        await profile.ReopenAsync();
        Assert.Equal("Prvá zmena", (await profile.Events.GetAsync(created.Id, Ct)).Value.Name);
    }

    [Fact]
    public async Task Concurrent_sessions_cannot_both_commit_the_same_revision()
    {
        await using var profile = await TestProfile.CreateAsync();
        var created = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;

        // Two windows load the same revision; the store-level token must reject the second commit.
        await using var windowA = profile.Sessions.Open();
        await using var windowB = profile.Sessions.Open();
        var a = (await windowA.Events.FindAsync(created.Id, Ct))!;
        var b = (await windowB.Events.FindAsync(created.Id, Ct))!;
        a.UpdateDetails("A", a.EventType, a.LocalStartDate, a.LocalEndDate, a.TimeZoneId, a.VenueText, a.Currency);
        b.UpdateDetails("B", b.EventType, b.LocalStartDate, b.LocalEndDate, b.TimeZoneId, b.VenueText, b.Currency);

        Assert.Equal(Core.Ports.SaveOutcome.Saved, await windowA.SaveChangesAsync(Ct));
        Assert.Equal(Core.Ports.SaveOutcome.Conflict, await windowB.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Ready_requires_confirmation_and_archived_project_is_read_only()
    {
        await using var profile = await TestProfile.CreateAsync();
        var project = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;

        project = (await profile.Events.ChangeStatusAsync(new ChangeEventStatus(project.Id, project.Revision, EventStatus.Planning, false), Ct)).Value;
        var unconfirmed = await profile.Events.ChangeStatusAsync(new ChangeEventStatus(project.Id, project.Revision, EventStatus.Ready, false), Ct);
        Assert.Contains(unconfirmed.Error!.Issues, i => i.Rule == ValidationRules.ConfirmationRequired);

        project = (await profile.Events.ChangeStatusAsync(new ChangeEventStatus(project.Id, project.Revision, EventStatus.Ready, true), Ct)).Value;
        project = (await profile.Events.ChangeStatusAsync(new ChangeEventStatus(project.Id, project.Revision, EventStatus.Completed, false), Ct)).Value;
        project = (await profile.Events.ChangeStatusAsync(new ChangeEventStatus(project.Id, project.Revision, EventStatus.Archived, false), Ct)).Value;

        var edit = await profile.Events.UpdateAsync(Update(project, "Zmena"), Ct);
        var guest = await profile.Guests.CreateGuestAsync(Commands.Guest(project.Id, "Ján", RsvpState.Accepted, []), Ct);
        Assert.Equal(ErrorCode.ReadOnly, edit.Error!.Code);
        Assert.Equal(ErrorCode.ReadOnly, guest.Error!.Code);

        // Explicit unlock returns the project to Completed and makes it writable again.
        project = (await profile.Events.ChangeStatusAsync(new ChangeEventStatus(project.Id, project.Revision, EventStatus.Completed, false), Ct)).Value;
        Assert.False(project.IsReadOnly);
    }

    [Fact]
    public async Task Removing_program_part_removes_it_from_guest_scopes()
    {
        await using var profile = await TestProfile.CreateAsync();
        var project = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;
        var ceremony = project.ProgramParts[0];
        var dinner = project.ProgramParts[1];
        await profile.Guests.CreateGuestAsync(Commands.Guest(project.Id, "Eva", RsvpState.Accepted, [ceremony.Id, dinner.Id]), Ct);

        var result = await profile.Events.RemoveProgramPartAsync(new RemoveProgramPart(project.Id, project.Revision, ceremony.Id), Ct);
        Assert.True(result.IsSuccess);

        await profile.ReopenAsync();
        var guest = Assert.Single(await profile.Guests.ListGuestsAsync(project.Id, Ct));
        Assert.Equal([dinner.Id], guest.ProgramPartIds);
    }

    [Fact]
    public async Task Invalid_create_returns_field_issues()
    {
        await using var profile = await TestProfile.CreateAsync();
        var result = await profile.Events.CreateAsync(Commands.Wedding() with { Name = "", Currency = "eur" }, Ct);

        Assert.Equal(ErrorCode.ValidationFailed, result.Error!.Code);
        Assert.Contains(result.Error.Issues, i => i.Field == nameof(EventProjectDto.Name));
        Assert.Contains(result.Error.Issues, i => i.Field == nameof(EventProjectDto.Currency));
        Assert.Empty(await profile.Events.ListAsync(Ct));
    }

    private static UpdateEventProject Update(EventProjectDto project, string name) => new(
        project.Id, project.Revision, name, project.EventType, project.LocalStartDate, project.LocalEndDate,
        project.TimeZoneId, project.VenueText, project.Currency);
}
