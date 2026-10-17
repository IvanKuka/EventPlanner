using EventPlanner.Contracts.Budget;

namespace EventPlanner.Core.Models.Budget;

/// <summary>
/// One budget line with its payments. Estimated, committed and paid are independent figures;
/// paid is always derived from posted payments, never stored. Every payment change bumps
/// <see cref="Revision"/>, so concurrent payment commands on one item cannot both commit.
/// </summary>
public sealed class BudgetItem
{
    private readonly List<Payment> _payments = [];

    private BudgetItem()
    {
    }

    public Guid Id { get; private set; }

    public Guid EventId { get; private set; }

    public string Category { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public long? EstimatedMinor { get; private set; }

    public long? CommittedMinor { get; private set; }

    /// <summary>ISO 4217 code fixed at creation; items in another than the project currency are totalled separately.</summary>
    public string Currency { get; private set; } = string.Empty;

    public Guid? SelectedQuoteId { get; private set; }

    public long Revision { get; private set; }

    public IReadOnlyList<Payment> Payments => _payments;

    public long PostedPaymentsMinor => SumPosted(refunds: false);

    public long PostedRefundsMinor => SumPosted(refunds: true);

    public static BudgetItem Create(Guid id, Guid eventId, string category, string title, long? estimatedMinor, long? committedMinor, string currency)
    {
        var item = new BudgetItem { Id = id, EventId = eventId, Currency = currency, Revision = 1 };
        item.Apply(category, title, estimatedMinor, committedMinor);
        return item;
    }

    public void Update(string category, string title, long? estimatedMinor, long? committedMinor)
    {
        Apply(category, title, estimatedMinor, committedMinor);
        Revision++;
    }

    /// <summary>Applies an explicitly chosen quote: the committed amount and the link change together.</summary>
    public void CommitToQuote(Guid quoteId, long committedMinor)
    {
        SelectedQuoteId = quoteId;
        CommittedMinor = committedMinor;
        Revision++;
    }

    /// <remarks>The caller validates amount, dates and the refund limit first.</remarks>
    public Payment AddPayment(Guid paymentId, Guid operationId, PaymentKind kind, long amountMinor, PaymentStatus status, DateOnly? paidLocalDate, DateOnly? dueLocalDate, string? reference)
    {
        var payment = new Payment(paymentId, Id, operationId, kind, amountMinor, status, paidLocalDate, dueLocalDate, reference);
        _payments.Add(payment);
        Revision++;
        return payment;
    }

    public void PostPayment(Payment payment, DateOnly paidLocalDate)
    {
        payment.Post(paidLocalDate);
        Revision++;
    }

    public void CancelPayment(Payment payment)
    {
        payment.Cancel();
        Revision++;
    }

    private long SumPosted(bool refunds) =>
        _payments.Where(p => p.Status == PaymentStatus.Posted && (p.Kind == PaymentKind.Refund) == refunds).Sum(p => p.AmountMinor);

    private void Apply(string category, string title, long? estimatedMinor, long? committedMinor)
    {
        Category = category.Trim();
        Title = title.Trim();
        EstimatedMinor = estimatedMinor;
        CommittedMinor = committedMinor;
    }
}
