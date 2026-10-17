using EventPlanner.Contracts.Budget;
using EventPlanner.Core.Models.Budget;
using EventPlanner.Core.Services.Budget;

namespace EventPlanner.Tests.Unit.Budget;

public sealed class BudgetCalculatorTests
{
    private static readonly Guid EventId = Guid.NewGuid();

    /// <summary>E-A04: plan 1000, commitment 900, deposit 200, balance 700, refund 50.</summary>
    [Fact]
    public void Plan_commitment_payments_and_refund_are_totalled_separately()
    {
        var item = BudgetItem.Create(Guid.NewGuid(), EventId, "Catering", "Večera", 1000_00, 900_00, "EUR");
        Pay(item, PaymentKind.Deposit, 200_00, PaymentStatus.Posted);
        Pay(item, PaymentKind.Balance, 700_00, PaymentStatus.Posted);
        Pay(item, PaymentKind.Refund, 50_00, PaymentStatus.Posted);

        var summary = BudgetCalculator.Summarize(item);
        var totals = BudgetCalculator.Overview("EUR", [item]).ProjectTotals;

        Assert.Equal(850_00, summary.PaidNetMinor);
        Assert.Equal(50_00, summary.RefundedMinor);
        Assert.Equal(50_00, summary.BalanceMinor);
        Assert.False(summary.IsOverpaid);
        Assert.Equal(1000_00, totals.EstimatedMinor);
        Assert.Equal(900_00, totals.CommittedMinor);
        Assert.Equal(850_00, totals.PaidNetMinor);
        Assert.Equal(50_00, totals.RemainingMinor);
        Assert.Equal(0, totals.OverpaidMinor);
    }

    [Fact]
    public void Planned_payment_is_scheduled_not_paid_and_not_counted_twice()
    {
        var item = BudgetItem.Create(Guid.NewGuid(), EventId, "Catering", "Večera", 1000_00, 900_00, "EUR");
        Pay(item, PaymentKind.Deposit, 200_00, PaymentStatus.Posted);
        var planned = Pay(item, PaymentKind.Balance, 700_00, PaymentStatus.Planned);
        Pay(item, PaymentKind.Balance, 300_00, PaymentStatus.Cancelled);

        var before = BudgetCalculator.Summarize(item);
        item.PostPayment(planned, new DateOnly(2027, 6, 1));
        var after = BudgetCalculator.Summarize(item);

        Assert.Equal((200_00, 700_00, 700_00), (before.PaidNetMinor, before.ScheduledMinor, before.BalanceMinor!.Value));
        Assert.Equal((900_00, 0, 0), (after.PaidNetMinor, after.ScheduledMinor, after.BalanceMinor!.Value));
    }

    [Fact]
    public void Negative_balance_is_an_explicit_overpayment_that_does_not_hide_other_debts()
    {
        var overpaid = BudgetItem.Create(Guid.NewGuid(), EventId, "Hudba", "DJ", null, 500_00, "EUR");
        Pay(overpaid, PaymentKind.Balance, 600_00, PaymentStatus.Posted);
        var open = BudgetItem.Create(Guid.NewGuid(), EventId, "Foto", "Fotograf", null, 400_00, "EUR");

        var overview = BudgetCalculator.Overview("EUR", [overpaid, open]);

        var dj = overview.Items.Single(i => i.Item.Title == "DJ");
        Assert.True(dj.IsOverpaid);
        Assert.Equal(-100_00, dj.BalanceMinor);
        Assert.Equal(400_00, overview.ProjectTotals.RemainingMinor);
        Assert.Equal(100_00, overview.ProjectTotals.OverpaidMinor);
        Assert.Equal(2, overview.ProjectTotals.ItemsWithoutEstimate);
    }

    [Fact]
    public void Unknown_commitment_is_reported_and_never_treated_as_zero_balance()
    {
        var item = BudgetItem.Create(Guid.NewGuid(), EventId, "Výzdoba", "Kvety", 300_00, null, "EUR");

        var summary = BudgetCalculator.Summarize(item);
        var totals = BudgetCalculator.Overview("EUR", [item]).ProjectTotals;

        Assert.Null(summary.BalanceMinor);
        Assert.False(summary.IsOverpaid);
        Assert.Equal(1, totals.ItemsWithoutCommitment);
        Assert.Equal(0, totals.RemainingMinor);
    }

    [Fact]
    public void Foreign_currency_item_is_kept_out_of_project_totals()
    {
        var eur = BudgetItem.Create(Guid.NewGuid(), EventId, "Výzdoba", "Kvety", null, 300_00, "EUR");
        var czk = BudgetItem.Create(Guid.NewGuid(), EventId, "Výzdoba", "Balóny", null, 5000_00, "CZK");

        var overview = BudgetCalculator.Overview("EUR", [eur, czk]);

        Assert.Equal(300_00, overview.ProjectTotals.CommittedMinor);
        Assert.Equal(1, overview.ProjectTotals.ItemCount);
        var other = Assert.Single(overview.OtherCurrencyTotals);
        Assert.Equal(("CZK", 5000_00L), (other.Currency, other.CommittedMinor));
    }

    private static Payment Pay(BudgetItem item, PaymentKind kind, long amountMinor, PaymentStatus status)
    {
        var payment = item.AddPayment(
            Guid.NewGuid(), Guid.NewGuid(), kind, amountMinor, status == PaymentStatus.Cancelled ? PaymentStatus.Planned : status,
            status == PaymentStatus.Posted ? new DateOnly(2027, 1, 10) : null, null, null);
        if (status == PaymentStatus.Cancelled)
        {
            item.CancelPayment(payment);
        }

        return payment;
    }
}
