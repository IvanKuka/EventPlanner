using EventPlanner.Core.Models.Budget;
using EventPlanner.Core.Ports;
using Microsoft.EntityFrameworkCore;

namespace EventPlanner.Infrastructure.Data;

internal sealed class BudgetStore(EventPlannerDbContext db) : IBudgetStore
{
    public async Task<IReadOnlyList<BudgetItem>> ListItemsAsync(Guid eventId, CancellationToken cancellationToken) =>
        await db.BudgetItems.AsNoTracking().Include(i => i.Payments).Where(i => i.EventId == eventId).ToListAsync(cancellationToken);

    public Task<BudgetItem?> FindItemAsync(Guid budgetItemId, CancellationToken cancellationToken) =>
        db.BudgetItems.Include(i => i.Payments).FirstOrDefaultAsync(i => i.Id == budgetItemId, cancellationToken);

    public Task<BudgetItem?> FindItemByPaymentAsync(Guid paymentId, CancellationToken cancellationToken) =>
        db.BudgetItems.Include(i => i.Payments).FirstOrDefaultAsync(i => i.Payments.Any(p => p.Id == paymentId), cancellationToken);

    public async Task<IReadOnlyList<BudgetItem>> ListItemsLinkedToQuoteAsync(Guid quoteId, CancellationToken cancellationToken) =>
        await db.BudgetItems.AsNoTracking().Where(i => i.SelectedQuoteId == quoteId).ToListAsync(cancellationToken);

    public Task<Payment?> FindPaymentByOperationAsync(Guid operationId, CancellationToken cancellationToken) =>
        db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.OperationId == operationId, cancellationToken);

    public void AddItem(BudgetItem item) => db.BudgetItems.Add(item);

    public void RemoveItem(BudgetItem item) => db.BudgetItems.Remove(item);
}
