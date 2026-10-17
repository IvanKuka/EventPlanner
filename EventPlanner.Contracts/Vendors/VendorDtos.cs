namespace EventPlanner.Contracts.Vendors;

public sealed record VendorDto(Guid Id, Guid EventId, string Category, string Name, string? Contact, string? Notes, long Revision);

/// <param name="KnownTotalMinor">
/// Amount plus transport when the full price is known; null when transport is unknown or its
/// price is missing, so an incomplete quote never looks cheaper by a pretended zero.
/// </param>
public sealed record QuoteDto(
    Guid Id,
    Guid VendorId,
    string Description,
    long AmountMinor,
    string Currency,
    string ScopeText,
    string? ExclusionsText,
    DateOnly? ValidUntilDate,
    TransportCoverage Transport,
    long? TransportMinor,
    int? PeopleCount,
    QuoteStatus Status,
    long Revision,
    long? KnownTotalMinor);

/// <param name="IsProjectCurrency">False for a quote in another currency; it is compared only with quotes in the same currency.</param>
/// <param name="IsLowestKnownInCurrency">Lowest known total among quotes in the same currency; never set across currencies.</param>
public sealed record QuoteComparisonRowDto(
    QuoteDto Quote,
    string VendorName,
    bool IsProjectCurrency,
    bool IsLowestKnownInCurrency,
    IReadOnlyList<string> MissingFields);

/// <summary>Quotes of one category. Totals are never summed or ranked across currencies.</summary>
public sealed record QuoteComparisonDto(string Category, string ProjectCurrency, IReadOnlyList<QuoteComparisonRowDto> Rows)
{
    public IReadOnlyList<string> Currencies => [.. Rows.Select(r => r.Quote.Currency).Distinct(StringComparer.Ordinal)];
}

/// <summary>Stable names for <see cref="QuoteComparisonRowDto.MissingFields"/>.</summary>
public static class QuoteFields
{
    public const string Transport = nameof(QuoteDto.Transport);
    public const string TransportPrice = nameof(QuoteDto.TransportMinor);
    public const string PeopleCount = nameof(QuoteDto.PeopleCount);
    public const string Exclusions = nameof(QuoteDto.ExclusionsText);
    public const string ValidUntil = nameof(QuoteDto.ValidUntilDate);
}

/// <param name="BudgetItemId">The budget item created or updated by the selection; null when the budget was left unchanged.</param>
public sealed record QuoteSelectionDto(QuoteDto Quote, Guid? BudgetItemId);

/// <summary>
/// One organizer decision about a quote. Amounts are a snapshot from the moment of the decision,
/// so later edits of the quote do not rewrite history.
/// </summary>
/// <param name="KnownTotalMinor">Full price at decision time; null when it was incomplete (see <see cref="QuoteDto.KnownTotalMinor"/>).</param>
/// <param name="BudgetImpact">Only for <see cref="QuoteDecisionKind.Selected"/>.</param>
public sealed record QuoteDecisionDto(
    Guid Id,
    Guid QuoteId,
    string QuoteDescription,
    QuoteDecisionKind Kind,
    DateTimeOffset DecidedUtc,
    long AmountMinor,
    long? KnownTotalMinor,
    string Currency,
    BudgetImpactPolicy? BudgetImpact,
    string? Note);
