using EventPlanner.Contracts.Budget;
using EventPlanner.Contracts.Events;
using EventPlanner.Contracts.Guests;
using EventPlanner.Contracts.Results;
using EventPlanner.Contracts.Tasks;
using EventPlanner.Contracts.Vendors;
using EventPlanner.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace EventPlanner.Tests.Integration.Storage;

/// <summary>Permanent project delete over SQLite: everything the project owns goes, nothing else does.</summary>
public sealed class EventProjectDeleteTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Delete_removes_the_project_with_all_it_owns_and_keeps_other_projects()
    {
        await using var profile = await TestProfile.CreateAsync();
        var doomed = await FillAsync(profile, "Svadba");
        var kept = await FillAsync(profile, "Krstiny");
        var before = await CountRowsAsync(profile);
        Assert.All(before, table => Assert.True(table.Value > 0, table.Key));

        var result = await profile.Events.DeleteAsync(new DeleteEventProject(doomed.Id, doomed.Revision, "  Svadba "), Ct);

        Assert.True(result.IsSuccess, result.Error?.Detail);
        await profile.ReopenAsync();
        Assert.Equal(ErrorCode.NotFound, (await profile.Events.GetAsync(doomed.Id, Ct)).Error?.Code);

        // Every owned table lost exactly the deleted project's half; the other project is intact.
        var after = await CountRowsAsync(profile);
        Assert.All(before, table => Assert.Equal(table.Value / 2, after[table.Key]));
        Assert.Single(await profile.Guests.ListGuestsAsync(kept.Id, Ct));
        Assert.Single((await profile.Budget.GetOverviewAsync(kept.Id, Ct)).Value.Items);
    }

    [Fact]
    public async Task Wrong_name_or_stale_revision_deletes_nothing()
    {
        await using var profile = await TestProfile.CreateAsync();
        var project = await FillAsync(profile, "Svadba");

        var wrongName = await profile.Events.DeleteAsync(new DeleteEventProject(project.Id, project.Revision, "svadba"), Ct);
        var stale = await profile.Events.DeleteAsync(new DeleteEventProject(project.Id, project.Revision - 1, "Svadba"), Ct);

        Assert.Equal(ValidationRules.ConfirmationMismatch, Assert.Single(wrongName.Error!.Issues).Rule);
        Assert.Equal(ErrorCode.Conflict, stale.Error?.Code);
        Assert.True((await profile.Events.GetAsync(project.Id, Ct)).IsSuccess);
        Assert.Single(await profile.Guests.ListGuestsAsync(project.Id, Ct));
    }

    [Fact]
    public async Task Archived_project_can_be_deleted()
    {
        await using var profile = await TestProfile.CreateAsync();
        var project = (await profile.Events.CreateAsync(Commands.Wedding("Stará svadba"), Ct)).Value;
        await BudgetServiceTests.ArchiveAsync(profile, project.Id);
        var archived = (await profile.Events.GetAsync(project.Id, Ct)).Value;

        var result = await profile.Events.DeleteAsync(new DeleteEventProject(archived.Id, archived.Revision, "Stará svadba"), Ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(await profile.Events.ListAsync(Ct));
    }

    /// <summary>One row in every table a project owns, directly or through its children.</summary>
    private static async Task<EventProjectDto> FillAsync(TestProfile profile, string name)
    {
        var project = (await profile.Events.CreateAsync(Commands.Wedding(name), Ct)).Value;
        var group = (await profile.Guests.CreateGroupAsync(new CreateGroup(project.Id, "Rodina", null, null, null, null), Ct)).Value;
        Assert.True((await profile.Guests.CreateGuestAsync(
            Commands.Guest(project.Id, "Hosť", RsvpState.Accepted, [project.ProgramParts[0].Id], group.Id), Ct)).IsSuccess);
        var vendor = (await profile.Vendors.CreateVendorAsync(new CreateVendor(project.Id, "Výzdoba", "Kvety", null, null), Ct)).Value;
        var quote = (await profile.Vendors.AddQuoteAsync(new AddQuote(vendor.Id, Commands.Quote(300_00)), Ct)).Value;
        var selection = (await profile.Quotes.SelectQuoteAsync(new SelectQuote(quote.Id, quote.Revision, BudgetImpactPolicy.CreateBudgetItem), Ct)).Value;
        Assert.True((await profile.Budget.RecordPaymentAsync(
            new RecordPayment(selection.BudgetItemId!.Value, Guid.NewGuid(), PaymentKind.Deposit, 100_00, PaymentStatus.Posted, new DateOnly(2027, 1, 1), null, null), Ct)).IsSuccess);
        var first = (await profile.Tasks.CreateAsync(new CreateTask(project.Id, new TaskInput("Vybrať sálu", null, null, TaskPriority.Normal, null, null)), Ct)).Value;
        Assert.True((await profile.Tasks.CreateAsync(new CreateTask(project.Id, new TaskInput("Pozvánky", null, null, TaskPriority.High, first.Id, null)), Ct)).IsSuccess);
        return (await profile.Events.GetAsync(project.Id, Ct)).Value;
    }

    private static async Task<Dictionary<string, int>> CountRowsAsync(TestProfile profile)
    {
        await using var db = profile.Profile.Database.CreateContext();
        return new Dictionary<string, int>
        {
            ["EventProjects"] = await db.EventProjects.CountAsync(Ct),
            ["ProgramParts"] = await db.ProgramParts.CountAsync(Ct),
            ["ParticipantGroups"] = await db.ParticipantGroups.CountAsync(Ct),
            ["Guests"] = await db.Guests.CountAsync(Ct),
            ["GuestAttendances"] = await db.GuestAttendances.CountAsync(Ct),
            ["Vendors"] = await db.Vendors.CountAsync(Ct),
            ["Quotes"] = await db.Quotes.CountAsync(Ct),
            ["QuoteDecisions"] = await db.QuoteDecisions.CountAsync(Ct),
            ["BudgetItems"] = await db.BudgetItems.CountAsync(Ct),
            ["Payments"] = await db.Payments.CountAsync(Ct),
            ["Tasks"] = await db.Tasks.CountAsync(Ct),
        };
    }
}
