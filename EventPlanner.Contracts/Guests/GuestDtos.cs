namespace EventPlanner.Contracts.Guests;

public sealed record GroupDto(Guid Id, Guid EventId, string Label, string? ContactName, string? ContactEmail, string? Phone, string? Notes);

/// <remarks>The private note is deliberately not part of the list DTO; load <see cref="GuestDetailDto"/> to edit it.</remarks>
public sealed record GuestDto(
    Guid Id,
    Guid EventId,
    Guid? GroupId,
    string DisplayName,
    GuestKind Kind,
    string? AgeCategory,
    RsvpState RsvpState,
    IReadOnlyList<Guid> ProgramPartIds,
    bool HasPrivateNote,
    long Revision);

public sealed record GuestDetailDto(GuestDto Guest, string? PrivateNote);

/// <summary>Counts for one program part, computed from guest records.</summary>
/// <param name="AcceptedPlaceholders">How many of <paramref name="Accepted"/> are unnamed plus-one seats.</param>
public sealed record ProgramPartCountsDto(
    Guid ProgramPartId,
    string Label,
    int Accepted,
    int Tentative,
    int Invited,
    int Unknown,
    int Declined,
    int AcceptedPlaceholders);

/// <param name="GuestsWithoutProgramPart">Guests not assigned to any part; shown as an open issue, not hidden.</param>
/// <param name="GuestsAwaitingAnswer">Distinct guests whose RSVP is still Unknown or Invited.</param>
public sealed record GuestCountsDto(
    int TotalGuests,
    IReadOnlyList<ProgramPartCountsDto> Parts,
    int GuestsWithoutProgramPart,
    int GuestsAwaitingAnswer);
