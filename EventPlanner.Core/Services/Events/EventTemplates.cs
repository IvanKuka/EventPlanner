using EventPlanner.Contracts.Events;

namespace EventPlanner.Core.Services.Events;

/// <summary>
/// Starter program parts per template. Labels are user data once created (editable, Slovak by
/// default as the product language); templates never set dates, headcounts or vendors.
/// </summary>
public static class EventTemplates
{
    public static IReadOnlyList<string> ProgramPartLabels(EventTemplate template) => template switch
    {
        EventTemplate.Wedding => ["Obrad", "Hostina", "Večerná zábava"],
        EventTemplate.Celebration => ["Oslava"],
        _ => [],
    };
}
