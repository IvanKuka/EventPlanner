using EventPlanner.Contracts.Guests;

namespace EventPlanner.Core.Models.Guests;

/// <summary>
/// A guest has a stable id independent of the name; identical names are allowed (warning only).
/// </summary>
public sealed class Guest
{
    private readonly List<GuestAttendance> _attendance = [];

    private Guest()
    {
    }

    public Guid Id { get; private set; }

    public Guid EventId { get; private set; }

    public Guid? GroupId { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public GuestKind Kind { get; private set; }

    public string? AgeCategory { get; private set; }

    public RsvpState RsvpState { get; private set; }

    /// <summary>Sensitive; excluded from list DTOs, logs and default exports.</summary>
    public string? PrivateNote { get; private set; }

    public long Revision { get; private set; }

    public IReadOnlyList<GuestAttendance> Attendance => _attendance;

    public IEnumerable<Guid> ProgramPartIds => _attendance.Select(a => a.ProgramPartId);

    public static Guest Create(
        Guid id,
        Guid eventId,
        Guid? groupId,
        string displayName,
        GuestKind kind,
        string? ageCategory,
        RsvpState rsvpState,
        IEnumerable<Guid> programPartIds,
        string? privateNote)
    {
        var guest = new Guest { Id = id, EventId = eventId, Revision = 1 };
        guest.Apply(groupId, displayName, kind, ageCategory, rsvpState, programPartIds, privateNote);
        return guest;
    }

    public void Update(
        Guid? groupId,
        string displayName,
        GuestKind kind,
        string? ageCategory,
        RsvpState rsvpState,
        IEnumerable<Guid> programPartIds,
        string? privateNote)
    {
        Apply(groupId, displayName, kind, ageCategory, rsvpState, programPartIds, privateNote);
        Revision++;
    }

    public void SetRsvp(RsvpState state)
    {
        if (RsvpState == state)
        {
            return;
        }

        RsvpState = state;
        Revision++;
    }

    public void LeaveGroup()
    {
        GroupId = null;
        Revision++;
    }

    private void Apply(
        Guid? groupId,
        string displayName,
        GuestKind kind,
        string? ageCategory,
        RsvpState rsvpState,
        IEnumerable<Guid> programPartIds,
        string? privateNote)
    {
        GroupId = groupId;
        DisplayName = displayName.Trim();
        Kind = kind;
        AgeCategory = ageCategory;
        RsvpState = rsvpState;
        PrivateNote = privateNote;

        var wanted = programPartIds.ToHashSet();
        _attendance.RemoveAll(a => !wanted.Contains(a.ProgramPartId));
        foreach (var partId in wanted.Except(_attendance.Select(a => a.ProgramPartId)))
        {
            _attendance.Add(new GuestAttendance(Id, partId));
        }
    }
}
