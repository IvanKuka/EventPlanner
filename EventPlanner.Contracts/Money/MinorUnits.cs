namespace EventPlanner.Contracts.Money;

/// <summary>
/// Money is always stored as an integer count of the currency's minor unit (cents for EUR).
/// The exponent follows ISO 4217; currencies not listed here use two decimals.
/// </summary>
public static class MinorUnits
{
    /// <summary>Upper bound for any single amount (10 billion major units with 2 decimals); keeps sums far from overflow.</summary>
    public const long MaxAmountMinor = 1_000_000_000_000;

    private static readonly HashSet<string> ZeroDecimalCurrencies =
        new(StringComparer.Ordinal) { "BIF", "CLP", "DJF", "GNF", "ISK", "JPY", "KMF", "KRW", "PYG", "RWF", "UGX", "VND", "VUV", "XAF", "XOF", "XPF" };

    private static readonly HashSet<string> ThreeDecimalCurrencies =
        new(StringComparer.Ordinal) { "BHD", "IQD", "JOD", "KWD", "LYD", "OMR", "TND" };

    public static int DecimalDigits(string currency) =>
        ZeroDecimalCurrencies.Contains(currency) ? 0
        : ThreeDecimalCurrencies.Contains(currency) ? 3
        : 2;
}
