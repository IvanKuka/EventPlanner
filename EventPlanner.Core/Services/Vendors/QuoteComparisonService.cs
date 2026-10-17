using EventPlanner.Contracts.Results;
using EventPlanner.Contracts.Vendors;
using EventPlanner.Core.Models.Budget;
using EventPlanner.Core.Models.Vendors;
using EventPlanner.Core.Ports;
using EventPlanner.Core.Services.Events;
using EventPlanner.Core.Validation;
using EventPlanner.Core.Validation.Vendors;
using Microsoft.Extensions.Logging;

namespace EventPlanner.Core.Services.Vendors;

/// <summary>
/// Compares quotes per category and records the organizer's selection. Selection never books the
/// vendor and changes the budget only according to the explicit <see cref="BudgetImpactPolicy"/>.
/// </summary>
public sealed class QuoteComparisonService(IDataSessionFactory sessions, TimeProvider clock, ILogger<QuoteComparisonService> logger)
{
    private const string EventEntity = "EventProject";
    private const string QuoteEntity = "Quote";
    private const string BudgetItemEntity = "BudgetItem";

    public async Task<IReadOnlyList<string>> ListCategoriesAsync(Guid eventId, CancellationToken cancellationToken)
    {
        await using var session = sessions.Open();
        var vendors = await session.Vendors.ListVendorsAsync(eventId, cancellationToken);
        return [.. vendors
            .Select(v => v.Category)
            .Distinct(QuoteComparison.CategoryComparer)
            .Order(StringComparer.CurrentCulture)];
    }

    public async Task<Result<QuoteComparisonDto>> CompareQuotesAsync(Guid eventId, string category, CancellationToken cancellationToken)
    {
        await using var session = sessions.Open();
        var project = await session.Events.FindAsync(eventId, cancellationToken);
        if (project is null)
        {
            return OperationError.NotFound(EventEntity);
        }

        var vendors = await session.Vendors.ListVendorsAsync(eventId, cancellationToken);
        var quotes = await session.Vendors.ListQuotesAsync(eventId, cancellationToken);
        return QuoteComparison.Build(category, project.Currency, vendors, quotes);
    }

    /// <summary>
    /// Selects the quote; a previously selected quote of the same category returns to Received.
    /// Budget impact: <see cref="BudgetImpactPolicy.CreateBudgetItem"/> creates an item in the quote's
    /// own currency (a foreign currency stays outside the project totals);
    /// <see cref="BudgetImpactPolicy.UpdateBudgetItem"/> requires the same currency. Everything is one transaction.
    /// </summary>
    public async Task<Result<QuoteSelectionDto>> SelectQuoteAsync(SelectQuote command, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(command.BudgetImpact))
        {
            return OperationError.Validation(nameof(command.BudgetImpact), ValidationRules.InvalidValue);
        }

        if (command.BudgetImpact == BudgetImpactPolicy.UpdateBudgetItem
            && (command.BudgetItemId is null || command.ExpectedBudgetItemRevision is null))
        {
            return OperationError.Validation(nameof(command.BudgetItemId), ValidationRules.Required);
        }

        var note = TextValidation.NullIfBlank(command.Note)?.Trim();
        var noteIssues = VendorValidator.ValidateDecisionNote(note);
        if (noteIssues.Count > 0)
        {
            return OperationError.Validation(noteIssues);
        }

        await using var session = sessions.Open();
        var (quote, error) = await VendorService.FindWritableQuoteAsync(session, command.QuoteId, command.ExpectedRevision, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var vendor = (await session.Vendors.FindVendorAsync(quote!.VendorId, cancellationToken))!;
        var project = (await WritableEvent.FindAsync(session, vendor.EventId, cancellationToken)).Value;

        var warnings = new List<Warning>();
        var committedMinor = quote.KnownTotalMinor ?? quote.AmountMinor;
        if (command.BudgetImpact != BudgetImpactPolicy.LeaveBudgetUnchanged && quote.KnownTotalMinor is null)
        {
            warnings.Add(new Warning(WarningCodes.QuoteTotalIncomplete));
        }

        Guid? budgetItemId = null;
        switch (command.BudgetImpact)
        {
            case BudgetImpactPolicy.CreateBudgetItem:
                var created = BudgetItem.Create(Guid.CreateVersion7(), project.Id, vendor.Category, vendor.Name, null, null, quote.Currency);
                created.CommitToQuote(quote.Id, committedMinor);
                session.Budget.AddItem(created);
                budgetItemId = created.Id;
                break;

            case BudgetImpactPolicy.UpdateBudgetItem:
                var item = await session.Budget.FindItemAsync(command.BudgetItemId!.Value, cancellationToken);
                if (item is null || item.EventId != project.Id)
                {
                    // Cross-event links are forbidden.
                    return OperationError.Validation(nameof(command.BudgetItemId), ValidationRules.UnknownReference);
                }

                if (item.Revision != command.ExpectedBudgetItemRevision)
                {
                    return OperationError.Conflict(BudgetItemEntity);
                }

                if (!string.Equals(item.Currency, quote.Currency, StringComparison.Ordinal))
                {
                    return OperationError.Validation(nameof(command.BudgetItemId), ValidationRules.CurrencyMismatch);
                }

                item.CommitToQuote(quote.Id, committedMinor);
                budgetItemId = item.Id;
                break;
        }

        if (budgetItemId is not null && !string.Equals(quote.Currency, project.Currency, StringComparison.Ordinal))
        {
            warnings.Add(new Warning(WarningCodes.ForeignCurrencyNotInTotals));
        }

        var decidedUtc = clock.GetUtcNow();
        await UnselectOthersInCategoryAsync(session, project.Id, vendor.Category, quote.Id, decidedUtc, cancellationToken);
        quote.Select();
        session.Vendors.AddDecision(QuoteDecision.Record(quote, QuoteDecisionKind.Selected, decidedUtc, command.BudgetImpact, note));

        if (await session.SaveChangesAsync(cancellationToken) == SaveOutcome.Conflict)
        {
            return OperationError.Conflict(QuoteEntity);
        }

        logger.LogInformation("Quote {QuoteId} selected with budget impact {Policy}", quote.Id, command.BudgetImpact);
        return Result<QuoteSelectionDto>.Success(new QuoteSelectionDto(quote.ToDto(), budgetItemId), warnings);
    }

    private static async Task UnselectOthersInCategoryAsync(
        IDataSession session, Guid eventId, string category, Guid selectedQuoteId, DateTimeOffset decidedUtc, CancellationToken cancellationToken)
    {
        var vendorIds = (await session.Vendors.ListVendorsAsync(eventId, cancellationToken))
            .Where(v => QuoteComparison.CategoryComparer.Equals(v.Category, category))
            .Select(v => v.Id)
            .ToList();
        foreach (var other in await session.Vendors.ListQuotesOfVendorsAsync(vendorIds, cancellationToken))
        {
            if (other.Id != selectedQuoteId && other.Unselect())
            {
                session.Vendors.AddDecision(QuoteDecision.Record(other, QuoteDecisionKind.Replaced, decidedUtc, null, null));
            }
        }
    }
}
