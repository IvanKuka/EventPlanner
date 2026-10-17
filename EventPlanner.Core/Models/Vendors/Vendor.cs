namespace EventPlanner.Core.Models.Vendors;

/// <summary>A supplier considered for the event. Stored contacts never authorize the app to contact anyone.</summary>
public sealed class Vendor
{
    private Vendor()
    {
    }

    public Guid Id { get; private set; }

    public Guid EventId { get; private set; }

    /// <summary>Free label; quotes are compared within a category (case-insensitive).</summary>
    public string Category { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Contact { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>Optimistic concurrency token; bumped on every change so a stale edit is rejected, not merged.</summary>
    public long Revision { get; private set; }

    public static Vendor Create(Guid id, Guid eventId, string category, string name, string? contact, string? notes)
    {
        var vendor = new Vendor { Id = id, EventId = eventId, Revision = 1 };
        vendor.Apply(category, name, contact, notes);
        return vendor;
    }

    public void Update(string category, string name, string? contact, string? notes)
    {
        Apply(category, name, contact, notes);
        Revision++;
    }

    private void Apply(string category, string name, string? contact, string? notes)
    {
        Category = category.Trim();
        Name = name.Trim();
        Contact = contact;
        Notes = notes;
    }
}
