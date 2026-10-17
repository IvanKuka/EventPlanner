using EventPlanner.Contracts.Events;
using EventPlanner.Contracts.Results;

namespace EventPlanner.Core.Validation.Events;

public static class EventProjectValidator
{
    public static List<ValidationIssue> Validate(
        string? name,
        EventType eventType,
        DateOnly localStartDate,
        DateOnly localEndDate,
        string? timeZoneId,
        string? venueText,
        string? currency)
    {
        var issues = new List<ValidationIssue>();
        TextValidation.RequiredName(nameof(EventProjectDto.Name), name, issues);
        TextValidation.OptionalSingleLine(nameof(EventProjectDto.VenueText), venueText, issues, TextValidation.ShortTextMaxLength);

        if (!Enum.IsDefined(eventType))
        {
            issues.Add(new ValidationIssue(nameof(EventProjectDto.EventType), ValidationRules.InvalidValue));
        }

        // An event may span several days (and cross midnight), but it cannot end before it starts.
        if (localEndDate < localStartDate)
        {
            issues.Add(new ValidationIssue(nameof(EventProjectDto.LocalEndDate), ValidationRules.EndBeforeStart));
        }

        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            issues.Add(new ValidationIssue(nameof(EventProjectDto.TimeZoneId), ValidationRules.Required));
        }
        else if (!IsKnownIanaTimeZone(timeZoneId))
        {
            issues.Add(new ValidationIssue(nameof(EventProjectDto.TimeZoneId), ValidationRules.InvalidTimeZone));
        }

        MoneyValidation.Currency(nameof(EventProjectDto.Currency), currency, issues);

        return issues;
    }

    /// <summary>
    /// Only IANA ids are stored so that a project created on Windows opens with the same zone on macOS.
    /// </summary>
    /// Windows ids (e.g. "Central Europe Standard Time") also resolve on both platforms, so they are
    /// rejected explicitly via <see cref="TimeZoneInfo.HasIanaId"/>.
    public static bool IsKnownIanaTimeZone(string timeZoneId) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone) && zone.HasIanaId;
}
