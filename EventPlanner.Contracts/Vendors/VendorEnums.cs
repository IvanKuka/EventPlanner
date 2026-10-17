namespace EventPlanner.Contracts.Vendors;

/// <summary>
/// Decision state of a quote. Selecting a quote is the organizer's own decision; it never books
/// or confirms anything with the vendor.
/// </summary>
public enum QuoteStatus
{
    Received,
    Selected,
    Declined,
}

/// <summary>
/// How transport is covered by a quote. <see cref="Unknown"/> means the quote does not say;
/// it is never treated as included or as zero cost.
/// </summary>
public enum TransportCoverage
{
    Unknown,
    Included,

    /// <summary>Charged on top of the quote amount; the amount itself may still be unknown.</summary>
    Separate,
    NotNeeded,
}

/// <summary>What selecting a quote does to the budget; the user always chooses explicitly.</summary>
public enum BudgetImpactPolicy
{
    LeaveBudgetUnchanged,
    CreateBudgetItem,

    /// <summary>Sets the committed amount and the quote link of an existing budget item.</summary>
    UpdateBudgetItem,
}

/// <summary>Entry kinds of the quote decision history.</summary>
public enum QuoteDecisionKind
{
    Selected,

    /// <summary>Another quote of the same category was selected, so this one returned to Received.</summary>
    Replaced,
    Declined,
}
