namespace EventPlanner.Contracts.Results;

/// <summary>
/// A field-level problem. <paramref name="Rule"/> is a stable key from <see cref="ValidationRules"/>
/// that the UI maps to localized text next to the field.
/// </summary>
public sealed record ValidationIssue(string Field, string Rule);

public static class ValidationRules
{
    public const string Required = "Required";
    public const string TooLong = "TooLong";
    public const string InvalidCharacters = "InvalidCharacters";
    public const string InvalidTimeZone = "InvalidTimeZone";
    public const string InvalidCurrency = "InvalidCurrency";
    public const string EndBeforeStart = "EndBeforeStart";
    public const string InvalidTransition = "InvalidTransition";
    public const string ConfirmationRequired = "ConfirmationRequired";
    public const string ConfirmationMismatch = "ConfirmationMismatch";
    public const string UnknownReference = "UnknownReference";
    public const string NotInGroup = "NotInGroup";
    public const string InvalidValue = "InvalidValue";
    public const string AmountOutOfRange = "AmountOutOfRange";
    public const string CurrencyMismatch = "CurrencyMismatch";
    public const string RefundExceedsPaid = "RefundExceedsPaid";
    public const string NotAllowedForStatus = "NotAllowedForStatus";
    public const string HasPostedPayments = "HasPostedPayments";
}
