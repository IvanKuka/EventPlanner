using EventPlanner.Contracts.Vendors;
using EventPlanner.Core.Models.Vendors;

namespace EventPlanner.Core.Services.Vendors;

internal static class VendorMapping
{
    public static VendorDto ToDto(this Vendor vendor) =>
        new(vendor.Id, vendor.EventId, vendor.Category, vendor.Name, vendor.Contact, vendor.Notes, vendor.Revision);

    public static QuoteDto ToDto(this Quote quote) => new(
        quote.Id,
        quote.VendorId,
        quote.Description,
        quote.AmountMinor,
        quote.Currency,
        quote.ScopeText,
        quote.ExclusionsText,
        quote.ValidUntilDate,
        quote.Transport,
        quote.TransportMinor,
        quote.PeopleCount,
        quote.Status,
        quote.Revision,
        quote.KnownTotalMinor);

    public static QuoteDecisionDto ToDto(this QuoteDecision decision, string quoteDescription) => new(
        decision.Id,
        decision.QuoteId,
        quoteDescription,
        decision.Kind,
        decision.DecidedUtc,
        decision.AmountMinor,
        decision.KnownTotalMinor,
        decision.Currency,
        decision.BudgetImpact,
        decision.Note);
}
