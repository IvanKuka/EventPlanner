using EventPlanner.Contracts.Budget;
using EventPlanner.Contracts.Results;
using EventPlanner.Core.Models.Budget;

namespace EventPlanner.Core.Validation.Budget;

public static class BudgetValidator
{
    public const int ReferenceMaxLength = 200;

    public static List<ValidationIssue> ValidateItem(string? category, string? title, long? estimatedMinor, long? committedMinor)
    {
        var issues = new List<ValidationIssue>();
        TextValidation.RequiredName(nameof(BudgetItemDto.Category), category, issues);
        TextValidation.RequiredName(nameof(BudgetItemDto.Title), title, issues);
        MoneyValidation.Amount(nameof(BudgetItemDto.EstimatedMinor), estimatedMinor, issues, allowZero: true);
        MoneyValidation.Amount(nameof(BudgetItemDto.CommittedMinor), committedMinor, issues, allowZero: true);
        return issues;
    }

    public static List<ValidationIssue> ValidatePayment(RecordPayment command, string? reference)
    {
        var issues = new List<ValidationIssue>();
        if (command.OperationId == Guid.Empty)
        {
            issues.Add(new ValidationIssue(nameof(PaymentDto.OperationId), ValidationRules.Required));
        }

        if (!Enum.IsDefined(command.Kind))
        {
            issues.Add(new ValidationIssue(nameof(PaymentDto.Kind), ValidationRules.InvalidValue));
        }

        MoneyValidation.Amount(nameof(PaymentDto.AmountMinor), command.AmountMinor, issues, allowZero: false);
        TextValidation.OptionalSingleLine(nameof(PaymentDto.Reference), reference, issues, ReferenceMaxLength);

        switch (command.Status)
        {
            case PaymentStatus.Posted when command.PaidLocalDate is null:
                issues.Add(new ValidationIssue(nameof(PaymentDto.PaidLocalDate), ValidationRules.Required));
                break;
            case PaymentStatus.Planned when command.PaidLocalDate is not null:
                issues.Add(new ValidationIssue(nameof(PaymentDto.PaidLocalDate), ValidationRules.NotAllowedForStatus));
                break;

            // A refund is recorded when the money came back; an expected refund is not money the organizer has.
            case PaymentStatus.Planned when command.Kind == PaymentKind.Refund:
                issues.Add(new ValidationIssue(nameof(PaymentDto.Status), ValidationRules.NotAllowedForStatus));
                break;
            case PaymentStatus.Posted or PaymentStatus.Planned:
                break;
            default:
                issues.Add(new ValidationIssue(nameof(PaymentDto.Status), ValidationRules.InvalidValue));
                break;
        }

        return issues;
    }

    /// <summary>Net paid of an item may never become negative: refunds cannot exceed posted payments.</summary>
    public static bool KeepsNetPaidNonNegative(BudgetItem item, long addedPaymentsMinor, long addedRefundsMinor) =>
        item.PostedPaymentsMinor + addedPaymentsMinor >= item.PostedRefundsMinor + addedRefundsMinor;
}
