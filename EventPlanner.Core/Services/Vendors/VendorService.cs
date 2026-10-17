using EventPlanner.Contracts.Results;
using EventPlanner.Contracts.Vendors;
using EventPlanner.Core.Models.Vendors;
using EventPlanner.Core.Ports;
using EventPlanner.Core.Services.Events;
using EventPlanner.Core.Validation;
using EventPlanner.Core.Validation.Vendors;

namespace EventPlanner.Core.Services.Vendors;

/// <summary>Vendors and their quotes. Selecting a quote is in <see cref="QuoteComparisonService"/>.</summary>
public sealed class VendorService(IDataSessionFactory sessions, TimeProvider clock)
{
    private const string VendorEntity = "Vendor";
    private const string QuoteEntity = "Quote";

    public async Task<IReadOnlyList<VendorDto>> ListVendorsAsync(Guid eventId, CancellationToken cancellationToken)
    {
        await using var session = sessions.Open();
        var vendors = await session.Vendors.ListVendorsAsync(eventId, cancellationToken);
        return [.. vendors
            .OrderBy(v => v.Category, StringComparer.CurrentCulture)
            .ThenBy(v => v.Name, StringComparer.CurrentCulture)
            .Select(v => v.ToDto())];
    }

    public async Task<IReadOnlyList<QuoteDto>> ListQuotesAsync(Guid eventId, CancellationToken cancellationToken)
    {
        await using var session = sessions.Open();
        var quotes = await session.Vendors.ListQuotesAsync(eventId, cancellationToken);
        return [.. quotes.Select(q => q.ToDto())];
    }

    /// <summary>Decisions about the vendor's quotes, newest first.</summary>
    public async Task<IReadOnlyList<QuoteDecisionDto>> ListDecisionsAsync(Guid vendorId, CancellationToken cancellationToken)
    {
        await using var session = sessions.Open();
        var decisions = await session.Vendors.ListDecisionsOfVendorAsync(vendorId, cancellationToken);
        if (decisions.Count == 0)
        {
            return [];
        }

        var vendor = await session.Vendors.FindVendorAsync(vendorId, cancellationToken);
        var descriptions = (await session.Vendors.ListQuotesAsync(vendor!.EventId, cancellationToken))
            .ToDictionary(q => q.Id, q => q.Description);

        // Ordered in memory: SQLite cannot order DateTimeOffset. One selection writes Selected and Replaced
        // with the same time; Selected is listed first.
        return [.. decisions
            .OrderByDescending(d => d.DecidedUtc)
            .ThenBy(d => d.Kind)
            .Select(d => d.ToDto(descriptions[d.QuoteId]))];
    }

    public async Task<Result<VendorDto>> CreateVendorAsync(CreateVendor command, CancellationToken cancellationToken)
    {
        var (contact, notes) = (TextValidation.NullIfBlank(command.Contact), TextValidation.NullIfBlank(command.Notes));
        var issues = VendorValidator.ValidateVendor(command.Category, command.Name, contact, notes);
        if (issues.Count > 0)
        {
            return OperationError.Validation(issues);
        }

        await using var session = sessions.Open();
        var writable = await WritableEvent.FindAsync(session, command.EventId, cancellationToken);
        if (writable.Error is not null)
        {
            return writable.Error;
        }

        var vendor = Vendor.Create(Guid.CreateVersion7(), command.EventId, command.Category, command.Name, contact, notes);
        session.Vendors.AddVendor(vendor);
        if (await session.SaveChangesAsync(cancellationToken) == SaveOutcome.Conflict)
        {
            return OperationError.Conflict(VendorEntity);
        }
        return vendor.ToDto();
    }

    public async Task<Result<VendorDto>> UpdateVendorAsync(UpdateVendor command, CancellationToken cancellationToken)
    {
        var (contact, notes) = (TextValidation.NullIfBlank(command.Contact), TextValidation.NullIfBlank(command.Notes));
        var issues = VendorValidator.ValidateVendor(command.Category, command.Name, contact, notes);
        if (issues.Count > 0)
        {
            return OperationError.Validation(issues);
        }

        await using var session = sessions.Open();
        var (vendor, error) = await FindWritableVendorAsync(session, command.VendorId, command.ExpectedRevision, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        vendor!.Update(command.Category, command.Name, contact, notes);
        if (await session.SaveChangesAsync(cancellationToken) == SaveOutcome.Conflict)
        {
            return OperationError.Conflict(VendorEntity);
        }

        return vendor.ToDto();
    }

    public async Task<Result<Guid>> DeleteVendorAsync(DeleteVendor command, CancellationToken cancellationToken)
    {
        await using var session = sessions.Open();
        var (vendor, error) = await FindWritableVendorAsync(session, command.VendorId, command.ExpectedRevision, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        // Quotes cascade in the store; budget items keep their amounts and lose only the quote link (SET NULL).
        session.Vendors.RemoveVendor(vendor!);
        if (await session.SaveChangesAsync(cancellationToken) == SaveOutcome.Conflict)
        {
            return OperationError.Conflict(VendorEntity);
        }

        return vendor!.Id;
    }

    public async Task<Result<QuoteDto>> AddQuoteAsync(AddQuote command, CancellationToken cancellationToken)
    {
        var input = Normalize(command.Quote);
        var issues = VendorValidator.ValidateQuote(input);
        if (issues.Count > 0)
        {
            return OperationError.Validation(issues);
        }

        await using var session = sessions.Open();
        var vendor = await session.Vendors.FindVendorAsync(command.VendorId, cancellationToken);
        if (vendor is null)
        {
            return OperationError.NotFound(VendorEntity);
        }

        var writable = await WritableEvent.FindAsync(session, vendor.EventId, cancellationToken);
        if (writable.Error is not null)
        {
            return writable.Error;
        }

        var quote = Quote.Create(Guid.CreateVersion7(), vendor.Id, input);
        session.Vendors.AddQuote(quote);
        if (await session.SaveChangesAsync(cancellationToken) == SaveOutcome.Conflict)
        {
            return OperationError.Conflict(QuoteEntity);
        }
        return quote.ToDto();
    }

    /// <summary>
    /// Edits a quote. If budget items are linked to it, they stay unchanged (the user decides via
    /// SelectQuote) and the result carries <see cref="WarningCodes.LinkedBudgetNotUpdated"/>.
    /// </summary>
    public async Task<Result<QuoteDto>> UpdateQuoteAsync(UpdateQuote command, CancellationToken cancellationToken)
    {
        var input = Normalize(command.Quote);
        var issues = VendorValidator.ValidateQuote(input);
        if (issues.Count > 0)
        {
            return OperationError.Validation(issues);
        }

        await using var session = sessions.Open();
        var (quote, error) = await FindWritableQuoteAsync(session, command.QuoteId, command.ExpectedRevision, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        quote!.Update(input);
        var linked = await session.Budget.ListItemsLinkedToQuoteAsync(quote.Id, cancellationToken);
        var warnings = linked.Any(i => i.CommittedMinor != (quote.KnownTotalMinor ?? quote.AmountMinor) || i.Currency != quote.Currency)
            ? [new Warning(WarningCodes.LinkedBudgetNotUpdated)]
            : Array.Empty<Warning>();

        if (await session.SaveChangesAsync(cancellationToken) == SaveOutcome.Conflict)
        {
            return OperationError.Conflict(QuoteEntity);
        }

        return Result<QuoteDto>.Success(quote.ToDto(), warnings);
    }

    public async Task<Result<Guid>> DeleteQuoteAsync(DeleteQuote command, CancellationToken cancellationToken)
    {
        await using var session = sessions.Open();
        var (quote, error) = await FindWritableQuoteAsync(session, command.QuoteId, command.ExpectedRevision, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        session.Vendors.RemoveQuote(quote!);
        if (await session.SaveChangesAsync(cancellationToken) == SaveOutcome.Conflict)
        {
            return OperationError.Conflict(QuoteEntity);
        }

        return quote!.Id;
    }

    /// <summary>Declining keeps the quote for history; it is no longer a candidate for the lowest price.</summary>
    public async Task<Result<QuoteDto>> DeclineQuoteAsync(DeclineQuote command, CancellationToken cancellationToken)
    {
        var note = TextValidation.NullIfBlank(command.Note)?.Trim();
        var issues = VendorValidator.ValidateDecisionNote(note);
        if (issues.Count > 0)
        {
            return OperationError.Validation(issues);
        }

        await using var session = sessions.Open();
        var (quote, error) = await FindWritableQuoteAsync(session, command.QuoteId, command.ExpectedRevision, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        // Declining an already declined quote is not a new decision.
        if (quote!.Status != QuoteStatus.Declined)
        {
            quote.Decline();
            session.Vendors.AddDecision(QuoteDecision.Record(quote, QuoteDecisionKind.Declined, clock.GetUtcNow(), null, note));
        }

        if (await session.SaveChangesAsync(cancellationToken) == SaveOutcome.Conflict)
        {
            return OperationError.Conflict(QuoteEntity);
        }

        return quote.ToDto();
    }

    private static async Task<(Vendor? Vendor, OperationError? Error)> FindWritableVendorAsync(
        IDataSession session, Guid vendorId, long expectedRevision, CancellationToken cancellationToken)
    {
        var vendor = await session.Vendors.FindVendorAsync(vendorId, cancellationToken);
        if (vendor is null)
        {
            return (null, OperationError.NotFound(VendorEntity));
        }

        var writable = await WritableEvent.FindAsync(session, vendor.EventId, cancellationToken);
        if (writable.Error is not null)
        {
            return (null, writable.Error);
        }

        return vendor.Revision != expectedRevision
            ? (null, OperationError.Conflict(VendorEntity))
            : (vendor, null);
    }

    internal static async Task<(Quote? Quote, OperationError? Error)> FindWritableQuoteAsync(
        IDataSession session, Guid quoteId, long expectedRevision, CancellationToken cancellationToken)
    {
        var quote = await session.Vendors.FindQuoteAsync(quoteId, cancellationToken);
        if (quote is null)
        {
            return (null, OperationError.NotFound(QuoteEntity));
        }

        var vendor = await session.Vendors.FindVendorAsync(quote.VendorId, cancellationToken);
        if (vendor is null)
        {
            return (null, OperationError.NotFound(VendorEntity));
        }

        var writable = await WritableEvent.FindAsync(session, vendor.EventId, cancellationToken);
        if (writable.Error is not null)
        {
            return (null, writable.Error);
        }

        return quote.Revision != expectedRevision
            ? (null, OperationError.Conflict(QuoteEntity))
            : (quote, null);
    }

    // Blank optional text is "not stated" (null), never an empty value that could read as "none".
    private static QuoteInput Normalize(QuoteInput input) => input with
    {
        ExclusionsText = TextValidation.NullIfBlank(input.ExclusionsText),
        Currency = input.Currency?.Trim().ToUpperInvariant() ?? string.Empty,
    };
}
