using EventPlanner.Contracts.Guests;
using EventPlanner.Contracts.Results;

namespace EventPlanner.Core.Validation.Guests;

public static class GuestValidator
{
    public static List<ValidationIssue> ValidateGuest(string? displayName, GuestKind kind, string? ageCategory, RsvpState rsvpState, string? privateNote)
    {
        var issues = new List<ValidationIssue>();
        TextValidation.RequiredName(nameof(GuestDto.DisplayName), displayName, issues);
        TextValidation.OptionalSingleLine(nameof(GuestDto.AgeCategory), ageCategory, issues);
        TextValidation.OptionalMultiLine(nameof(GuestDetailDto.PrivateNote), privateNote, issues);

        if (!Enum.IsDefined(kind))
        {
            issues.Add(new ValidationIssue(nameof(GuestDto.Kind), ValidationRules.InvalidValue));
        }

        if (!Enum.IsDefined(rsvpState))
        {
            issues.Add(new ValidationIssue(nameof(GuestDto.RsvpState), ValidationRules.InvalidValue));
        }

        return issues;
    }

    public static List<ValidationIssue> ValidateGroup(string? label, string? contactName, string? contactEmail, string? phone, string? notes)
    {
        var issues = new List<ValidationIssue>();
        TextValidation.RequiredName(nameof(GroupDto.Label), label, issues);
        TextValidation.OptionalSingleLine(nameof(GroupDto.ContactName), contactName, issues);
        TextValidation.OptionalSingleLine(nameof(GroupDto.ContactEmail), contactEmail, issues, 254);
        TextValidation.OptionalSingleLine(nameof(GroupDto.Phone), phone, issues, 40);
        TextValidation.OptionalMultiLine(nameof(GroupDto.Notes), notes, issues, TextValidation.ShortTextMaxLength);
        return issues;
    }
}
