using EventPlanner.Contracts.Guests;
using EventPlanner.Core.Models.Events;
using EventPlanner.Core.Models.Guests;

namespace EventPlanner.Core.Services.Guests;

/// <summary>
/// Derives headcounts per program part from guest records (never from manually kept numbers).
/// A guest counts only for the parts in their explicit attendance scope.
/// </summary>
public static class GuestCountCalculator
{
    public static GuestCountsDto Calculate(IReadOnlyList<ProgramPart> parts, IReadOnlyList<Guest> guests)
    {
        var partCounts = parts
            .OrderBy(p => p.Order)
            .Select(part => CountPart(part, guests.Where(g => g.ProgramPartIds.Contains(part.Id))))
            .ToList();

        var withoutPart = guests.Count(g => !g.Attendance.Any());
        var awaitingAnswer = guests.Count(g => g.RsvpState is RsvpState.Unknown or RsvpState.Invited);
        return new GuestCountsDto(guests.Count, partCounts, withoutPart, awaitingAnswer);
    }

    private static ProgramPartCountsDto CountPart(ProgramPart part, IEnumerable<Guest> attendees)
    {
        int accepted = 0, tentative = 0, invited = 0, unknown = 0, declined = 0, acceptedPlaceholders = 0;
        foreach (var guest in attendees)
        {
            switch (guest.RsvpState)
            {
                case RsvpState.Accepted:
                    accepted++;
                    if (guest.Kind == GuestKind.PlusOnePlaceholder)
                    {
                        acceptedPlaceholders++;
                    }

                    break;
                case RsvpState.Tentative:
                    tentative++;
                    break;
                case RsvpState.Invited:
                    invited++;
                    break;
                case RsvpState.Declined:
                    declined++;
                    break;
                default:
                    unknown++;
                    break;
            }
        }

        return new ProgramPartCountsDto(part.Id, part.Label, accepted, tentative, invited, unknown, declined, acceptedPlaceholders);
    }
}
