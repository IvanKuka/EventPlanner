using EventPlanner.Contracts.Results;

namespace EventPlanner.Core.Validation;

/// <summary>
/// Contract validation policy shared by all features (see 15-delivery-matrix.md):
/// names 1–120 chars, short descriptions up to 2000, long notes up to 100 000.
/// User text is never rewritten here; trimming happens only where a feature explicitly decides so.
/// </summary>
public static class TextValidation
{
    public const int NameMaxLength = 120;
    public const int ShortTextMaxLength = 2000;
    public const int LongTextMaxLength = 100_000;

    public static void RequiredName(string field, string? value, List<ValidationIssue> issues, int maxLength = NameMaxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(new ValidationIssue(field, ValidationRules.Required));
            return;
        }

        CheckLength(field, value, maxLength, issues);
        if (ContainsControlCharacter(value, allowLineBreaks: false))
        {
            issues.Add(new ValidationIssue(field, ValidationRules.InvalidCharacters));
        }
    }

    public static void OptionalSingleLine(string field, string? value, List<ValidationIssue> issues, int maxLength = NameMaxLength)
    {
        if (value is null)
        {
            return;
        }

        CheckLength(field, value, maxLength, issues);
        if (ContainsControlCharacter(value, allowLineBreaks: false))
        {
            issues.Add(new ValidationIssue(field, ValidationRules.InvalidCharacters));
        }
    }

    /// <summary>Multi-line text (notes) may contain new lines and tabs, but no NUL or other control characters.</summary>
    public static void OptionalMultiLine(string field, string? value, List<ValidationIssue> issues, int maxLength = LongTextMaxLength)
    {
        if (value is null)
        {
            return;
        }

        CheckLength(field, value, maxLength, issues);
        if (ContainsControlCharacter(value, allowLineBreaks: true))
        {
            issues.Add(new ValidationIssue(field, ValidationRules.InvalidCharacters));
        }
    }

    /// <summary>Empty or whitespace-only optional text is stored as null (unknown), never as an empty value.</summary>
    public static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static void CheckLength(string field, string value, int maxLength, List<ValidationIssue> issues)
    {
        if (value.Length > maxLength)
        {
            issues.Add(new ValidationIssue(field, ValidationRules.TooLong));
        }
    }

    private static bool ContainsControlCharacter(string value, bool allowLineBreaks)
    {
        foreach (var c in value)
        {
            if (allowLineBreaks && c is '\n' or '\r' or '\t')
            {
                continue;
            }

            if (char.IsControl(c))
            {
                return true;
            }
        }

        return false;
    }
}
