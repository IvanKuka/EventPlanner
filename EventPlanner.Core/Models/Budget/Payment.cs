using EventPlanner.Contracts.Budget;

namespace EventPlanner.Core.Models.Budget;

/// <summary>
/// A payment or refund of a budget item, always with a positive amount. <see cref="OperationId"/>
/// is unique and makes recording idempotent. Changed only through its <see cref="BudgetItem"/>.
/// </summary>
public sealed class Payment
{
    private Payment()
    {
    }

    internal Payment(Guid id, Guid budgetItemId, Guid operationId, PaymentKind kind, long amountMinor, PaymentStatus status, DateOnly? paidLocalDate, DateOnly? dueLocalDate, string? reference)
    {
        Id = id;
        BudgetItemId = budgetItemId;
        OperationId = operationId;
        Kind = kind;
        AmountMinor = amountMinor;
        Status = status;
        PaidLocalDate = paidLocalDate;
        DueLocalDate = dueLocalDate;
        Reference = reference;
        Revision = 1;
    }

    public Guid Id { get; private set; }

    public Guid BudgetItemId { get; private set; }

    public Guid OperationId { get; private set; }

    public PaymentKind Kind { get; private set; }

    public long AmountMinor { get; private set; }

    public PaymentStatus Status { get; private set; }

    public DateOnly? PaidLocalDate { get; private set; }

    public DateOnly? DueLocalDate { get; private set; }

    public string? Reference { get; private set; }

    public long Revision { get; private set; }

    /// <summary>
    /// True when a replayed command carries the same payment as the one already recorded. Status and
    /// paid date are not compared: a replay may arrive after the payment was posted or cancelled.
    /// </summary>
    public bool Matches(Guid budgetItemId, PaymentKind kind, long amountMinor, DateOnly? dueLocalDate, string? reference) =>
        BudgetItemId == budgetItemId && Kind == kind && AmountMinor == amountMinor && DueLocalDate == dueLocalDate
        && string.Equals(Reference, reference, StringComparison.Ordinal);

    internal void Post(DateOnly paidLocalDate)
    {
        if (Status != PaymentStatus.Planned)
        {
            throw new InvalidOperationException($"Only a planned payment can be posted (was {Status}).");
        }

        Status = PaymentStatus.Posted;
        PaidLocalDate = paidLocalDate;
        Revision++;
    }

    internal void Cancel()
    {
        if (Status == PaymentStatus.Cancelled)
        {
            throw new InvalidOperationException("Payment is already cancelled.");
        }

        Status = PaymentStatus.Cancelled;
        Revision++;
    }
}
