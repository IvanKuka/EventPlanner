using EventPlanner.Core.Models.Events;
using EventPlanner.Core.Ports;
using Microsoft.EntityFrameworkCore;

namespace EventPlanner.Infrastructure.Data;

internal sealed class EventProjectStore(EventPlannerDbContext db) : IEventProjectStore
{
    public async Task<IReadOnlyList<EventProject>> ListAsync(CancellationToken cancellationToken) =>
        await db.EventProjects.AsNoTracking().ToListAsync(cancellationToken);

    public Task<EventProject?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.EventProjects.Include(e => e.ProgramParts).FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public void Add(EventProject project) => db.EventProjects.Add(project);

    public void Remove(EventProject project) => db.EventProjects.Remove(project);
}
