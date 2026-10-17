using System.Globalization;
using EventPlanner.Contracts.Vendors;
using EventPlanner.Desktop.Localization;

namespace EventPlanner.Desktop.ViewModels.Vendors;

/// <summary>
/// One line of the quote comparison. Every text shows an unknown value as "neuvedené", never as 0;
/// foreign-currency quotes are flagged so they are not read against the project currency.
/// </summary>
public sealed record QuoteRow(QuoteComparisonRowDto Row, string ProjectCurrency)
{
    public QuoteDto Quote => Row.Quote;

    public string VendorName => Row.VendorName;

    public string Description => Quote.Description;

    public string AmountText => MoneyText.Format(Quote.AmountMinor, Quote.Currency);

    public string TransportText => Quote.Transport switch
    {
        TransportCoverage.Included => Strings.Transport_Included,
        TransportCoverage.NotNeeded => Strings.Transport_NotNeeded,
        TransportCoverage.Separate => string.Format(
            CultureInfo.CurrentCulture, Strings.Vendors_TransportSeparate, MoneyText.Format(Quote.TransportMinor, Quote.Currency)),
        _ => Strings.Transport_Unknown,
    };

    /// <summary>The full price, or an explicit "incomplete" marker when part of it is unknown.</summary>
    public string TotalText => Quote.KnownTotalMinor is { } total
        ? MoneyText.Format(total, Quote.Currency)
        : Strings.Vendors_TotalIncomplete;

    public string PeopleText => Quote.PeopleCount is { } people
        ? string.Format(CultureInfo.CurrentCulture, Strings.Vendors_PeopleCount, people)
        : string.Format(CultureInfo.CurrentCulture, Strings.Vendors_PeopleCount, Strings.Money_Unknown);

    public string ScopeText => Quote.ScopeText;

    public string ExclusionsText => Quote.ExclusionsText ?? Strings.Vendors_ExclusionsNotStated;

    public string ValidUntilText => Quote.ValidUntilDate is { } date
        ? string.Format(CultureInfo.CurrentCulture, Strings.Vendors_ValidUntil, date)
        : string.Format(CultureInfo.CurrentCulture, Strings.Vendors_ValidUntil, Strings.Money_Unknown);

    public string StatusText => EnumText.For(Quote.Status);

    public bool IsSelected => Quote.Status == QuoteStatus.Selected;

    public bool IsLowest => Row.IsLowestKnownInCurrency;

    public bool IsForeignCurrency => !Row.IsProjectCurrency;

    public string ForeignCurrencyText =>
        string.Format(CultureInfo.CurrentCulture, Strings.Vendors_ForeignCurrency, Quote.Currency, ProjectCurrency);

    public string? MissingText => Row.MissingFields.Count == 0
        ? null
        : string.Format(CultureInfo.CurrentCulture, Strings.Vendors_Missing, string.Join(", ", Row.MissingFields.Select(EnumText.ForQuoteField)));
}
