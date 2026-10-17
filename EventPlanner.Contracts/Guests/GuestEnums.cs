namespace EventPlanner.Contracts.Guests;

public enum RsvpState
{
    Unknown,
    Invited,
    Accepted,
    Declined,
    Tentative,
}

/// <summary>
/// A plus-one is either a named guest or an explicit unnamed placeholder seat with its own RSVP,
/// never a hidden increment of a counter.
/// </summary>
public enum GuestKind
{
    Named,
    PlusOnePlaceholder,
}
