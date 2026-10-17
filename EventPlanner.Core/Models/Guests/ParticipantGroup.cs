namespace EventPlanner.Core.Models.Guests;

/// <summary>A household or party of guests. Contact data are stored, never used to contact anyone automatically.</summary>
public sealed class ParticipantGroup
{
    private ParticipantGroup()
    {
    }

    public Guid Id { get; private set; }

    public Guid EventId { get; private set; }

    public string Label { get; private set; } = string.Empty;

    public string? ContactName { get; private set; }

    public string? ContactEmail { get; private set; }

    public string? Phone { get; private set; }

    public string? Notes { get; private set; }

    public static ParticipantGroup Create(Guid id, Guid eventId, string label, string? contactName, string? contactEmail, string? phone, string? notes)
    {
        var group = new ParticipantGroup { Id = id, EventId = eventId };
        group.Update(label, contactName, contactEmail, phone, notes);
        return group;
    }

    public void Update(string label, string? contactName, string? contactEmail, string? phone, string? notes)
    {
        Label = label.Trim();
        ContactName = contactName;
        ContactEmail = contactEmail;
        Phone = phone;
        Notes = notes;
    }
}
