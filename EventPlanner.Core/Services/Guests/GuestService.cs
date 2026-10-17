using EventPlanner.Contracts.Guests;
using EventPlanner.Contracts.Results;
using EventPlanner.Core.Models.Events;
using EventPlanner.Core.Models.Guests;
using EventPlanner.Core.Ports;
using EventPlanner.Core.Services.Events;
using EventPlanner.Core.Validation;
using EventPlanner.Core.Validation.Guests;
using Microsoft.Extensions.Logging;

namespace EventPlanner.Core.Services.Guests;

public sealed class GuestService(IDataSessionFactory sessions, ILogger<GuestService> logger)
{
    private const string GuestEntity = "Guest";
    private const string GroupEntity = "ParticipantGroup";
    private const string EventEntity = "EventProject";

    public async Task<IReadOnlyList<GuestDto>> ListGuestsAsync(Guid eventId, CancellationToken cancellationToken)
    {
        await using var session = sessions.Open();
        var guests = await session.Guests.ListGuestsAsync(eventId, cancellationToken);
        return [.. guests.OrderBy(g => g.DisplayName, StringComparer.CurrentCulture).Select(g => g.ToDto())];
    }

    public async Task<IReadOnlyList<GroupDto>> ListGroupsAsync(Guid eventId, CancellationToken cancellationToken)
    {
        await using var session = sessions.Open();
        var groups = await session.Guests.ListGroupsAsync(eventId, cancellationToken);
        return [.. groups.OrderBy(g => g.Label, StringComparer.CurrentCulture).Select(g => g.ToDto())];
    }

    /// <summary>Explicit load of the sensitive private note for editing.</summary>
    public async Task<Result<GuestDetailDto>> GetGuestDetailAsync(Guid guestId, CancellationToken cancellationToken)
    {
        await using var session = sessions.Open();
        var guest = await session.Guests.FindGuestAsync(guestId, cancellationToken);
        return guest is null ? OperationError.NotFound(GuestEntity) : Result<GuestDetailDto>.Success(guest.ToDetailDto());
    }

    public async Task<Result<GuestCountsDto>> GetCountsAsync(Guid eventId, CancellationToken cancellationToken)
    {
        await using var session = sessions.Open();
        var project = await session.Events.FindAsync(eventId, cancellationToken);
        if (project is null)
        {
            return OperationError.NotFound(EventEntity);
        }

        var guests = await session.Guests.ListGuestsAsync(eventId, cancellationToken);
        return GuestCountCalculator.Calculate(project.ProgramParts, guests);
    }

    public async Task<Result<GroupDto>> CreateGroupAsync(CreateGroup command, CancellationToken cancellationToken)
    {
        var (contactName, email, phone, notes) = Normalize(command.ContactName, command.ContactEmail, command.Phone, command.Notes);
        var issues = GuestValidator.ValidateGroup(command.Label, contactName, email, phone, notes);
        if (issues.Count > 0)
        {
            return OperationError.Validation(issues);
        }

        await using var session = sessions.Open();
        var writable = await WritableEvent.FindAsync(session, command.EventId, cancellationToken);
        if (writable.Error is not null)
        {
            return writable.Error;
        }

        var group = ParticipantGroup.Create(Guid.CreateVersion7(), command.EventId, command.Label, contactName, email, phone, notes);
        session.Guests.AddGroup(group);
        if (await session.SaveChangesAsync(cancellationToken) == SaveOutcome.Conflict)
        {
            return OperationError.Conflict(GroupEntity);
        }
        return group.ToDto();
    }

    public async Task<Result<GroupDto>> UpdateGroupAsync(UpdateGroup command, CancellationToken cancellationToken)
    {
        var (contactName, email, phone, notes) = Normalize(command.ContactName, command.ContactEmail, command.Phone, command.Notes);
        var issues = GuestValidator.ValidateGroup(command.Label, contactName, email, phone, notes);
        if (issues.Count > 0)
        {
            return OperationError.Validation(issues);
        }

        await using var session = sessions.Open();
        var group = await session.Guests.FindGroupAsync(command.GroupId, cancellationToken);
        if (group is null)
        {
            return OperationError.NotFound(GroupEntity);
        }

        var writable = await WritableEvent.FindAsync(session, group.EventId, cancellationToken);
        if (writable.Error is not null)
        {
            return writable.Error;
        }

        group.Update(command.Label, contactName, email, phone, notes);
        if (await session.SaveChangesAsync(cancellationToken) == SaveOutcome.Conflict)
        {
            return OperationError.Conflict(GroupEntity);
        }
        return group.ToDto();
    }

    public async Task<Result<Guid>> DeleteGroupAsync(DeleteGroup command, CancellationToken cancellationToken)
    {
        await using var session = sessions.Open();
        var group = await session.Guests.FindGroupAsync(command.GroupId, cancellationToken);
        if (group is null)
        {
            return OperationError.NotFound(GroupEntity);
        }

        var writable = await WritableEvent.FindAsync(session, group.EventId, cancellationToken);
        if (writable.Error is not null)
        {
            return writable.Error;
        }

        // Guests are kept; only the link is removed so nobody silently disappears from the list.
        foreach (var member in await session.Guests.ListGroupMembersAsync(group.Id, cancellationToken))
        {
            member.LeaveGroup();
        }

        session.Guests.RemoveGroup(group);
        if (await session.SaveChangesAsync(cancellationToken) == SaveOutcome.Conflict)
        {
            return OperationError.Conflict(GuestEntity);
        }

        return group.Id;
    }

    public async Task<Result<GuestDto>> CreateGuestAsync(CreateGuest command, CancellationToken cancellationToken)
    {
        var ageCategory = TextValidation.NullIfBlank(command.AgeCategory);
        var privateNote = TextValidation.NullIfBlank(command.PrivateNote);
        var issues = GuestValidator.ValidateGuest(command.DisplayName, command.Kind, ageCategory, command.RsvpState, privateNote);
        if (issues.Count > 0)
        {
            return OperationError.Validation(issues);
        }

        await using var session = sessions.Open();
        var writable = await WritableEvent.FindAsync(session, command.EventId, cancellationToken);
        if (writable.Error is not null)
        {
            return writable.Error;
        }

        var referenceError = await ValidateReferencesAsync(session, writable.Value, command.GroupId, command.ProgramPartIds, cancellationToken);
        if (referenceError is not null)
        {
            return referenceError;
        }

        var warnings = await DuplicateNameWarningsAsync(session, command.EventId, null, command.DisplayName, cancellationToken);
        var guest = Guest.Create(
            Guid.CreateVersion7(),
            command.EventId,
            command.GroupId,
            command.DisplayName,
            command.Kind,
            ageCategory,
            command.RsvpState,
            command.ProgramPartIds,
            privateNote);
        session.Guests.AddGuest(guest);
        if (await session.SaveChangesAsync(cancellationToken) == SaveOutcome.Conflict)
        {
            return OperationError.Conflict(GuestEntity);
        }
        return Result<GuestDto>.Success(guest.ToDto(), warnings);
    }

    public async Task<Result<GuestDto>> UpdateGuestAsync(UpdateGuest command, CancellationToken cancellationToken)
    {
        var ageCategory = TextValidation.NullIfBlank(command.AgeCategory);
        var privateNote = TextValidation.NullIfBlank(command.PrivateNote);
        var issues = GuestValidator.ValidateGuest(command.DisplayName, command.Kind, ageCategory, command.RsvpState, privateNote);
        if (issues.Count > 0)
        {
            return OperationError.Validation(issues);
        }

        await using var session = sessions.Open();
        var guest = await session.Guests.FindGuestAsync(command.GuestId, cancellationToken);
        if (guest is null)
        {
            return OperationError.NotFound(GuestEntity);
        }

        var writable = await WritableEvent.FindAsync(session, guest.EventId, cancellationToken);
        if (writable.Error is not null)
        {
            return writable.Error;
        }

        if (guest.Revision != command.ExpectedRevision)
        {
            return OperationError.Conflict(GuestEntity);
        }

        var referenceError = await ValidateReferencesAsync(session, writable.Value, command.GroupId, command.ProgramPartIds, cancellationToken);
        if (referenceError is not null)
        {
            return referenceError;
        }

        var warnings = await DuplicateNameWarningsAsync(session, guest.EventId, guest.Id, command.DisplayName, cancellationToken);
        guest.Update(command.GroupId, command.DisplayName, command.Kind, ageCategory, command.RsvpState, command.ProgramPartIds, privateNote);
        if (await session.SaveChangesAsync(cancellationToken) == SaveOutcome.Conflict)
        {
            return OperationError.Conflict(GuestEntity);
        }

        return Result<GuestDto>.Success(guest.ToDto(), warnings);
    }

    public async Task<Result<Guid>> DeleteGuestAsync(DeleteGuest command, CancellationToken cancellationToken)
    {
        await using var session = sessions.Open();
        var guest = await session.Guests.FindGuestAsync(command.GuestId, cancellationToken);
        if (guest is null)
        {
            return OperationError.NotFound(GuestEntity);
        }

        var writable = await WritableEvent.FindAsync(session, guest.EventId, cancellationToken);
        if (writable.Error is not null)
        {
            return writable.Error;
        }

        if (guest.Revision != command.ExpectedRevision)
        {
            return OperationError.Conflict(GuestEntity);
        }

        session.Guests.RemoveGuest(guest);
        if (await session.SaveChangesAsync(cancellationToken) == SaveOutcome.Conflict)
        {
            return OperationError.Conflict(GuestEntity);
        }

        return guest.Id;
    }

    /// <summary>
    /// Applies one RSVP state to the listed group members in a single transaction. Any stale
    /// revision or non-member aborts the whole command; unlisted members keep their state.
    /// </summary>
    public async Task<Result<IReadOnlyList<GuestDto>>> SetGroupRsvpAsync(SetGroupRsvp command, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(command.NewState))
        {
            return OperationError.Validation(nameof(command.NewState), ValidationRules.InvalidValue);
        }

        if (command.ApplyTo.Count == 0)
        {
            return OperationError.Validation(nameof(command.ApplyTo), ValidationRules.Required);
        }

        await using var session = sessions.Open();
        var group = await session.Guests.FindGroupAsync(command.GroupId, cancellationToken);
        if (group is null)
        {
            return OperationError.NotFound(GroupEntity);
        }

        var writable = await WritableEvent.FindAsync(session, group.EventId, cancellationToken);
        if (writable.Error is not null)
        {
            return writable.Error;
        }

        var members = (await session.Guests.ListGroupMembersAsync(group.Id, cancellationToken)).ToDictionary(g => g.Id);
        foreach (var target in command.ApplyTo)
        {
            if (!members.TryGetValue(target.GuestId, out var member))
            {
                return OperationError.Validation(nameof(command.ApplyTo), ValidationRules.NotInGroup);
            }

            if (member.Revision != target.ExpectedRevision)
            {
                return OperationError.Conflict(GuestEntity);
            }
        }

        foreach (var target in command.ApplyTo)
        {
            members[target.GuestId].SetRsvp(command.NewState);
        }

        if (await session.SaveChangesAsync(cancellationToken) == SaveOutcome.Conflict)
        {
            return OperationError.Conflict(GuestEntity);
        }

        logger.LogInformation("Group RSVP applied to {Count} guests in group {GroupId}", command.ApplyTo.Count, group.Id);
        return Result<IReadOnlyList<GuestDto>>.Success([.. command.ApplyTo.Select(t => members[t.GuestId].ToDto())]);
    }

    /// <summary>Cross-event links are forbidden: group and program parts must belong to the same event.</summary>
    private static async Task<OperationError?> ValidateReferencesAsync(
        IDataSession session, EventProject project, Guid? groupId, IReadOnlyCollection<Guid> programPartIds, CancellationToken cancellationToken)
    {
        if (groupId is { } id)
        {
            var group = await session.Guests.FindGroupAsync(id, cancellationToken);
            if (group is null || group.EventId != project.Id)
            {
                return OperationError.Validation(nameof(GuestDto.GroupId), ValidationRules.UnknownReference);
            }
        }

        var knownParts = project.ProgramParts.Select(p => p.Id).ToHashSet();
        if (programPartIds.Any(p => !knownParts.Contains(p)))
        {
            return OperationError.Validation(nameof(GuestDto.ProgramPartIds), ValidationRules.UnknownReference);
        }

        return null;
    }

    private static async Task<IReadOnlyList<Warning>> DuplicateNameWarningsAsync(
        IDataSession session, Guid eventId, Guid? excludeGuestId, string displayName, CancellationToken cancellationToken)
    {
        var names = await session.Guests.ListGuestNamesAsync(eventId, excludeGuestId, cancellationToken);
        var normalized = NormalizeForComparison(displayName);
        return names.Any(n => NormalizeForComparison(n) == normalized)
            ? [new Warning(WarningCodes.DuplicateGuestName, nameof(GuestDto.DisplayName))]
            : [];
    }

    // Normalization is used only for comparison; the stored name is left as typed.
    private static string NormalizeForComparison(string name) => name.Trim().ToUpperInvariant();

    private static (string? ContactName, string? Email, string? Phone, string? Notes) Normalize(
        string? contactName, string? email, string? phone, string? notes) =>
        (TextValidation.NullIfBlank(contactName), TextValidation.NullIfBlank(email), TextValidation.NullIfBlank(phone), TextValidation.NullIfBlank(notes));
}
