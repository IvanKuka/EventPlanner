using EventPlanner.Contracts.Budget;
using EventPlanner.Contracts.Events;
using EventPlanner.Contracts.Results;
using EventPlanner.Tests.Fixtures;

namespace EventPlanner.Tests.Integration.Storage;

public sealed class BudgetServiceTests
{
    private static readonly DateOnly PaidDay = new(2027, 2, 1);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>E-A04 end to end over SQLite, verified after reopening the profile.</summary>
    [Fact]
    public async Task Plan_commitment_deposit_balance_and_refund_round_trip_with_separate_totals()
    {
        await using var profile = await TestProfile.CreateAsync();
        var project = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;
        var item = (await profile.Budget.CreateItemAsync(new CreateBudgetItem(project.Id, "Catering", "Večera", 1000_00, 900_00, "EUR"), Ct)).Value;

        Assert.True((await profile.Budget.RecordPaymentAsync(Posted(item.Id, PaymentKind.Deposit, 200_00), Ct)).IsSuccess);
        var balance = (await profile.Budget.RecordPaymentAsync(
            new RecordPayment(item.Id, Guid.NewGuid(), PaymentKind.Balance, 700_00, PaymentStatus.Planned, null, new DateOnly(2027, 6, 1), "VS 2027"), Ct)).Value;

        var beforePosting = (await profile.Budget.GetOverviewAsync(project.Id, Ct)).Value.ProjectTotals;
        Assert.Equal((200_00L, 700_00L, 700_00L), (beforePosting.PaidNetMinor, beforePosting.ScheduledMinor, beforePosting.RemainingMinor));

        Assert.True((await profile.Budget.PostPaymentAsync(new PostPayment(balance.Id, balance.Revision, PaidDay), Ct)).IsSuccess);
        Assert.True((await profile.Budget.RecordPaymentAsync(Posted(item.Id, PaymentKind.Refund, 50_00), Ct)).IsSuccess);

        await profile.ReopenAsync();
        var overview = (await profile.Budget.GetOverviewAsync(project.Id, Ct)).Value;
        var totals = overview.ProjectTotals;
        Assert.Equal(1000_00, totals.EstimatedMinor);
        Assert.Equal(900_00, totals.CommittedMinor);
        Assert.Equal(850_00, totals.PaidNetMinor);
        Assert.Equal(50_00, totals.RefundedMinor);
        Assert.Equal(0, totals.ScheduledMinor);
        Assert.Equal(50_00, totals.RemainingMinor);
        Assert.Equal(0, totals.OverpaidMinor);

        var payments = Assert.Single(overview.Items).Payments;
        Assert.Equal(3, payments.Count);
        Assert.All(payments, p => Assert.True(p.AmountMinor > 0));
        Assert.Equal("VS 2027", payments.Single(p => p.Kind == PaymentKind.Balance).Reference);
    }

    [Fact]
    public async Task Repeated_payment_command_with_same_operation_id_records_one_payment()
    {
        await using var profile = await TestProfile.CreateAsync();
        var (projectId, itemId) = await CreateItemAsync(profile);
        var command = Posted(itemId, PaymentKind.Deposit, 200_00);

        var first = await profile.Budget.RecordPaymentAsync(command, Ct);
        var replay = await profile.Budget.RecordPaymentAsync(command, Ct);

        Assert.True(replay.IsSuccess);
        Assert.Equal(first.Value.Id, replay.Value.Id);
        Assert.Contains(replay.Warnings, w => w.Code == WarningCodes.PaymentAlreadyRecorded);
        Assert.Single((await profile.Budget.GetOverviewAsync(projectId, Ct)).Value.Items.Single().Payments);
    }

    [Fact]
    public async Task Same_date_and_amount_with_new_operation_id_is_a_legitimate_second_payment()
    {
        await using var profile = await TestProfile.CreateAsync();
        var (projectId, itemId) = await CreateItemAsync(profile);

        await profile.Budget.RecordPaymentAsync(Posted(itemId, PaymentKind.Balance, 100_00), Ct);
        var second = await profile.Budget.RecordPaymentAsync(Posted(itemId, PaymentKind.Balance, 100_00), Ct);

        Assert.True(second.IsSuccess);
        Assert.Empty(second.Warnings);
        Assert.Equal(200_00, (await profile.Budget.GetOverviewAsync(projectId, Ct)).Value.ProjectTotals.PaidNetMinor);
    }

    [Fact]
    public async Task Reusing_operation_id_for_a_different_payment_is_a_conflict()
    {
        await using var profile = await TestProfile.CreateAsync();
        var (projectId, itemId) = await CreateItemAsync(profile);
        var command = Posted(itemId, PaymentKind.Deposit, 200_00);
        await profile.Budget.RecordPaymentAsync(command, Ct);

        var reused = await profile.Budget.RecordPaymentAsync(command with { AmountMinor = 250_00 }, Ct);

        Assert.Equal(ErrorCode.Conflict, reused.Error?.Code);
        Assert.Equal(200_00, (await profile.Budget.GetOverviewAsync(projectId, Ct)).Value.ProjectTotals.PaidNetMinor);
    }

    [Fact]
    public async Task Concurrent_double_submit_creates_exactly_one_payment()
    {
        await using var profile = await TestProfile.CreateAsync();
        var (projectId, itemId) = await CreateItemAsync(profile);
        var command = Posted(itemId, PaymentKind.Deposit, 200_00);

        var results = await Task.WhenAll(
            Task.Run(() => profile.Budget.RecordPaymentAsync(command, Ct), Ct),
            Task.Run(() => profile.Budget.RecordPaymentAsync(command, Ct), Ct));

        Assert.All(results, r => Assert.True(r.IsSuccess, r.Error?.Detail));
        Assert.Equal(results[0].Value.Id, results[1].Value.Id);
        Assert.Single((await profile.Budget.GetOverviewAsync(projectId, Ct)).Value.Items.Single().Payments);
    }

    [Fact]
    public async Task Refund_cannot_exceed_posted_payments_and_planned_refund_is_rejected()
    {
        await using var profile = await TestProfile.CreateAsync();
        var (_, itemId) = await CreateItemAsync(profile);
        await profile.Budget.RecordPaymentAsync(Posted(itemId, PaymentKind.Deposit, 100_00), Ct);

        var tooMuch = await profile.Budget.RecordPaymentAsync(Posted(itemId, PaymentKind.Refund, 100_01), Ct);
        var planned = await profile.Budget.RecordPaymentAsync(
            new RecordPayment(itemId, Guid.NewGuid(), PaymentKind.Refund, 10_00, PaymentStatus.Planned, null, null, null), Ct);
        var negative = await profile.Budget.RecordPaymentAsync(Posted(itemId, PaymentKind.Balance, -10_00), Ct);

        Assert.Contains(tooMuch.Error!.Issues, i => i.Rule == ValidationRules.RefundExceedsPaid);
        Assert.Contains(planned.Error!.Issues, i => i.Rule == ValidationRules.NotAllowedForStatus);
        Assert.Contains(negative.Error!.Issues, i => i.Rule == ValidationRules.AmountOutOfRange);
    }

    [Fact]
    public async Task Overpayment_is_shown_as_overpaid_not_as_zero()
    {
        await using var profile = await TestProfile.CreateAsync();
        var (projectId, itemId) = await CreateItemAsync(profile, committedMinor: 900_00);
        await profile.Budget.RecordPaymentAsync(Posted(itemId, PaymentKind.Balance, 950_00), Ct);

        var overview = (await profile.Budget.GetOverviewAsync(projectId, Ct)).Value;

        Assert.Equal(-50_00, overview.Items.Single().BalanceMinor);
        Assert.True(overview.Items.Single().IsOverpaid);
        Assert.Equal((0L, 50_00L), (overview.ProjectTotals.RemainingMinor, overview.ProjectTotals.OverpaidMinor));
    }

    [Fact]
    public async Task Posted_payment_requires_paid_date_and_item_with_posted_payment_cannot_be_deleted()
    {
        await using var profile = await TestProfile.CreateAsync();
        var (projectId, itemId) = await CreateItemAsync(profile);

        var noDate = await profile.Budget.RecordPaymentAsync(Posted(itemId, PaymentKind.Deposit, 100_00) with { PaidLocalDate = null }, Ct);
        var paid = (await profile.Budget.RecordPaymentAsync(Posted(itemId, PaymentKind.Deposit, 100_00), Ct)).Value;
        var item = (await profile.Budget.GetOverviewAsync(projectId, Ct)).Value.Items.Single().Item;
        var delete = await profile.Budget.DeleteItemAsync(new DeleteBudgetItem(itemId, item.Revision), Ct);

        Assert.Contains(noDate.Error!.Issues, i => i.Field == nameof(PaymentDto.PaidLocalDate));
        Assert.Contains(delete.Error!.Issues, i => i.Rule == ValidationRules.HasPostedPayments);

        // After cancelling the only payment, the line can go.
        Assert.True((await profile.Budget.CancelPaymentAsync(new CancelPayment(paid.Id, paid.Revision), Ct)).IsSuccess);
        item = (await profile.Budget.GetOverviewAsync(projectId, Ct)).Value.Items.Single().Item;
        Assert.True((await profile.Budget.DeleteItemAsync(new DeleteBudgetItem(itemId, item.Revision), Ct)).IsSuccess);
    }

    [Fact]
    public async Task Cancelling_a_payment_that_backs_a_refund_is_refused()
    {
        await using var profile = await TestProfile.CreateAsync();
        var (_, itemId) = await CreateItemAsync(profile);
        var paid = (await profile.Budget.RecordPaymentAsync(Posted(itemId, PaymentKind.Deposit, 100_00), Ct)).Value;
        await profile.Budget.RecordPaymentAsync(Posted(itemId, PaymentKind.Refund, 40_00), Ct);

        var cancel = await profile.Budget.CancelPaymentAsync(new CancelPayment(paid.Id, paid.Revision), Ct);

        Assert.Contains(cancel.Error!.Issues, i => i.Rule == ValidationRules.RefundExceedsPaid);
    }

    [Fact]
    public async Task Stale_item_revision_is_a_conflict_and_archived_event_is_read_only()
    {
        await using var profile = await TestProfile.CreateAsync();
        var (projectId, itemId) = await CreateItemAsync(profile);
        var item = (await profile.Budget.GetOverviewAsync(projectId, Ct)).Value.Items.Single().Item;
        await profile.Budget.RecordPaymentAsync(Posted(itemId, PaymentKind.Deposit, 10_00), Ct); // bumps the item revision

        var stale = await profile.Budget.UpdateItemAsync(new UpdateBudgetItem(itemId, item.Revision, "Catering", "Nové", null, null), Ct);
        Assert.Equal(ErrorCode.Conflict, stale.Error?.Code);

        await ArchiveAsync(profile, projectId);
        var readOnly = await profile.Budget.RecordPaymentAsync(Posted(itemId, PaymentKind.Deposit, 10_00), Ct);
        Assert.Equal(ErrorCode.ReadOnly, readOnly.Error?.Code);
    }

    [Fact]
    public async Task Foreign_currency_item_is_created_with_warning_and_stays_out_of_project_totals()
    {
        await using var profile = await TestProfile.CreateAsync();
        var project = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;

        var created = await profile.Budget.CreateItemAsync(new CreateBudgetItem(project.Id, "Hudba", "Kapela", null, 20000_00, "czk"), Ct);
        var overview = (await profile.Budget.GetOverviewAsync(project.Id, Ct)).Value;

        Assert.Equal("CZK", created.Value.Currency);
        Assert.Contains(created.Warnings, w => w.Code == WarningCodes.ForeignCurrencyNotInTotals);
        Assert.Equal(0, overview.ProjectTotals.CommittedMinor);
        Assert.Equal(20000_00, Assert.Single(overview.OtherCurrencyTotals).CommittedMinor);
    }

    internal static async Task ArchiveAsync(TestProfile profile, Guid projectId)
    {
        foreach (var (target, confirmed) in new[] { (EventStatus.Planning, false), (EventStatus.Ready, true), (EventStatus.Completed, false), (EventStatus.Archived, false) })
        {
            var current = (await profile.Events.GetAsync(projectId, Ct)).Value;
            Assert.True((await profile.Events.ChangeStatusAsync(new ChangeEventStatus(projectId, current.Revision, target, confirmed), Ct)).IsSuccess);
        }
    }

    private static RecordPayment Posted(Guid itemId, PaymentKind kind, long amountMinor) =>
        new(itemId, Guid.NewGuid(), kind, amountMinor, PaymentStatus.Posted, PaidDay, null, null);

    private static async Task<(Guid ProjectId, Guid ItemId)> CreateItemAsync(TestProfile profile, long? committedMinor = 900_00)
    {
        var project = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;
        var item = (await profile.Budget.CreateItemAsync(new CreateBudgetItem(project.Id, "Catering", "Večera", 1000_00, committedMinor, "EUR"), Ct)).Value;
        return (project.Id, item.Id);
    }
}
