using EventPlanner.Contracts.Guests;
using EventPlanner.Contracts.Results;
using EventPlanner.Tests.Fixtures;

namespace EventPlanner.Tests.Integration.Storage;

public sealed class GuestServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Guest_round_trip_keeps_scope_and_private_note_only_in_detail()
    {
        await using var profile = await TestProfile.CreateAsync();
        var project = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;
        var dinner = project.ProgramParts[1].Id;
        var created = (await profile.Guests.CreateGuestAsync(
            Commands.Guest(project.Id, "Mária", RsvpState.Tentative, [dinner]) with { PrivateNote = "alergia na orechy" }, Ct)).Value;

        await profile.ReopenAsync();
        var listed = Assert.Single(await profile.Guests.ListGuestsAsync(project.Id, Ct));
        var detail = (await profile.Guests.GetGuestDetailAsync(created.Id, Ct)).Value;

        Assert.Equal(RsvpState.Tentative, listed.RsvpState);
        Assert.Equal([dinner], listed.ProgramPartIds);
        Assert.True(listed.HasPrivateNote);
        Assert.Equal("alergia na orechy", detail.PrivateNote);
    }

    [Fact]
    public async Task Duplicate_name_is_a_warning_not_a_block()
    {
        await using var profile = await TestProfile.CreateAsync();
        var project = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;

        var first = await profile.Guests.CreateGuestAsync(Commands.Guest(project.Id, "Peter Novák", RsvpState.Invited, []), Ct);
        var second = await profile.Guests.CreateGuestAsync(Commands.Guest(project.Id, " peter novák ", RsvpState.Invited, []), Ct);

        Assert.Empty(first.Warnings);
        Assert.True(second.IsSuccess);
        Assert.Contains(second.Warnings, w => w.Code == WarningCodes.DuplicateGuestName);
        Assert.Equal(2, (await profile.Guests.ListGuestsAsync(project.Id, Ct)).Count);
    }

    [Fact]
    public async Task Group_rsvp_applies_only_to_listed_members()
    {
        await using var profile = await TestProfile.CreateAsync();
        var project = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;
        var parts = project.ProgramParts.Select(p => p.Id).ToArray();
        var group = (await profile.Guests.CreateGroupAsync(new CreateGroup(project.Id, "Rodina Kováčová", null, null, null, null), Ct)).Value;
        var mom = (await profile.Guests.CreateGuestAsync(Commands.Guest(project.Id, "Mama", RsvpState.Invited, parts, group.Id), Ct)).Value;
        var dad = (await profile.Guests.CreateGuestAsync(Commands.Guest(project.Id, "Otec", RsvpState.Invited, parts, group.Id), Ct)).Value;
        var son = (await profile.Guests.CreateGuestAsync(Commands.Guest(project.Id, "Syn", RsvpState.Invited, parts, group.Id), Ct)).Value;

        // The son is an exception: he is deliberately not listed.
        var result = await profile.Guests.SetGroupRsvpAsync(
            new SetGroupRsvp(group.Id, RsvpState.Accepted, [new(mom.Id, mom.Revision), new(dad.Id, dad.Revision)]), Ct);
        Assert.True(result.IsSuccess);

        await profile.ReopenAsync();
        var guests = (await profile.Guests.ListGuestsAsync(project.Id, Ct)).ToDictionary(g => g.Id);
        Assert.Equal(RsvpState.Accepted, guests[mom.Id].RsvpState);
        Assert.Equal(RsvpState.Accepted, guests[dad.Id].RsvpState);
        Assert.Equal(RsvpState.Invited, guests[son.Id].RsvpState);
    }

    [Fact]
    public async Task Group_rsvp_with_stale_member_revision_changes_nobody()
    {
        await using var profile = await TestProfile.CreateAsync();
        var project = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;
        var group = (await profile.Guests.CreateGroupAsync(new CreateGroup(project.Id, "Skupina", null, null, null, null), Ct)).Value;
        var a = (await profile.Guests.CreateGuestAsync(Commands.Guest(project.Id, "A", RsvpState.Invited, [], group.Id), Ct)).Value;
        var b = (await profile.Guests.CreateGuestAsync(Commands.Guest(project.Id, "B", RsvpState.Invited, [], group.Id), Ct)).Value;

        await profile.Guests.UpdateGuestAsync(
            new UpdateGuest(b.Id, b.Revision, group.Id, "B", GuestKind.Named, null, RsvpState.Declined, [], null), Ct);

        var result = await profile.Guests.SetGroupRsvpAsync(
            new SetGroupRsvp(group.Id, RsvpState.Accepted, [new(a.Id, a.Revision), new(b.Id, b.Revision)]), Ct);

        Assert.Equal(ErrorCode.Conflict, result.Error!.Code);
        var guests = (await profile.Guests.ListGuestsAsync(project.Id, Ct)).ToDictionary(g => g.Id);
        Assert.Equal(RsvpState.Invited, guests[a.Id].RsvpState);
        Assert.Equal(RsvpState.Declined, guests[b.Id].RsvpState);
    }

    [Fact]
    public async Task Cross_event_group_and_program_part_links_are_rejected()
    {
        await using var profile = await TestProfile.CreateAsync();
        var first = (await profile.Events.CreateAsync(Commands.Wedding("Prvá"), Ct)).Value;
        var second = (await profile.Events.CreateAsync(Commands.Wedding("Druhá"), Ct)).Value;
        var foreignGroup = (await profile.Guests.CreateGroupAsync(new CreateGroup(second.Id, "Cudzia", null, null, null, null), Ct)).Value;

        var withForeignGroup = await profile.Guests.CreateGuestAsync(
            Commands.Guest(first.Id, "X", RsvpState.Invited, [], foreignGroup.Id), Ct);
        var withForeignPart = await profile.Guests.CreateGuestAsync(
            Commands.Guest(first.Id, "Y", RsvpState.Invited, [second.ProgramParts[0].Id]), Ct);

        Assert.Contains(withForeignGroup.Error!.Issues, i => i.Rule == ValidationRules.UnknownReference);
        Assert.Contains(withForeignPart.Error!.Issues, i => i.Rule == ValidationRules.UnknownReference);
        Assert.Empty(await profile.Guests.ListGuestsAsync(first.Id, Ct));
    }

    [Fact]
    public async Task Deleting_group_keeps_its_guests()
    {
        await using var profile = await TestProfile.CreateAsync();
        var project = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;
        var group = (await profile.Guests.CreateGroupAsync(new CreateGroup(project.Id, "Skupina", "Kontakt", "a@b.sk", null, null), Ct)).Value;
        await profile.Guests.CreateGuestAsync(Commands.Guest(project.Id, "Člen", RsvpState.Accepted, [], group.Id), Ct);

        Assert.True((await profile.Guests.DeleteGroupAsync(new DeleteGroup(group.Id), Ct)).IsSuccess);

        await profile.ReopenAsync();
        var guest = Assert.Single(await profile.Guests.ListGuestsAsync(project.Id, Ct));
        Assert.Null(guest.GroupId);
        Assert.Empty(await profile.Guests.ListGroupsAsync(project.Id, Ct));
    }

    [Fact]
    public async Task Counts_are_derived_from_stored_guests()
    {
        await using var profile = await TestProfile.CreateAsync();
        var project = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;
        var ceremony = project.ProgramParts[0].Id;
        var dinner = project.ProgramParts[1].Id;
        await profile.Guests.CreateGuestAsync(Commands.Guest(project.Id, "A", RsvpState.Accepted, [ceremony, dinner]), Ct);
        await profile.Guests.CreateGuestAsync(Commands.Guest(project.Id, "B", RsvpState.Accepted, [ceremony]), Ct);
        await profile.Guests.CreateGuestAsync(Commands.Guest(project.Id, "+1", RsvpState.Accepted, [dinner], kind: GuestKind.PlusOnePlaceholder), Ct);

        var counts = (await profile.Guests.GetCountsAsync(project.Id, Ct)).Value;

        Assert.Equal(2, counts.Parts.Single(p => p.ProgramPartId == ceremony).Accepted);
        var dinnerCounts = counts.Parts.Single(p => p.ProgramPartId == dinner);
        Assert.Equal(2, dinnerCounts.Accepted);
        Assert.Equal(1, dinnerCounts.AcceptedPlaceholders);
    }
}
