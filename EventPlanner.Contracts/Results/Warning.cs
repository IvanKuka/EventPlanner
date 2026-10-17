namespace EventPlanner.Contracts.Results;

/// <summary>A non-blocking notice attached to a successful result (e.g. a duplicate guest name).</summary>
public sealed record Warning(string Code, string? Field = null);

public static class WarningCodes
{
    public const string DuplicateGuestName = "DuplicateGuestName";

    /// <summary>A replayed payment command; the existing payment was returned and nothing new was recorded.</summary>
    public const string PaymentAlreadyRecorded = "PaymentAlreadyRecorded";

    /// <summary>The committed amount was taken from a quote whose full price (e.g. transport) is unknown.</summary>
    public const string QuoteTotalIncomplete = "QuoteTotalIncomplete";

    /// <summary>A selected quote was edited; the linked budget item was deliberately left unchanged.</summary>
    public const string LinkedBudgetNotUpdated = "LinkedBudgetNotUpdated";

    /// <summary>The budget item is in another currency than the project and is not part of the project totals.</summary>
    public const string ForeignCurrencyNotInTotals = "ForeignCurrencyNotInTotals";
}
