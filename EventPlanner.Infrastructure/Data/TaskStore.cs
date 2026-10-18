using EventPlanner.Core.Models.Tasks;
using EventPlanner.Core.Ports;
using Microsoft.EntityFrameworkCore;

namespace EventPlanner.Infrastructure.Data;

internal sealed class TaskStore(EventPlannerDbContext db) : ITaskStore
{
    public async Task<IReadOnlyList<EventTask>> ListAsync(Guid eventId, CancellationToken cancellationToken) =>
        await db.Tasks.AsNoTracking().Where(t => t.EventId == eventId).ToListAsync(cancellationToken);

    public Task<EventTask?> FindAsync(Guid taskId, CancellationToken cancellationToken) =>
        db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

    public void Add(EventTask task) => db.Tasks.Add(task);

    public void Remove(EventTask task) => db.Tasks.Remove(task);
}
