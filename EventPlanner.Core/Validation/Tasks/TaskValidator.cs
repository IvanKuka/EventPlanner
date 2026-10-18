using EventPlanner.Contracts.Results;
using EventPlanner.Contracts.Tasks;

namespace EventPlanner.Core.Validation.Tasks;

public static class TaskValidator
{
    public static List<ValidationIssue> Validate(TaskInput input)
    {
        var issues = new List<ValidationIssue>();
        TextValidation.RequiredName(nameof(TaskDto.Title), input.Title, issues);
        TextValidation.OptionalSingleLine(nameof(TaskDto.OwnerLabel), input.OwnerLabel, issues);
        TextValidation.OptionalMultiLine(nameof(TaskDto.Notes), input.Notes, issues, TextValidation.ShortTextMaxLength);
        if (!Enum.IsDefined(input.Priority))
        {
            issues.Add(new ValidationIssue(nameof(TaskDto.Priority), ValidationRules.InvalidValue));
        }

        return issues;
    }
}
