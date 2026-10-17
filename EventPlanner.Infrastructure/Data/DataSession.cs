using EventPlanner.Core.Ports;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EventPlanner.Infrastructure.Data;

internal sealed class DataSession(EventPlannerDbContext db) : IDataSession
{
    // SQLITE_CONSTRAINT_UNIQUE, SQLITE_CONSTRAINT_PRIMARYKEY and SQLITE_CONSTRAINT_FOREIGNKEY extended result codes.
    private const int UniqueConstraintViolation = 2067;
    private const int PrimaryKeyConstraintViolation = 1555;
    private const int ForeignKeyConstraintViolation = 787;

    public IEventProjectStore Events { get; } = new EventProjectStore(db);

    public IGuestStore Guests { get; } = new GuestStore(db);

    public IVendorStore Vendors { get; } = new VendorStore(db);

    public IBudgetStore Budget { get; } = new BudgetStore(db);

    public async Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return SaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            // A revision token no longer matched: another window or command committed first.
            return SaveOutcome.Conflict;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException
        {
            SqliteExtendedErrorCode: UniqueConstraintViolation or PrimaryKeyConstraintViolation or ForeignKeyConstraintViolation,
        })
        {
            // A concurrent commit took the same unique key (e.g. a payment operation id), or deleted a
            // record this change refers to (e.g. the project meanwhile deleted): the caller reloads.
            return SaveOutcome.Conflict;
        }
    }

    public ValueTask DisposeAsync() => db.DisposeAsync();
}

/// <summary>Opens sessions against the database of the currently opened profile.</summary>
public sealed class DataSessionFactory(ProfileDatabase database) : IDataSessionFactory
{
    public IDataSession Open() => new DataSession(database.CreateContext());
}
