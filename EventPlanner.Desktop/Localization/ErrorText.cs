using EventPlanner.Contracts.Results;
using EventPlanner.Desktop.ViewModels;

namespace EventPlanner.Desktop.Localization;

/// <summary>Maps stable error codes and validation rules to Slovak UI texts (never shows technical details).</summary>
public static class ErrorText
{
    public static string For(OperationError error) => error.Code switch
    {
        ErrorCode.ValidationFailed => Strings.Error_ValidationFailed,
        ErrorCode.Conflict => Strings.Error_Conflict,
        ErrorCode.NotFound => Strings.Error_NotFound,
        ErrorCode.ReadOnly => Strings.Error_ReadOnly,
        ErrorCode.MissingSource => Strings.Error_MissingSource,
        ErrorCode.UnsupportedInput => Strings.Error_UnsupportedInput,
        ErrorCode.PermissionDenied => Strings.Error_PermissionDenied,
        ErrorCode.InsufficientSpace => Strings.Error_InsufficientSpace,
        ErrorCode.CorruptData => Strings.Error_CorruptData,
        ErrorCode.UnsupportedVersion => Strings.Error_UnsupportedVersion,
        ErrorCode.Cancelled => Strings.Error_Cancelled,
        _ => Strings.Error_Unexpected,
    } + $" ({error.Code})";

    public static string For(ValidationIssue issue) => issue.Rule switch
    {
        ValidationRules.Required => Strings.Validation_Required,
        ValidationRules.TooLong => Strings.Validation_TooLong,
        ValidationRules.InvalidCharacters => Strings.Validation_InvalidCharacters,
        ValidationRules.InvalidTimeZone => Strings.Validation_InvalidTimeZone,
        ValidationRules.InvalidCurrency => Strings.Validation_InvalidCurrency,
        ValidationRules.EndBeforeStart => Strings.Validation_EndBeforeStart,
        ValidationRules.InvalidTransition => Strings.Validation_InvalidTransition,
        ValidationRules.ConfirmationRequired => Strings.Validation_ConfirmationRequired,
        ValidationRules.ConfirmationMismatch => Strings.Validation_ConfirmationMismatch,
        ValidationRules.UnknownReference => Strings.Validation_UnknownReference,
        ValidationRules.NotInGroup => Strings.Validation_NotInGroup,
        ValidationRules.AmountOutOfRange => Strings.Validation_AmountOutOfRange,
        ValidationRules.CurrencyMismatch => Strings.Validation_CurrencyMismatch,
        ValidationRules.RefundExceedsPaid => Strings.Validation_RefundExceedsPaid,
        ValidationRules.NotAllowedForStatus => Strings.Validation_NotAllowedForStatus,
        ValidationRules.HasPostedPayments => Strings.Validation_HasPostedPayments,
        _ => Strings.Validation_InvalidValue,
    };

    public static FieldErrors FieldsOf(OperationError? error) =>
        error is null
            ? FieldErrors.None
            : new FieldErrors(error.Issues.Select(i => KeyValuePair.Create(i.Field, For(i))));

    public static string For(Warning warning) => warning.Code switch
    {
        WarningCodes.DuplicateGuestName => Strings.Warning_DuplicateGuestName,
        WarningCodes.PaymentAlreadyRecorded => Strings.Warning_PaymentAlreadyRecorded,
        WarningCodes.QuoteTotalIncomplete => Strings.Warning_QuoteTotalIncomplete,
        WarningCodes.LinkedBudgetNotUpdated => Strings.Warning_LinkedBudgetNotUpdated,
        WarningCodes.ForeignCurrencyNotInTotals => Strings.Warning_ForeignCurrencyNotInTotals,
        _ => warning.Code,
    };
}
