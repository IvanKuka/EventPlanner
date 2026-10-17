using EventPlanner.Contracts.Results;
using EventPlanner.Contracts.Vendors;

namespace EventPlanner.Core.Validation.Vendors;

public static class VendorValidator
{
    public const int MaxPeopleCount = 100_000;

    public static List<ValidationIssue> ValidateVendor(string? category, string? name, string? contact, string? notes)
    {
        var issues = new List<ValidationIssue>();
        TextValidation.RequiredName(nameof(VendorDto.Category), category, issues);
        TextValidation.RequiredName(nameof(VendorDto.Name), name, issues);
        TextValidation.OptionalMultiLine(nameof(VendorDto.Contact), contact, issues, TextValidation.ShortTextMaxLength);
        TextValidation.OptionalMultiLine(nameof(VendorDto.Notes), notes, issues, TextValidation.ShortTextMaxLength);
        return issues;
    }

    public static List<ValidationIssue> ValidateDecisionNote(string? note)
    {
        var issues = new List<ValidationIssue>();
        TextValidation.OptionalMultiLine(nameof(QuoteDecisionDto.Note), note, issues, TextValidation.ShortTextMaxLength);
        return issues;
    }

    public static List<ValidationIssue> ValidateQuote(QuoteInput quote)
    {
        var issues = new List<ValidationIssue>();
        TextValidation.RequiredName(nameof(QuoteDto.Description), quote.Description, issues);
        MoneyValidation.Amount(nameof(QuoteDto.AmountMinor), quote.AmountMinor, issues, allowZero: true);
        MoneyValidation.Currency(nameof(QuoteDto.Currency), quote.Currency, issues);

        // The scope is what makes quotes comparable, so it is required; exclusions stay optional (unknown).
        if (string.IsNullOrWhiteSpace(quote.ScopeText))
        {
            issues.Add(new ValidationIssue(nameof(QuoteDto.ScopeText), ValidationRules.Required));
        }
        else
        {
            TextValidation.OptionalMultiLine(nameof(QuoteDto.ScopeText), quote.ScopeText, issues, TextValidation.ShortTextMaxLength);
        }

        TextValidation.OptionalMultiLine(nameof(QuoteDto.ExclusionsText), quote.ExclusionsText, issues, TextValidation.ShortTextMaxLength);

        if (!Enum.IsDefined(quote.Transport))
        {
            issues.Add(new ValidationIssue(nameof(QuoteDto.Transport), ValidationRules.InvalidValue));
        }

        MoneyValidation.Amount(nameof(QuoteDto.TransportMinor), quote.TransportMinor, issues, allowZero: true);
        if (quote.PeopleCount is < 1 or > MaxPeopleCount)
        {
            issues.Add(new ValidationIssue(nameof(QuoteDto.PeopleCount), ValidationRules.InvalidValue));
        }

        return issues;
    }
}
