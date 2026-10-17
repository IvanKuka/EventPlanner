namespace EventPlanner.Contracts.Budget;

/// <param name="EstimatedMinor">Planned amount; null means not estimated yet (shown as missing, not as zero).</param>
/// <param name="CommittedMinor">Agreed amount; null means nothing agreed yet.</param>
public sealed record CreateBudgetItem(Guid EventId, string Category, string Title, long? EstimatedMinor, long? CommittedMinor, string Currency);

/// <remarks>The currency is fixed at creation so recorded payments keep their meaning.</remarks>
public sealed record UpdateBudgetItem(Guid BudgetItemId, long ExpectedRevision, string Category, string Title, long? EstimatedMinor, long? CommittedMinor);

/// <summary>Refused while the item has posted payments; cancel or refund them first.</summary>
public sealed record DeleteBudgetItem(Guid BudgetItemId, long ExpectedRevision);

/// <summary>
/// Records one payment. <paramref name="OperationId"/> is created once per user intent (e.g. when the
/// payment form opens); repeating the command with the same id returns the already recorded payment
/// instead of a duplicate. Same date and amount are not treated as duplicates.
/// </summary>
/// <param name="PaidLocalDate">Required for <see cref="PaymentStatus.Posted"/>, forbidden for planned payments.</param>
public sealed record RecordPayment(
    Guid BudgetItemId,
    Guid OperationId,
    PaymentKind Kind,
    long AmountMinor,
    PaymentStatus Status,
    DateOnly? PaidLocalDate,
    DateOnly? DueLocalDate,
    string? Reference);

/// <summary>Turns a planned payment into a posted one, so it is never counted twice.</summary>
public sealed record PostPayment(Guid PaymentId, long ExpectedRevision, DateOnly PaidLocalDate);

public sealed record CancelPayment(Guid PaymentId, long ExpectedRevision);
