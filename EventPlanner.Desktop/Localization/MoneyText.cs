using System.Globalization;
using EventPlanner.Contracts.Money;

namespace EventPlanner.Desktop.Localization;

/// <summary>
/// Converts between typed amounts ("1 250,50") and minor units. Parsing is exact: more decimals
/// than the currency has are rejected instead of being rounded. Empty input means "unknown" (null),
/// never zero.
/// </summary>
public static class MoneyText
{
    public static string Format(long amountMinor, string currency) =>
        $"{FormatNumber(amountMinor, currency)} {currency}";

    public static string Format(long? amountMinor, string currency) =>
        amountMinor is { } amount ? Format(amount, currency) : Strings.Money_Unknown;

    /// <summary>Number only, for editing in a text box.</summary>
    public static string FormatNumber(long amountMinor, string currency)
    {
        var digits = MinorUnits.DecimalDigits(currency);
        var major = amountMinor / Pow10(digits);
        return major.ToString($"N{digits}", CultureInfo.CurrentCulture);
    }

    public static string? FormatNumber(long? amountMinor, string currency) =>
        amountMinor is { } amount ? FormatNumber(amount, currency) : null;

    /// <summary>Returns false for text that is not a non-negative amount with at most the currency's decimals.</summary>
    public static bool TryParse(string? text, string currency, out long? amountMinor)
    {
        amountMinor = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        // Accept both the culture's separators and a plain dot, and ignore spaces used as thousands separators.
        var normalized = text.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);
        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.CurrentCulture, out var value)
            && !decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }

        var scaled = value * Pow10(MinorUnits.DecimalDigits(currency));
        if (value < 0 || scaled != decimal.Truncate(scaled) || scaled > MinorUnits.MaxAmountMinor)
        {
            return false;
        }

        amountMinor = (long)scaled;
        return true;
    }

    private static decimal Pow10(int digits) => digits switch
    {
        0 => 1m,
        3 => 1000m,
        _ => 100m,
    };
}
