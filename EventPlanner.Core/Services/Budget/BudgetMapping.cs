using EventPlanner.Contracts.Budget;
using EventPlanner.Core.Models.Budget;

namespace EventPlanner.Core.Services.Budget;

internal static class BudgetMapping
{
    public static BudgetItemDto ToDto(this BudgetItem item) => new(
        item.Id,
        item.EventId,
        item.Category,
        item.Title,
        item.EstimatedMinor,
        item.CommittedMinor,
        item.Currency,
        item.SelectedQuoteId,
        item.Revision);

    public static PaymentDto ToDto(this Payment payment) => new(
        payment.Id,
        payment.BudgetItemId,
        payment.OperationId,
        payment.Kind,
        payment.AmountMinor,
        payment.Status,
        payment.PaidLocalDate,
        payment.DueLocalDate,
        payment.Reference,
        payment.Revision);
}
