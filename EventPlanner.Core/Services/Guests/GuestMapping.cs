using EventPlanner.Contracts.Guests;
using EventPlanner.Core.Models.Guests;

namespace EventPlanner.Core.Services.Guests;

internal static class GuestMapping
{
    public static GuestDto ToDto(this Guest guest) => new(
        guest.Id,
        guest.EventId,
        guest.GroupId,
        guest.DisplayName,
        guest.Kind,
        guest.AgeCategory,
        guest.RsvpState,
        [.. guest.ProgramPartIds],
        guest.PrivateNote is not null,
        guest.Revision);

    public static GuestDetailDto ToDetailDto(this Guest guest) => new(guest.ToDto(), guest.PrivateNote);

    public static GroupDto ToDto(this ParticipantGroup group) =>
        new(group.Id, group.EventId, group.Label, group.ContactName, group.ContactEmail, group.Phone, group.Notes);
}
