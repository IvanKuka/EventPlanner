using EventPlanner.Contracts.Events;
using EventPlanner.Contracts.Guests;
using EventPlanner.Core.Models.Events;
using EventPlanner.Core.Models.Guests;
using EventPlanner.Core.Services.Guests;

namespace EventPlanner.Tests.Unit.Guests;

public sealed class GuestCountCalculatorTests
{
    /// <summary>E-A01: 200 guests with group exceptions → correct counts per ceremony/dinner and differing RSVPs.</summary>
    [Fact]
    public void Counts_per_program_part_respect_individual_scope_and_rsvp()
    {
        var project = EventProject.Create(
            Guid.NewGuid(), "Svadba", EventType.Wedding, new DateOnly(2027, 6, 12), new DateOnly(2027, 6, 12),
            "Europe/Bratislava", null, "EUR", DateTimeOffset.UtcNow);
        var ceremony = project.AddProgramPart(Guid.NewGuid(), "Obrad");
        var dinner = project.AddProgramPart(Guid.NewGuid(), "Hostina");

        var guests = new List<Guest>();

        // 120 accepted for both parts.
        guests.AddRange(Make(project.Id, 120, RsvpState.Accepted, ceremony.Id, dinner.Id));

        // 40 accepted for the ceremony only (exceptions within groups).
        guests.AddRange(Make(project.Id, 40, RsvpState.Accepted, ceremony.Id));

        // 20 declined, 10 tentative and 5 unknown for both parts.
        guests.AddRange(Make(project.Id, 20, RsvpState.Declined, ceremony.Id, dinner.Id));
        guests.AddRange(Make(project.Id, 10, RsvpState.Tentative, ceremony.Id, dinner.Id));
        guests.AddRange(Make(project.Id, 5, RsvpState.Unknown, dinner.Id));

        // 3 accepted unnamed plus-one seats for dinner, 2 guests with no part assigned yet.
        guests.AddRange(Make(project.Id, 3, RsvpState.Accepted, GuestKind.PlusOnePlaceholder, dinner.Id));
        guests.AddRange(Make(project.Id, 2, RsvpState.Invited));

        var counts = GuestCountCalculator.Calculate(project.ProgramParts, guests);

        Assert.Equal(200, counts.TotalGuests);
        Assert.Equal(2, counts.GuestsWithoutProgramPart);
        Assert.Equal(7, counts.GuestsAwaitingAnswer);

        var ceremonyCounts = counts.Parts.Single(p => p.ProgramPartId == ceremony.Id);
        Assert.Equal(160, ceremonyCounts.Accepted);
        Assert.Equal(20, ceremonyCounts.Declined);
        Assert.Equal(10, ceremonyCounts.Tentative);
        Assert.Equal(0, ceremonyCounts.Unknown);

        var dinnerCounts = counts.Parts.Single(p => p.ProgramPartId == dinner.Id);
        Assert.Equal(123, dinnerCounts.Accepted);
        Assert.Equal(3, dinnerCounts.AcceptedPlaceholders);
        Assert.Equal(5, dinnerCounts.Unknown);
        Assert.Equal(20, dinnerCounts.Declined);
    }

    [Fact]
    public void Parts_are_ordered_and_empty_parts_report_zero_not_missing()
    {
        var project = EventProject.Create(
            Guid.NewGuid(), "Oslava", EventType.Celebration, new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 1),
            "Europe/Bratislava", null, "EUR", DateTimeOffset.UtcNow);
        project.AddProgramPart(Guid.NewGuid(), "A");
        project.AddProgramPart(Guid.NewGuid(), "B");

        var counts = GuestCountCalculator.Calculate(project.ProgramParts, []);

        Assert.Equal(["A", "B"], counts.Parts.Select(p => p.Label));
        Assert.All(counts.Parts, p => Assert.Equal(0, p.Accepted));
    }

    private static IEnumerable<Guest> Make(Guid eventId, int count, RsvpState rsvp, params Guid[] parts) =>
        Make(eventId, count, rsvp, GuestKind.Named, parts);

    private static IEnumerable<Guest> Make(Guid eventId, int count, RsvpState rsvp, GuestKind kind, params Guid[] parts) =>
        Enumerable.Range(0, count).Select(i =>
            Guest.Create(Guid.NewGuid(), eventId, null, $"Hosť {i}", kind, null, rsvp, parts, null));
}
