using EventPlanner.Contracts.Vendors;
using EventPlanner.Core.Models.Vendors;
using EventPlanner.Core.Services.Vendors;
using EventPlanner.Tests.Fixtures;

namespace EventPlanner.Tests.Unit.Vendors;

public sealed class QuoteComparisonTests
{
    private static readonly Guid EventId = Guid.NewGuid();

    /// <summary>E-A05: quotes in two currencies and a missing transport price.</summary>
    [Fact]
    public void Two_currencies_and_missing_transport_give_no_shared_total_and_no_pretended_zero()
    {
        var florist = Vendor.Create(Guid.NewGuid(), EventId, "Výzdoba", "Kvetinárstvo", null, null);
        var studio = Vendor.Create(Guid.NewGuid(), EventId, "výzdoba", "Dekor studio", null, null);
        var prague = Vendor.Create(Guid.NewGuid(), EventId, "Výzdoba", "Praha dekor", null, null);
        var caterer = Vendor.Create(Guid.NewGuid(), EventId, "Catering", "Kuchyňa", null, null);
        var cheapButIncomplete = Quote.Create(Guid.NewGuid(), florist.Id, Commands.Quote(300_00) with { Transport = TransportCoverage.Unknown, PeopleCount = null });
        var complete = Quote.Create(Guid.NewGuid(), studio.Id, Commands.Quote(450_00, transport: TransportCoverage.Separate, transportMinor: 50_00));
        var czk = Quote.Create(Guid.NewGuid(), prague.Id, Commands.Quote(9000_00, "CZK"));
        var otherCategory = Quote.Create(Guid.NewGuid(), caterer.Id, Commands.Quote(10_00));

        var comparison = QuoteComparison.Build("Výzdoba", "EUR", [florist, studio, prague, caterer], [cheapButIncomplete, complete, czk, otherCategory]);

        Assert.Equal(3, comparison.Rows.Count);
        Assert.Equal(["EUR", "CZK"], comparison.Currencies);

        var incomplete = comparison.Rows.Single(r => r.Quote.Id == cheapButIncomplete.Id);
        Assert.Null(incomplete.Quote.KnownTotalMinor);
        Assert.False(incomplete.IsLowestKnownInCurrency);
        Assert.Contains(QuoteFields.Transport, incomplete.MissingFields);
        Assert.Contains(QuoteFields.PeopleCount, incomplete.MissingFields);

        var studioRow = comparison.Rows.Single(r => r.Quote.Id == complete.Id);
        Assert.Equal(500_00, studioRow.Quote.KnownTotalMinor);
        Assert.True(studioRow.IsLowestKnownInCurrency);
        Assert.Empty(studioRow.MissingFields);

        // The CZK quote is lowest only within CZK; it is never compared with EUR.
        var czkRow = comparison.Rows.Single(r => r.Quote.Id == czk.Id);
        Assert.False(czkRow.IsProjectCurrency);
        Assert.True(czkRow.IsLowestKnownInCurrency);
    }

    [Fact]
    public void Separate_transport_without_price_is_incomplete()
    {
        var vendor = Vendor.Create(Guid.NewGuid(), EventId, "Hudba", "DJ", null, null);
        var quote = Quote.Create(Guid.NewGuid(), vendor.Id, Commands.Quote(800_00, transport: TransportCoverage.Separate) with { ExclusionsText = null });

        var row = Assert.Single(QuoteComparison.Build("Hudba", "EUR", [vendor], [quote]).Rows);

        Assert.Null(row.Quote.KnownTotalMinor);
        Assert.Contains(QuoteFields.TransportPrice, row.MissingFields);
        Assert.Contains(QuoteFields.Exclusions, row.MissingFields);
    }

    [Fact]
    public void Declined_quote_is_never_marked_lowest()
    {
        var vendor = Vendor.Create(Guid.NewGuid(), EventId, "Foto", "Fotograf", null, null);
        var declined = Quote.Create(Guid.NewGuid(), vendor.Id, Commands.Quote(100_00));
        declined.Decline();
        var open = Quote.Create(Guid.NewGuid(), vendor.Id, Commands.Quote(200_00));

        var rows = QuoteComparison.Build("Foto", "EUR", [vendor], [declined, open]).Rows;

        Assert.False(rows.Single(r => r.Quote.Id == declined.Id).IsLowestKnownInCurrency);
        Assert.True(rows.Single(r => r.Quote.Id == open.Id).IsLowestKnownInCurrency);
    }

    [Fact]
    public void Transport_price_is_dropped_unless_charged_separately()
    {
        var quote = Quote.Create(Guid.NewGuid(), Guid.NewGuid(), Commands.Quote(100_00, transport: TransportCoverage.Included, transportMinor: 30_00));

        Assert.Null(quote.TransportMinor);
        Assert.Equal(100_00, quote.KnownTotalMinor);
    }
}
