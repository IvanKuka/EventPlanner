using EventPlanner.Contracts.Events;
using EventPlanner.Contracts.Guests;
using EventPlanner.Contracts.Vendors;

namespace EventPlanner.Tests.Fixtures;

internal static class Commands
{
    public static CreateEventProject Wedding(string name = "Svadba test") => new(
        name,
        EventType.Wedding,
        new DateOnly(2027, 6, 12),
        new DateOnly(2027, 6, 13),
        "Europe/Bratislava",
        "Sála",
        "EUR",
        EventTemplate.Wedding);

    public static CreateGuest Guest(Guid eventId, string name, RsvpState rsvp, IReadOnlyCollection<Guid> parts, Guid? groupId = null, GuestKind kind = GuestKind.Named) =>
        new(eventId, groupId, name, kind, null, rsvp, parts, null);

    /// <summary>A complete quote (transport included, people count, exclusions and validity stated).</summary>
    public static QuoteInput Quote(long amountMinor, string currency = "EUR", TransportCoverage transport = TransportCoverage.Included, long? transportMinor = null) =>
        new("Výzdoba sály", amountMinor, currency, "kvety, stuhy", "obrusy", new DateOnly(2027, 1, 31), transport, transportMinor, 80);
}
