using EventPlanner.Contracts.Results;
using EventPlanner.Core.Models.Events;
using EventPlanner.Core.Ports;

namespace EventPlanner.Core.Services.Events;

/// <summary>Shared guard of feature services: changes are allowed only in an existing, non-archived event.</summary>
internal static class WritableEvent
{
    private const string EventEntity = "EventProject";

    public static async Task<Result<EventProject>> FindAsync(IDataSession session, Guid eventId, CancellationToken cancellationToken)
    {
        var project = await session.Events.FindAsync(eventId, cancellationToken);
        if (project is null)
        {
            return OperationError.NotFound(EventEntity);
        }

        return project.IsReadOnly
            ? OperationError.ReadOnly("Archived event is read-only.")
            : Result<EventProject>.Success(project);
    }
}
