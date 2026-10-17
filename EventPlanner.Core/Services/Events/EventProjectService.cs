using EventPlanner.Contracts.Events;
using EventPlanner.Contracts.Results;
using EventPlanner.Core.Models.Events;
using EventPlanner.Core.Ports;
using EventPlanner.Core.Validation;
using EventPlanner.Core.Validation.Events;
using Microsoft.Extensions.Logging;

namespace EventPlanner.Core.Services.Events;

public sealed class EventProjectService(IDataSessionFactory sessions, TimeProvider clock, ILogger<EventProjectService> logger)
{
    private const string Entity = "EventProject";

    public async Task<IReadOnlyList<EventSummaryDto>> ListAsync(CancellationToken cancellationToken)
    {
        await using var session = sessions.Open();
        var projects = await session.Events.ListAsync(cancellationToken);
        return [.. projects.OrderBy(p => p.LocalStartDate).ThenBy(p => p.Name, StringComparer.CurrentCulture).Select(p => p.ToSummary())];
    }

    public async Task<Result<EventProjectDto>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var session = sessions.Open();
        var project = await session.Events.FindAsync(id, cancellationToken);
        return project is null ? OperationError.NotFound(Entity) : Result<EventProjectDto>.Success(project.ToDto());
    }

    public async Task<Result<EventProjectDto>> CreateAsync(CreateEventProject command, CancellationToken cancellationToken)
    {
        var venue = TextValidation.NullIfBlank(command.VenueText);
        var issues = EventProjectValidator.Validate(
            command.Name, command.EventType, command.LocalStartDate, command.LocalEndDate, command.TimeZoneId, venue, command.Currency);
        if (!Enum.IsDefined(command.Template))
        {
            issues.Add(new ValidationIssue(nameof(command.Template), ValidationRules.InvalidValue));
        }

        if (issues.Count > 0)
        {
            return OperationError.Validation(issues);
        }

        var project = EventProject.Create(
            Guid.CreateVersion7(),
            command.Name,
            command.EventType,
            command.LocalStartDate,
            command.LocalEndDate,
            command.TimeZoneId,
            venue,
            command.Currency,
            clock.GetUtcNow());

        foreach (var label in EventTemplates.ProgramPartLabels(command.Template))
        {
            project.AddProgramPart(Guid.CreateVersion7(), label);
        }

        await using var session = sessions.Open();
        session.Events.Add(project);
        if (await session.SaveChangesAsync(cancellationToken) == SaveOutcome.Conflict)
        {
            return OperationError.Conflict(Entity);
        }
        logger.LogInformation("Event project {EventId} created", project.Id);
        return project.ToDto();
    }

    public Task<Result<EventProjectDto>> UpdateAsync(UpdateEventProject command, CancellationToken cancellationToken)
    {
        var venue = TextValidation.NullIfBlank(command.VenueText);
        var issues = EventProjectValidator.Validate(
            command.Name, command.EventType, command.LocalStartDate, command.LocalEndDate, command.TimeZoneId, venue, command.Currency);
        if (issues.Count > 0)
        {
            return Task.FromResult<Result<EventProjectDto>>(OperationError.Validation(issues));
        }

        return MutateAsync(command.Id, command.ExpectedRevision, project =>
        {
            project.UpdateDetails(
                command.Name, command.EventType, command.LocalStartDate, command.LocalEndDate, command.TimeZoneId, venue, command.Currency);
            return null;
        }, cancellationToken);
    }

    public async Task<Result<EventProjectDto>> ChangeStatusAsync(ChangeEventStatus command, CancellationToken cancellationToken)
    {
        await using var session = sessions.Open();
        var project = await session.Events.FindAsync(command.Id, cancellationToken);
        if (project is null)
        {
            return OperationError.NotFound(Entity);
        }

        if (project.Revision != command.ExpectedRevision)
        {
            return OperationError.Conflict(Entity);
        }

        if (!EventStatusRules.CanTransition(project.Status, command.Target))
        {
            return OperationError.Validation(nameof(EventProjectDto.Status), ValidationRules.InvalidTransition);
        }

        if (EventStatusRules.RequiresConfirmation(command.Target) && !command.ChecklistConfirmed)
        {
            return OperationError.Validation(nameof(command.ChecklistConfirmed), ValidationRules.ConfirmationRequired);
        }

        project.ChangeStatus(command.Target);
        return await CommitAsync(session, project, cancellationToken);
    }

    public Task<Result<EventProjectDto>> AddProgramPartAsync(AddProgramPart command, CancellationToken cancellationToken)
    {
        var issues = new List<ValidationIssue>();
        TextValidation.RequiredName(nameof(ProgramPartDto.Label), command.Label, issues);
        if (issues.Count > 0)
        {
            return Task.FromResult<Result<EventProjectDto>>(OperationError.Validation(issues));
        }

        return MutateAsync(command.EventId, command.ExpectedRevision, project =>
        {
            project.AddProgramPart(Guid.CreateVersion7(), command.Label.Trim());
            return null;
        }, cancellationToken);
    }

    public Task<Result<EventProjectDto>> RenameProgramPartAsync(RenameProgramPart command, CancellationToken cancellationToken)
    {
        var issues = new List<ValidationIssue>();
        TextValidation.RequiredName(nameof(ProgramPartDto.Label), command.Label, issues);
        if (issues.Count > 0)
        {
            return Task.FromResult<Result<EventProjectDto>>(OperationError.Validation(issues));
        }

        return MutateAsync(command.EventId, command.ExpectedRevision, project =>
        {
            var part = project.ProgramParts.FirstOrDefault(p => p.Id == command.PartId);
            if (part is null)
            {
                return OperationError.NotFound("ProgramPart");
            }

            project.RenameProgramPart(part, command.Label.Trim());
            return null;
        }, cancellationToken);
    }

    public Task<Result<EventProjectDto>> RemoveProgramPartAsync(RemoveProgramPart command, CancellationToken cancellationToken) =>
        MutateAsync(command.EventId, command.ExpectedRevision, project =>
        {
            var part = project.ProgramParts.FirstOrDefault(p => p.Id == command.PartId);
            if (part is null)
            {
                return OperationError.NotFound("ProgramPart");
            }

            project.RemoveProgramPart(part);
            return null;
        }, cancellationToken);

    /// <summary>
    /// Loads, checks revision and read-only state, applies <paramref name="change"/> and commits.
    /// The change returns an error to abort without writing.
    /// </summary>
    private async Task<Result<EventProjectDto>> MutateAsync(
        Guid id, long expectedRevision, Func<EventProject, OperationError?> change, CancellationToken cancellationToken)
    {
        await using var session = sessions.Open();
        var project = await session.Events.FindAsync(id, cancellationToken);
        if (project is null)
        {
            return OperationError.NotFound(Entity);
        }

        if (project.IsReadOnly)
        {
            return OperationError.ReadOnly("Archived event is read-only.");
        }

        if (project.Revision != expectedRevision)
        {
            return OperationError.Conflict(Entity);
        }

        var error = change(project);
        if (error is not null)
        {
            return error;
        }

        return await CommitAsync(session, project, cancellationToken);
    }

    /// <summary>
    /// Permanent delete of the project and all it owns. The typed name must match exactly (after
    /// trimming), so the command itself, not only the UI, refuses an unconfirmed delete.
    /// </summary>
    public async Task<Result<Guid>> DeleteAsync(DeleteEventProject command, CancellationToken cancellationToken)
    {
        await using var session = sessions.Open();
        var project = await session.Events.FindAsync(command.Id, cancellationToken);
        if (project is null)
        {
            return OperationError.NotFound(Entity);
        }

        if (project.Revision != command.ExpectedRevision)
        {
            return OperationError.Conflict(Entity);
        }

        if (!string.Equals(command.ConfirmationName?.Trim(), project.Name, StringComparison.Ordinal))
        {
            return OperationError.Validation(nameof(command.ConfirmationName), ValidationRules.ConfirmationMismatch);
        }

        session.Events.Remove(project);
        if (await session.SaveChangesAsync(cancellationToken) == SaveOutcome.Conflict)
        {
            return OperationError.Conflict(Entity);
        }

        logger.LogInformation("Event project {EventId} deleted", project.Id);
        return project.Id;
    }

    private async Task<Result<EventProjectDto>> CommitAsync(IDataSession session, EventProject project, CancellationToken cancellationToken)
    {
        if (await session.SaveChangesAsync(cancellationToken) == SaveOutcome.Conflict)
        {
            logger.LogWarning("Event project {EventId} save rejected: concurrent change", project.Id);
            return OperationError.Conflict(Entity);
        }

        return project.ToDto();
    }
}
