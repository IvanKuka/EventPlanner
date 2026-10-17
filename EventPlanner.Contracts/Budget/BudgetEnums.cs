namespace EventPlanner.Contracts.Budget;

/// <summary>
/// A refund is its own kind with a positive amount; negative amounts are never used as a workaround.
/// </summary>
public enum PaymentKind
{
    Deposit,
    Balance,
    Refund,
}

/// <summary>
/// Whether money actually moved. Only <see cref="Posted"/> counts as paid; due dates are tracked
/// separately and never imply payment.
/// </summary>
public enum PaymentStatus
{
    Planned,
    Posted,

    /// <summary>A planned or mistakenly recorded payment that did not happen; kept for history.</summary>
    Cancelled,
}
