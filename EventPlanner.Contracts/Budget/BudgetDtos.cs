namespace EventPlanner.Contracts.Budget;

public sealed record PaymentDto(
    Guid Id,
    Guid BudgetItemId,
    Guid OperationId,
    PaymentKind Kind,
    long AmountMinor,
    PaymentStatus Status,
    DateOnly? PaidLocalDate,
    DateOnly? DueLocalDate,
    string? Reference,
    long Revision);

public sealed record BudgetItemDto(
    Guid Id,
    Guid EventId,
    string Category,
    string Title,
    long? EstimatedMinor,
    long? CommittedMinor,
    string Currency,
    Guid? SelectedQuoteId,
    long Revision);

/// <param name="PaidNetMinor">Posted payments minus posted refunds.</param>
/// <param name="ScheduledMinor">Planned (not yet posted) payments; never added to <paramref name="PaidNetMinor"/>.</param>
/// <param name="BalanceMinor">Committed minus net paid; null when nothing is committed. Negative means overpaid.</param>
public sealed record BudgetItemSummaryDto(
    BudgetItemDto Item,
    long PaidNetMinor,
    long RefundedMinor,
    long ScheduledMinor,
    long? BalanceMinor,
    IReadOnlyList<PaymentDto> Payments)
{
    public bool IsOverpaid => BalanceMinor < 0;
}

/// <summary>
/// Totals for one currency. Estimated, committed and paid are kept apart. Remaining and overpaid
/// are summed per item, so an overpayment on one item never hides what is still due on another.
/// </summary>
/// <param name="ItemsWithoutEstimate">Items whose estimate is unknown; their absence is reported, not counted as zero.</param>
public sealed record BudgetTotalsDto(
    string Currency,
    int ItemCount,
    long EstimatedMinor,
    int ItemsWithoutEstimate,
    long CommittedMinor,
    int ItemsWithoutCommitment,
    long PaidNetMinor,
    long RefundedMinor,
    long ScheduledMinor,
    long RemainingMinor,
    long OverpaidMinor);

/// <param name="ProjectTotals">Totals in the project currency.</param>
/// <param name="OtherCurrencyTotals">Items in other currencies, never converted or added to <paramref name="ProjectTotals"/>.</param>
public sealed record BudgetOverviewDto(
    string ProjectCurrency,
    IReadOnlyList<BudgetItemSummaryDto> Items,
    BudgetTotalsDto ProjectTotals,
    IReadOnlyList<BudgetTotalsDto> OtherCurrencyTotals);
