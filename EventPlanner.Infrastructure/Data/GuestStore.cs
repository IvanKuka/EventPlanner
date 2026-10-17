using EventPlanner.Core.Models.Guests;
using EventPlanner.Core.Ports;
using Microsoft.EntityFrameworkCore;

namespace EventPlanner.Infrastructure.Data;

internal sealed class GuestStore(EventPlannerDbContext db) : IGuestStore
{
    public async Task<IReadOnlyList<Guest>> ListGuestsAsync(Guid eventId, CancellationToken cancellationToken) =>
        await db.Guests.AsNoTracking().Include(g => g.Attendance).Where(g => g.EventId == eventId).ToListAsync(cancellationToken);

    public Task<Guest?> FindGuestAsync(Guid guestId, CancellationToken cancellationToken) =>
        db.Guests.Include(g => g.Attendance).FirstOrDefaultAsync(g => g.Id == guestId, cancellationToken);

    public async Task<IReadOnlyList<Guest>> ListGroupMembersAsync(Guid groupId, CancellationToken cancellationToken) =>
        await db.Guests.Include(g => g.Attendance).Where(g => g.GroupId == groupId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> ListGuestNamesAsync(Guid eventId, Guid? excludeGuestId, CancellationToken cancellationToken) =>
        await db.Guests.AsNoTracking()
            .Where(g => g.EventId == eventId && g.Id != excludeGuestId)
            .Select(g => g.DisplayName)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ParticipantGroup>> ListGroupsAsync(Guid eventId, CancellationToken cancellationToken) =>
        await db.ParticipantGroups.AsNoTracking().Where(g => g.EventId == eventId).ToListAsync(cancellationToken);

    public Task<ParticipantGroup?> FindGroupAsync(Guid groupId, CancellationToken cancellationToken) =>
        db.ParticipantGroups.FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);

    public void AddGuest(Guest guest) => db.Guests.Add(guest);

    public void RemoveGuest(Guest guest) => db.Guests.Remove(guest);

    public void AddGroup(ParticipantGroup group) => db.ParticipantGroups.Add(group);

    public void RemoveGroup(ParticipantGroup group) => db.ParticipantGroups.Remove(group);
}
