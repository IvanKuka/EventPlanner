namespace EventPlanner.Contracts.Guests;

public sealed record CreateGroup(Guid EventId, string Label, string? ContactName, string? ContactEmail, string? Phone, string? Notes);

public sealed record UpdateGroup(Guid GroupId, string Label, string? ContactName, string? ContactEmail, string? Phone, string? Notes);

/// <summary>Deleting a group keeps its guests; they only lose the group link.</summary>
public sealed record DeleteGroup(Guid GroupId);

/// <param name="ProgramPartIds">Explicit parts of the program the guest attends.</param>
public sealed record CreateGuest(
    Guid EventId,
    Guid? GroupId,
    string DisplayName,
    GuestKind Kind,
    string? AgeCategory,
    RsvpState RsvpState,
    IReadOnlyCollection<Guid> ProgramPartIds,
    string? PrivateNote);

public sealed record UpdateGuest(
    Guid GuestId,
    long ExpectedRevision,
    Guid? GroupId,
    string DisplayName,
    GuestKind Kind,
    string? AgeCategory,
    RsvpState RsvpState,
    IReadOnlyCollection<Guid> ProgramPartIds,
    string? PrivateNote);

public sealed record DeleteGuest(Guid GuestId, long ExpectedRevision);

public sealed record GuestRevision(Guid GuestId, long ExpectedRevision);

/// <summary>
/// Sets one RSVP state for the explicitly listed members of a group. Members that are not
/// listed stay unchanged, so the organizer can always keep exceptions.
/// </summary>
public sealed record SetGroupRsvp(Guid GroupId, RsvpState NewState, IReadOnlyCollection<GuestRevision> ApplyTo);
