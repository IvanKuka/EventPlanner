namespace EventPlanner.Core.Models.Guests;

/// <summary>Membership of a guest in one program part (the guest's explicit attendance scope).</summary>
public sealed class GuestAttendance
{
    private GuestAttendance()
    {
    }

    internal GuestAttendance(Guid guestId, Guid programPartId)
    {
        GuestId = guestId;
        ProgramPartId = programPartId;
    }

    public Guid GuestId { get; private set; }

    public Guid ProgramPartId { get; private set; }
}
