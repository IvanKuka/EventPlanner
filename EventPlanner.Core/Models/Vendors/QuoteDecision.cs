using EventPlanner.Contracts.Vendors;

namespace EventPlanner.Core.Models.Vendors;

/// <summary>
/// Append-only history entry of an organizer decision about a quote. The price is copied at decision
/// time, so editing the quote later does not change what the organizer decided on.
/// </summary>
public sealed class QuoteDecision
{
    private QuoteDecision()
    {
    }

    public Guid Id { get; private set; }

    public Guid QuoteId { get; private set; }

    public QuoteDecisionKind Kind { get; private set; }

    public DateTimeOffset DecidedUtc { get; private set; }

    public long AmountMinor { get; private set; }

    public long? KnownTotalMinor { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public BudgetImpactPolicy? BudgetImpact { get; private set; }

    public string? Note { get; private set; }

    public static QuoteDecision Record(Quote quote, QuoteDecisionKind kind, DateTimeOffset decidedUtc, BudgetImpactPolicy? budgetImpact, string? note) => new()
    {
        Id = Guid.CreateVersion7(),
        QuoteId = quote.Id,
        Kind = kind,
        DecidedUtc = decidedUtc,
        AmountMinor = quote.AmountMinor,
        KnownTotalMinor = quote.KnownTotalMinor,
        Currency = quote.Currency,
        BudgetImpact = kind == QuoteDecisionKind.Selected ? budgetImpact : null,
        Note = note,
    };
}
