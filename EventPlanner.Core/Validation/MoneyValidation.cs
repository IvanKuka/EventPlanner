using System.Text.RegularExpressions;
using EventPlanner.Contracts.Money;
using EventPlanner.Contracts.Results;

namespace EventPlanner.Core.Validation;

/// <summary>Money rules shared by features: ISO 4217 codes and integer minor-unit amounts within a safe range.</summary>
public static partial class MoneyValidation
{
    public static void Currency(string field, string? currency, List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            issues.Add(new ValidationIssue(field, ValidationRules.Required));
        }
        else if (!CurrencyCode().IsMatch(currency))
        {
            issues.Add(new ValidationIssue(field, ValidationRules.InvalidCurrency));
        }
    }

    /// <summary>Amounts are never negative; direction is expressed by the record type (e.g. refund), not the sign.</summary>
    public static void Amount(string field, long? amountMinor, List<ValidationIssue> issues, bool allowZero)
    {
        if (amountMinor is not { } amount)
        {
            return;
        }

        if (amount < 0 || amount > MinorUnits.MaxAmountMinor || (!allowZero && amount == 0))
        {
            issues.Add(new ValidationIssue(field, ValidationRules.AmountOutOfRange));
        }
    }

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CurrencyCode();
}
