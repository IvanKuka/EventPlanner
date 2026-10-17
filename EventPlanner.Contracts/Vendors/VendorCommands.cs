namespace EventPlanner.Contracts.Vendors;

/// <param name="Category">Free label shared with budget items (e.g. "Výzdoba"); quotes are compared per category.</param>
public sealed record CreateVendor(Guid EventId, string Category, string Name, string? Contact, string? Notes);

public sealed record UpdateVendor(Guid VendorId, long ExpectedRevision, string Category, string Name, string? Contact, string? Notes);

/// <summary>Deletes the vendor and its quotes; budget items keep their amounts and only lose the quote link.</summary>
public sealed record DeleteVendor(Guid VendorId, long ExpectedRevision);

/// <param name="ScopeText">What the quote includes, as written by the vendor.</param>
/// <param name="ExclusionsText">Explicitly excluded services; null means "not stated", not "nothing excluded".</param>
/// <param name="TransportMinor">Only for <see cref="TransportCoverage.Separate"/>; null means the transport price is unknown.</param>
/// <param name="PeopleCount">Number of people the quote is calculated for; null when not stated.</param>
public sealed record QuoteInput(
    string Description,
    long AmountMinor,
    string Currency,
    string ScopeText,
    string? ExclusionsText,
    DateOnly? ValidUntilDate,
    TransportCoverage Transport,
    long? TransportMinor,
    int? PeopleCount);

public sealed record AddQuote(Guid VendorId, QuoteInput Quote);

/// <summary>Editing a selected quote never changes the budget; the result carries a warning instead.</summary>
public sealed record UpdateQuote(Guid QuoteId, long ExpectedRevision, QuoteInput Quote);

public sealed record DeleteQuote(Guid QuoteId, long ExpectedRevision);

/// <param name="Note">Optional reason kept in the decision history.</param>
public sealed record DeclineQuote(Guid QuoteId, long ExpectedRevision, string? Note = null);

/// <summary>
/// Marks the quote as the selected one in its category (a previously selected quote in the same
/// category returns to Received). The budget changes only as <paramref name="BudgetImpact"/> says.
/// </summary>
/// <param name="BudgetItemId">Required for <see cref="BudgetImpactPolicy.UpdateBudgetItem"/>.</param>
/// <param name="ExpectedBudgetItemRevision">Required together with <paramref name="BudgetItemId"/>.</param>
/// <param name="Note">Optional reason kept in the decision history.</param>
public sealed record SelectQuote(
    Guid QuoteId,
    long ExpectedRevision,
    BudgetImpactPolicy BudgetImpact,
    Guid? BudgetItemId = null,
    long? ExpectedBudgetItemRevision = null,
    string? Note = null);
