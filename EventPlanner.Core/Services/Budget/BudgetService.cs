using EventPlanner.Contracts.Budget;
using EventPlanner.Contracts.Results;
using EventPlanner.Core.Models.Budget;
using EventPlanner.Core.Ports;
using EventPlanner.Core.Services.Events;
using EventPlanner.Core.Validation;
using EventPlanner.Core.Validation.Budget;
using Microsoft.Extensions.Logging;

namespace EventPlanner.Core.Services.Budget;

/// <summary>Budget items and payments of one event; totals are always derived from the records.</summary>
public sealed class BudgetService(IDataSessionFactory sessions, ILogger<BudgetService> logger)
{
    private const string EventEntity = "EventProject";
    private const string BudgetItemEntity = "BudgetItem";
    private const string PaymentEntity = "Payment";

    public async Task<Result<BudgetOverviewDto>> GetOverviewAsync(Guid eventId, CancellationToken cancellationToken)
    {
        await using var session = sessions.Open();
        var project = await session.Events.FindAsync(eventId, cancellationToken);
        if (project is null)
        {
            return OperationError.NotFound(EventEntity);
        }

        var items = await session.Budget.ListItemsAsync(eventId, cancellationToken);
        return BudgetCalculator.Overview(project.Currency, items);
    }

    public async Task<Result<BudgetItemDto>> CreateItemAsync(CreateBudgetItem command, CancellationToken cancellationToken)
    {
        var currency = command.Currency?.Trim().ToUpperInvariant();
        var issues = BudgetValidator.ValidateItem(command.Category, command.Title, command.EstimatedMinor, command.CommittedMinor);
        MoneyValidation.Currency(nameof(BudgetItemDto.Currency), currency, issues);
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

        var item = BudgetItem.Create(Guid.CreateVersion7(), command.EventId, command.Category, command.Title, command.EstimatedMinor, command.CommittedMinor, currency!);
        session.Budget.AddItem(item);
        await session.SaveChangesAsync(cancellationToken);

        IReadOnlyList<Warning> warnings = string.Equals(item.Currency, writable.Value.Currency, StringComparison.Ordinal)
            ? []
            : [new Warning(WarningCodes.ForeignCurrencyNotInTotals, nameof(BudgetItemDto.Currency))];
        return Result<BudgetItemDto>.Success(item.ToDto(), warnings);
    }

    public async Task<Result<BudgetItemDto>> UpdateItemAsync(UpdateBudgetItem command, CancellationToken cancellationToken)
    {
        var issues = BudgetValidator.ValidateItem(command.Category, command.Title, command.EstimatedMinor, command.CommittedMinor);
        if (issues.Count > 0)
        {
            return OperationError.Validation(issues);
        }

        await using var session = sessions.Open();
        var (item, error) = await FindWritableItemAsync(session, command.BudgetItemId, command.ExpectedRevision, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        item!.Update(command.Category, command.Title, command.EstimatedMinor, command.CommittedMinor);
        if (await session.SaveChangesAsync(cancellationToken) == SaveOutcome.Conflict)
        {
            return OperationError.Conflict(BudgetItemEntity);
        }

        return item.ToDto();
    }

    public async Task<Result<Guid>> DeleteItemAsync(DeleteBudgetItem command, CancellationToken cancellationToken)
    {
        await using var session = sessions.Open();
        var (item, error) = await FindWritableItemAsync(session, command.BudgetItemId, command.ExpectedRevision, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        // Money that actually moved must not disappear with a deleted line.
        if (item!.Payments.Any(p => p.Status == PaymentStatus.Posted))
        {
            return OperationError.Validation(nameof(BudgetItemDto.Id), ValidationRules.HasPostedPayments);
        }

        session.Budget.RemoveItem(item);
        if (await session.SaveChangesAsync(cancellationToken) == SaveOutcome.Conflict)
        {
            return OperationError.Conflict(BudgetItemEntity);
        }

        return item.Id;
    }

    /// <summary>
    /// Records a payment idempotently by <see cref="RecordPayment.OperationId"/>: a replay returns the
    /// existing payment with <see cref="WarningCodes.PaymentAlreadyRecorded"/>; reusing the id for a
    /// different payment is a <see cref="ErrorCode.Conflict"/>. Equal date and amount alone never count as a duplicate.
    /// </summary>
    public async Task<Result<PaymentDto>> RecordPaymentAsync(RecordPayment command, CancellationToken cancellationToken)
    {
        var reference = TextValidation.NullIfBlank(command.Reference)?.Trim();
        var issues = BudgetValidator.ValidatePayment(command, reference);
        if (issues.Count > 0)
        {
            return OperationError.Validation(issues);
        }

        var replay = await FindReplayAsync(command, reference, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        await using var session = sessions.Open();
        var item = await session.Budget.FindItemAsync(command.BudgetItemId, cancellationToken);
        if (item is null)
        {
            return OperationError.NotFound(BudgetItemEntity);
        }

        var writable = await WritableEvent.FindAsync(session, item.EventId, cancellationToken);
        if (writable.Error is not null)
        {
            return writable.Error;
        }

        if (command.Kind == PaymentKind.Refund && command.Status == PaymentStatus.Posted
            && !BudgetValidator.KeepsNetPaidNonNegative(item, 0, command.AmountMinor))
        {
            return OperationError.Validation(nameof(PaymentDto.AmountMinor), ValidationRules.RefundExceedsPaid);
        }

        var payment = item.AddPayment(
            Guid.CreateVersion7(), command.OperationId, command.Kind, command.AmountMinor, command.Status, command.PaidLocalDate, command.DueLocalDate, reference);
        if (await session.SaveChangesAsync(cancellationToken) == SaveOutcome.Conflict)
        {
            // A concurrent commit may have been this very operation (double submit): answer as a replay.
            return await FindReplayAsync(command, reference, cancellationToken) ?? OperationError.Conflict(BudgetItemEntity);
        }

        logger.LogInformation("Payment {PaymentId} ({Kind}, {Status}) recorded for budget item {ItemId}", payment.Id, payment.Kind, payment.Status, item.Id);
        return payment.ToDto();
    }

    public async Task<Result<PaymentDto>> PostPaymentAsync(PostPayment command, CancellationToken cancellationToken)
    {
        await using var session = sessions.Open();
        var (item, payment, error) = await FindWritablePaymentAsync(session, command.PaymentId, command.ExpectedRevision, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        if (payment!.Status != PaymentStatus.Planned)
        {
            return OperationError.Validation(nameof(PaymentDto.Status), ValidationRules.NotAllowedForStatus);
        }

        item!.PostPayment(payment, command.PaidLocalDate);
        if (await session.SaveChangesAsync(cancellationToken) == SaveOutcome.Conflict)
        {
            return OperationError.Conflict(PaymentEntity);
        }

        return payment.ToDto();
    }

    /// <summary>Cancelling keeps the record for history. A posted payment cannot be cancelled if refunds would exceed what remains paid.</summary>
    public async Task<Result<PaymentDto>> CancelPaymentAsync(CancelPayment command, CancellationToken cancellationToken)
    {
        await using var session = sessions.Open();
        var (item, payment, error) = await FindWritablePaymentAsync(session, command.PaymentId, command.ExpectedRevision, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        if (payment!.Status == PaymentStatus.Cancelled)
        {
            return OperationError.Validation(nameof(PaymentDto.Status), ValidationRules.NotAllowedForStatus);
        }

        if (payment.Status == PaymentStatus.Posted && payment.Kind != PaymentKind.Refund
            && !BudgetValidator.KeepsNetPaidNonNegative(item!, -payment.AmountMinor, 0))
        {
            return OperationError.Validation(nameof(PaymentDto.AmountMinor), ValidationRules.RefundExceedsPaid);
        }

        item!.CancelPayment(payment);
        if (await session.SaveChangesAsync(cancellationToken) == SaveOutcome.Conflict)
        {
            return OperationError.Conflict(PaymentEntity);
        }

        return payment.ToDto();
    }

    private async Task<Result<PaymentDto>?> FindReplayAsync(RecordPayment command, string? reference, CancellationToken cancellationToken)
    {
        await using var session = sessions.Open();
        var existing = await session.Budget.FindPaymentByOperationAsync(command.OperationId, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        if (!existing.Matches(command.BudgetItemId, command.Kind, command.AmountMinor, command.DueLocalDate, reference))
        {
            logger.LogWarning("Operation id {OperationId} was reused for a different payment", command.OperationId);
            return OperationError.Of(ErrorCode.Conflict, "Operation id was already used for a different payment.");
        }

        return Result<PaymentDto>.Success(existing.ToDto(), [new Warning(WarningCodes.PaymentAlreadyRecorded)]);
    }

    private static async Task<(BudgetItem? Item, OperationError? Error)> FindWritableItemAsync(
        IDataSession session, Guid budgetItemId, long expectedRevision, CancellationToken cancellationToken)
    {
        var item = await session.Budget.FindItemAsync(budgetItemId, cancellationToken);
        if (item is null)
        {
            return (null, OperationError.NotFound(BudgetItemEntity));
        }

        var writable = await WritableEvent.FindAsync(session, item.EventId, cancellationToken);
        if (writable.Error is not null)
        {
            return (null, writable.Error);
        }

        return item.Revision != expectedRevision ? (null, OperationError.Conflict(BudgetItemEntity)) : (item, null);
    }

    private static async Task<(BudgetItem? Item, Payment? Payment, OperationError? Error)> FindWritablePaymentAsync(
        IDataSession session, Guid paymentId, long expectedRevision, CancellationToken cancellationToken)
    {
        var item = await session.Budget.FindItemByPaymentAsync(paymentId, cancellationToken);
        var payment = item?.Payments.FirstOrDefault(p => p.Id == paymentId);
        if (item is null || payment is null)
        {
            return (null, null, OperationError.NotFound(PaymentEntity));
        }

        var writable = await WritableEvent.FindAsync(session, item.EventId, cancellationToken);
        if (writable.Error is not null)
        {
            return (null, null, writable.Error);
        }

        return payment.Revision != expectedRevision ? (null, null, OperationError.Conflict(PaymentEntity)) : (item, payment, null);
    }
}
