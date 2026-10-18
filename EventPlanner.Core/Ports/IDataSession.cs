using EventPlanner.Core.Models.Budget;
using EventPlanner.Core.Models.Events;
using EventPlanner.Core.Models.Guests;
using EventPlanner.Core.Models.Tasks;
using EventPlanner.Core.Models.Vendors;

namespace EventPlanner.Core.Ports;

/// <summary>
/// Persistence boundary for one short unit of work (one command or query). Entities loaded from a
/// session are tracked; <see cref="SaveChangesAsync"/> commits them atomically and verifies
/// optimistic revisions in the store, not only in the UI.
/// </summary>
public interface IDataSession : IAsyncDisposable
{
    IEventProjectStore Events { get; }

    IGuestStore Guests { get; }

    IVendorStore Vendors { get; }

    IBudgetStore Budget { get; }

    ITaskStore Tasks { get; }

    Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IDataSessionFactory
{
    IDataSession Open();
}

public enum SaveOutcome
{
    Saved,

    /// <summary>
    /// A revision changed between load and commit, or a concurrent commit took a unique key
    /// (e.g. a payment operationId), or a referenced record was deleted meanwhile; nothing was written.
    /// </summary>
    Conflict,
}

public interface IEventProjectStore
{
    Task<IReadOnlyList<EventProject>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Loads the project with its program parts.</summary>
    Task<EventProject?> FindAsync(Guid id, CancellationToken cancellationToken);

    void Add(EventProject project);

    /// <summary>Removes the project; the database cascades to everything the project owns.</summary>
    void Remove(EventProject project);
}

public interface IGuestStore
{
    Task<IReadOnlyList<Guest>> ListGuestsAsync(Guid eventId, CancellationToken cancellationToken);

    Task<Guest?> FindGuestAsync(Guid guestId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guest>> ListGroupMembersAsync(Guid groupId, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListGuestNamesAsync(Guid eventId, Guid? excludeGuestId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ParticipantGroup>> ListGroupsAsync(Guid eventId, CancellationToken cancellationToken);

    Task<ParticipantGroup?> FindGroupAsync(Guid groupId, CancellationToken cancellationToken);

    void AddGuest(Guest guest);

    void RemoveGuest(Guest guest);

    void AddGroup(ParticipantGroup group);

    void RemoveGroup(ParticipantGroup group);
}

public interface IVendorStore
{
    Task<IReadOnlyList<Vendor>> ListVendorsAsync(Guid eventId, CancellationToken cancellationToken);

    Task<Vendor?> FindVendorAsync(Guid vendorId, CancellationToken cancellationToken);

    /// <summary>All quotes of the event's vendors, untracked (for queries).</summary>
    Task<IReadOnlyList<Quote>> ListQuotesAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>Tracked quotes of the given vendors (for changes across one category).</summary>
    Task<IReadOnlyList<Quote>> ListQuotesOfVendorsAsync(IReadOnlyCollection<Guid> vendorIds, CancellationToken cancellationToken);

    Task<Quote?> FindQuoteAsync(Guid quoteId, CancellationToken cancellationToken);

    void AddVendor(Vendor vendor);

    void RemoveVendor(Vendor vendor);

    void AddQuote(Quote quote);

    void RemoveQuote(Quote quote);

    /// <summary>Decision history of all quotes of the vendor, untracked.</summary>
    Task<IReadOnlyList<QuoteDecision>> ListDecisionsOfVendorAsync(Guid vendorId, CancellationToken cancellationToken);

    void AddDecision(QuoteDecision decision);
}

public interface IBudgetStore
{
    /// <summary>Budget items of the event with their payments, untracked.</summary>
    Task<IReadOnlyList<BudgetItem>> ListItemsAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>Tracked item with its payments.</summary>
    Task<BudgetItem?> FindItemAsync(Guid budgetItemId, CancellationToken cancellationToken);

    /// <summary>Tracked item that owns the payment, with all its payments.</summary>
    Task<BudgetItem?> FindItemByPaymentAsync(Guid paymentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<BudgetItem>> ListItemsLinkedToQuoteAsync(Guid quoteId, CancellationToken cancellationToken);

    Task<Payment?> FindPaymentByOperationAsync(Guid operationId, CancellationToken cancellationToken);

    void AddItem(BudgetItem item);

    void RemoveItem(BudgetItem item);
}

public interface ITaskStore
{
    /// <summary>Tasks of the event, untracked.</summary>
    Task<IReadOnlyList<EventTask>> ListAsync(Guid eventId, CancellationToken cancellationToken);

    Task<EventTask?> FindAsync(Guid taskId, CancellationToken cancellationToken);

    void Add(EventTask task);

    /// <summary>Removes the task; tasks that followed it lose only the dependency (SET NULL).</summary>
    void Remove(EventTask task);
}
