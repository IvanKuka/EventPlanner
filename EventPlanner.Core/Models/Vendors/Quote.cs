using EventPlanner.Contracts.Vendors;

namespace EventPlanner.Core.Models.Vendors;

/// <summary>
/// One offer of a vendor, kept in its own currency. Unknown parts (transport, people count,
/// exclusions) stay null/Unknown and are never interpreted as zero or as included.
/// </summary>
public sealed class Quote
{
    private Quote()
    {
    }

    public Guid Id { get; private set; }

    public Guid VendorId { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public long AmountMinor { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public string ScopeText { get; private set; } = string.Empty;

    public string? ExclusionsText { get; private set; }

    public DateOnly? ValidUntilDate { get; private set; }

    public TransportCoverage Transport { get; private set; }

    public long? TransportMinor { get; private set; }

    public int? PeopleCount { get; private set; }

    public QuoteStatus Status { get; private set; }

    public long Revision { get; private set; }

    /// <summary>Full price when every part is known; null otherwise (see <see cref="QuoteDto.KnownTotalMinor"/>).</summary>
    public long? KnownTotalMinor => Transport switch
    {
        TransportCoverage.Included or TransportCoverage.NotNeeded => AmountMinor,
        TransportCoverage.Separate when TransportMinor is { } transport => AmountMinor + transport,
        _ => null,
    };

    public static Quote Create(Guid id, Guid vendorId, QuoteInput input)
    {
        var quote = new Quote { Id = id, VendorId = vendorId, Status = QuoteStatus.Received, Revision = 1 };
        quote.Apply(input);
        return quote;
    }

    public void Update(QuoteInput input)
    {
        Apply(input);
        Revision++;
    }

    public void Select() => SetStatus(QuoteStatus.Selected);

    /// <summary>Another quote of the same category was selected; this one is open again.</summary>
    /// <returns>True when the quote was selected until now.</returns>
    public bool Unselect()
    {
        if (Status != QuoteStatus.Selected)
        {
            return false;
        }

        SetStatus(QuoteStatus.Received);
        return true;
    }

    public void Decline() => SetStatus(QuoteStatus.Declined);

    private void SetStatus(QuoteStatus status)
    {
        if (Status == status)
        {
            return;
        }

        Status = status;
        Revision++;
    }

    private void Apply(QuoteInput input)
    {
        Description = input.Description.Trim();
        AmountMinor = input.AmountMinor;
        Currency = input.Currency;
        ScopeText = input.ScopeText;
        ExclusionsText = input.ExclusionsText;
        ValidUntilDate = input.ValidUntilDate;
        Transport = input.Transport;

        // A transport price only has a meaning when transport is charged separately.
        TransportMinor = input.Transport == TransportCoverage.Separate ? input.TransportMinor : null;
        PeopleCount = input.PeopleCount;
    }
}
