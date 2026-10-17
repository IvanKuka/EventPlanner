using System.Globalization;
using EventPlanner.Contracts.Vendors;
using EventPlanner.Desktop.Localization;

namespace EventPlanner.Desktop.ViewModels.Vendors;

/// <summary>One entry of the vendor's decision history; the price is the snapshot from the decision time.</summary>
public sealed record QuoteDecisionRow(QuoteDecisionDto Decision)
{
    public string HeaderText => string.Join(
        " · ",
        Decision.DecidedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture),
        EnumText.For(Decision.Kind),
        Decision.QuoteDescription);

    public string PriceText => string.Format(
        CultureInfo.CurrentCulture,
        Strings.Vendors_DecisionPrice,
        Decision.KnownTotalMinor is { } total ? MoneyText.Format(total, Decision.Currency) : Strings.Vendors_TotalIncomplete);

    public string? BudgetImpactText => Decision.BudgetImpact is { } impact ? EnumText.For(impact) : null;

    public string? Note => Decision.Note;
}
