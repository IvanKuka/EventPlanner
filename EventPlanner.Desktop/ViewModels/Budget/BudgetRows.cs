using System.Globalization;
using EventPlanner.Contracts.Budget;
using EventPlanner.Desktop.Localization;

namespace EventPlanner.Desktop.ViewModels.Budget;

/// <summary>One budget line: estimated, committed and paid stay separate; a negative balance reads as overpaid.</summary>
public sealed record BudgetItemRow(BudgetItemSummaryDto Summary, bool IsProjectCurrency)
{
    public BudgetItemDto Item => Summary.Item;

    public string Title => Item.Title;

    public string Category => Item.Category;

    public string EstimatedText => MoneyText.Format(Item.EstimatedMinor, Item.Currency);

    public string CommittedText => MoneyText.Format(Item.CommittedMinor, Item.Currency);

    public string PaidText => MoneyText.Format(Summary.PaidNetMinor, Item.Currency);

    public string? ScheduledText => Summary.ScheduledMinor > 0
        ? string.Format(CultureInfo.CurrentCulture, Strings.Budget_ScheduledShort, MoneyText.Format(Summary.ScheduledMinor, Item.Currency))
        : null;

    public string BalanceText => BudgetText.Balance(Summary.BalanceMinor, Item.Currency);

    public bool IsOverpaid => Summary.IsOverpaid;

    public bool HasQuote => Item.SelectedQuoteId is not null;

    public string? ForeignCurrencyText => IsProjectCurrency
        ? null
        : string.Format(CultureInfo.CurrentCulture, Strings.Budget_ForeignItem, Item.Currency);
}

public sealed record PaymentRow(PaymentDto Payment, string Currency)
{
    public string KindText => EnumText.For(Payment.Kind);

    public string StatusText => EnumText.For(Payment.Status);

    /// <summary>A refund is shown with a minus sign for reading only; it is stored as a positive amount of kind Refund.</summary>
    public string AmountText => (Payment.Kind == PaymentKind.Refund ? "− " : string.Empty) + MoneyText.Format(Payment.AmountMinor, Currency);

    public string DateText => Payment.Status == PaymentStatus.Posted
        ? string.Format(CultureInfo.CurrentCulture, Strings.Budget_PaidOn, Payment.PaidLocalDate)
        : Payment.DueLocalDate is { } due
            ? string.Format(CultureInfo.CurrentCulture, Strings.Budget_DueOn, due)
            : Strings.Budget_NoDueDate;

    public string? Reference => Payment.Reference;

    public bool IsCancelled => Payment.Status == PaymentStatus.Cancelled;
}

/// <summary>Totals of one currency as display texts. Missing estimates or commitments are reported, not hidden.</summary>
public sealed record BudgetTotalsView(BudgetTotalsDto Totals)
{
    public string Currency => Totals.Currency;

    public string EstimatedText => MoneyText.Format(Totals.EstimatedMinor, Totals.Currency);

    public string CommittedText => MoneyText.Format(Totals.CommittedMinor, Totals.Currency);

    public string PaidText => MoneyText.Format(Totals.PaidNetMinor, Totals.Currency);

    public string RefundedText => MoneyText.Format(Totals.RefundedMinor, Totals.Currency);

    public string ScheduledText => MoneyText.Format(Totals.ScheduledMinor, Totals.Currency);

    public string RemainingText => MoneyText.Format(Totals.RemainingMinor, Totals.Currency);

    public string OverpaidText => MoneyText.Format(Totals.OverpaidMinor, Totals.Currency);

    public bool HasOverpaid => Totals.OverpaidMinor > 0;

    public string? MissingText
    {
        get
        {
            var parts = new List<string>();
            if (Totals.ItemsWithoutEstimate > 0)
            {
                parts.Add(string.Format(CultureInfo.CurrentCulture, Strings.Budget_ItemsWithoutEstimate, Totals.ItemsWithoutEstimate));
            }

            if (Totals.ItemsWithoutCommitment > 0)
            {
                parts.Add(string.Format(CultureInfo.CurrentCulture, Strings.Budget_ItemsWithoutCommitment, Totals.ItemsWithoutCommitment));
            }

            return parts.Count > 0 ? string.Join(" ", parts) : null;
        }
    }
}

internal static class BudgetText
{
    public static string Balance(long? balanceMinor, string currency) => balanceMinor switch
    {
        null => Strings.Budget_BalanceUnknown,
        < 0 => string.Format(CultureInfo.CurrentCulture, Strings.Budget_Overpaid, MoneyText.Format(-balanceMinor.Value, currency)),
        { } balance => string.Format(CultureInfo.CurrentCulture, Strings.Budget_Remaining, MoneyText.Format(balance, currency)),
    };
}
