using EventPlanner.Contracts.Vendors;
using EventPlanner.Core.Models.Vendors;

namespace EventPlanner.Core.Services.Vendors;

/// <summary>
/// Builds the comparison table of one category. Missing information is listed per quote instead of
/// being read as zero or "included"; the lowest price is marked only among complete quotes of the
/// same currency, so there is never a ranking or total across currencies.
/// </summary>
public static class QuoteComparison
{
    public static StringComparer CategoryComparer => StringComparer.CurrentCultureIgnoreCase;

    public static QuoteComparisonDto Build(string category, string projectCurrency, IReadOnlyCollection<Vendor> vendors, IEnumerable<Quote> quotes)
    {
        var vendorsInCategory = vendors
            .Where(v => CategoryComparer.Equals(v.Category, category.Trim()))
            .ToDictionary(v => v.Id);
        var candidates = quotes.Where(q => vendorsInCategory.ContainsKey(q.VendorId)).ToList();

        var lowestByCurrency = candidates
            .Where(q => q.Status != QuoteStatus.Declined && q.KnownTotalMinor is not null)
            .GroupBy(q => q.Currency, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Min(q => q.KnownTotalMinor!.Value), StringComparer.Ordinal);

        var rows = candidates
            .OrderBy(q => string.Equals(q.Currency, projectCurrency, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(q => q.Currency, StringComparer.Ordinal)
            .ThenBy(q => q.KnownTotalMinor ?? long.MaxValue)
            .ThenBy(q => vendorsInCategory[q.VendorId].Name, StringComparer.CurrentCulture)
            .Select(q => new QuoteComparisonRowDto(
                q.ToDto(),
                vendorsInCategory[q.VendorId].Name,
                string.Equals(q.Currency, projectCurrency, StringComparison.Ordinal),
                q.Status != QuoteStatus.Declined
                    && q.KnownTotalMinor is { } total
                    && lowestByCurrency.TryGetValue(q.Currency, out var lowest)
                    && total == lowest,
                MissingFields(q)))
            .ToList();

        return new QuoteComparisonDto(category.Trim(), projectCurrency, rows);
    }

    public static IReadOnlyList<string> MissingFields(Quote quote)
    {
        var missing = new List<string>();
        if (quote.Transport == TransportCoverage.Unknown)
        {
            missing.Add(QuoteFields.Transport);
        }
        else if (quote.Transport == TransportCoverage.Separate && quote.TransportMinor is null)
        {
            missing.Add(QuoteFields.TransportPrice);
        }

        if (quote.PeopleCount is null)
        {
            missing.Add(QuoteFields.PeopleCount);
        }

        if (quote.ExclusionsText is null)
        {
            missing.Add(QuoteFields.Exclusions);
        }

        if (quote.ValidUntilDate is null)
        {
            missing.Add(QuoteFields.ValidUntil);
        }

        return missing;
    }
}
