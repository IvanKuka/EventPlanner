using EventPlanner.Contracts.Budget;
using EventPlanner.Core.Models.Budget;

namespace EventPlanner.Core.Services.Budget;

/// <summary>
/// Budget figures computed from source records only (03-processing.md, Rozpočet a ponuky):
/// paid = posted payments − posted refunds; planned payments are scheduled, never paid;
/// balance = committed − paid, negative balance is an explicit overpayment.
/// </summary>
public static class BudgetCalculator
{
    public static BudgetItemSummaryDto Summarize(BudgetItem item)
    {
        var refunded = item.PostedRefundsMinor;
        var paidNet = item.PostedPaymentsMinor - refunded;
        var scheduled = item.Payments
            .Where(p => p.Status == PaymentStatus.Planned && p.Kind != PaymentKind.Refund)
            .Sum(p => p.AmountMinor);
        long? balance = item.CommittedMinor is { } committed ? committed - paidNet : null;

        return new BudgetItemSummaryDto(
            item.ToDto(),
            paidNet,
            refunded,
            scheduled,
            balance,
            [.. item.Payments
                .OrderBy(p => p.PaidLocalDate ?? p.DueLocalDate ?? DateOnly.MaxValue)
                .ThenBy(p => p.Id)
                .Select(p => p.ToDto())]);
    }

    public static BudgetOverviewDto Overview(string projectCurrency, IEnumerable<BudgetItem> items)
    {
        var summaries = items
            .OrderBy(i => i.Category, StringComparer.CurrentCulture)
            .ThenBy(i => i.Title, StringComparer.CurrentCulture)
            .Select(Summarize)
            .ToList();

        var byCurrency = summaries.ToLookup(s => s.Item.Currency, StringComparer.Ordinal);
        var projectTotals = Totals(projectCurrency, byCurrency[projectCurrency]);

        // Never converted: each foreign currency gets its own totals, outside the project totals.
        var others = byCurrency
            .Where(g => !string.Equals(g.Key, projectCurrency, StringComparison.Ordinal))
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => Totals(g.Key, g))
            .ToList();

        return new BudgetOverviewDto(projectCurrency, summaries, projectTotals, others);
    }

    public static BudgetTotalsDto Totals(string currency, IEnumerable<BudgetItemSummaryDto> summaries)
    {
        var list = summaries.ToList();
        return new BudgetTotalsDto(
            currency,
            list.Count,
            list.Sum(s => s.Item.EstimatedMinor ?? 0),
            list.Count(s => s.Item.EstimatedMinor is null),
            list.Sum(s => s.Item.CommittedMinor ?? 0),
            list.Count(s => s.Item.CommittedMinor is null),
            list.Sum(s => s.PaidNetMinor),
            list.Sum(s => s.RefundedMinor),
            list.Sum(s => s.ScheduledMinor),
            list.Sum(s => s.BalanceMinor is > 0 ? s.BalanceMinor.Value : 0),
            list.Sum(s => s.BalanceMinor is < 0 ? -s.BalanceMinor.Value : 0));
    }
}
