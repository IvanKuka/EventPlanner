using EventPlanner.Core.Models.Vendors;
using EventPlanner.Core.Ports;
using Microsoft.EntityFrameworkCore;

namespace EventPlanner.Infrastructure.Data;

internal sealed class VendorStore(EventPlannerDbContext db) : IVendorStore
{
    public async Task<IReadOnlyList<Vendor>> ListVendorsAsync(Guid eventId, CancellationToken cancellationToken) =>
        await db.Vendors.AsNoTracking().Where(v => v.EventId == eventId).ToListAsync(cancellationToken);

    public Task<Vendor?> FindVendorAsync(Guid vendorId, CancellationToken cancellationToken) =>
        db.Vendors.FirstOrDefaultAsync(v => v.Id == vendorId, cancellationToken);

    public async Task<IReadOnlyList<Quote>> ListQuotesAsync(Guid eventId, CancellationToken cancellationToken) =>
        await db.Quotes.AsNoTracking()
            .Where(q => db.Vendors.Any(v => v.Id == q.VendorId && v.EventId == eventId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Quote>> ListQuotesOfVendorsAsync(IReadOnlyCollection<Guid> vendorIds, CancellationToken cancellationToken) =>
        await db.Quotes.Where(q => vendorIds.Contains(q.VendorId)).ToListAsync(cancellationToken);

    public Task<Quote?> FindQuoteAsync(Guid quoteId, CancellationToken cancellationToken) =>
        db.Quotes.FirstOrDefaultAsync(q => q.Id == quoteId, cancellationToken);

    public void AddVendor(Vendor vendor) => db.Vendors.Add(vendor);

    public void RemoveVendor(Vendor vendor) => db.Vendors.Remove(vendor);

    public void AddQuote(Quote quote) => db.Quotes.Add(quote);

    public void RemoveQuote(Quote quote) => db.Quotes.Remove(quote);

    public async Task<IReadOnlyList<QuoteDecision>> ListDecisionsOfVendorAsync(Guid vendorId, CancellationToken cancellationToken) =>
        await db.QuoteDecisions.AsNoTracking()
            .Where(d => db.Quotes.Any(q => q.Id == d.QuoteId && q.VendorId == vendorId))
            .ToListAsync(cancellationToken);

    public void AddDecision(QuoteDecision decision) => db.QuoteDecisions.Add(decision);
}
