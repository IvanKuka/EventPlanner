using EventPlanner.Contracts.Budget;
using EventPlanner.Contracts.Events;
using EventPlanner.Contracts.Guests;
using EventPlanner.Contracts.Tasks;
using EventPlanner.Contracts.Vendors;
using EventPlanner.Desktop.ViewModels;

namespace EventPlanner.Desktop.Localization;

/// <summary>Slovak labels for stable English domain codes.</summary>
public static class EnumText
{
    public static string For(EventStatus status) => status switch
    {
        EventStatus.Draft => Strings.Status_Draft,
        EventStatus.Planning => Strings.Status_Planning,
        EventStatus.Ready => Strings.Status_Ready,
        EventStatus.Completed => Strings.Status_Completed,
        EventStatus.Archived => Strings.Status_Archived,
        _ => status.ToString(),
    };

    public static string For(EventType type) => type switch
    {
        EventType.Wedding => Strings.EventType_Wedding,
        EventType.Celebration => Strings.EventType_Celebration,
        EventType.Tournament => Strings.EventType_Tournament,
        _ => Strings.EventType_Other,
    };

    public static string For(EventTemplate template) => template switch
    {
        EventTemplate.Wedding => Strings.Template_Wedding,
        EventTemplate.Celebration => Strings.Template_Celebration,
        _ => Strings.Template_Blank,
    };

    public static string For(RsvpState state) => state switch
    {
        RsvpState.Invited => Strings.Rsvp_Invited,
        RsvpState.Accepted => Strings.Rsvp_Accepted,
        RsvpState.Declined => Strings.Rsvp_Declined,
        RsvpState.Tentative => Strings.Rsvp_Tentative,
        _ => Strings.Rsvp_Unknown,
    };

    public static string For(GuestKind kind) => kind switch
    {
        GuestKind.PlusOnePlaceholder => Strings.GuestKind_PlusOnePlaceholder,
        _ => Strings.GuestKind_Named,
    };

    public static string For(QuoteStatus status) => status switch
    {
        QuoteStatus.Selected => Strings.QuoteStatus_Selected,
        QuoteStatus.Declined => Strings.QuoteStatus_Declined,
        _ => Strings.QuoteStatus_Received,
    };

    public static string For(QuoteDecisionKind kind) => kind switch
    {
        QuoteDecisionKind.Selected => Strings.QuoteDecision_Selected,
        QuoteDecisionKind.Replaced => Strings.QuoteDecision_Replaced,
        _ => Strings.QuoteDecision_Declined,
    };

    public static string For(TransportCoverage transport) => transport switch
    {
        TransportCoverage.Included => Strings.Transport_Included,
        TransportCoverage.Separate => Strings.Transport_Separate,
        TransportCoverage.NotNeeded => Strings.Transport_NotNeeded,
        _ => Strings.Transport_Unknown,
    };

    public static string For(BudgetImpactPolicy policy) => policy switch
    {
        BudgetImpactPolicy.CreateBudgetItem => Strings.BudgetImpact_Create,
        BudgetImpactPolicy.UpdateBudgetItem => Strings.BudgetImpact_Update,
        _ => Strings.BudgetImpact_Leave,
    };

    public static string For(PaymentKind kind) => kind switch
    {
        PaymentKind.Deposit => Strings.PaymentKind_Deposit,
        PaymentKind.Refund => Strings.PaymentKind_Refund,
        _ => Strings.PaymentKind_Balance,
    };

    public static string For(TaskState state) => state switch
    {
        TaskState.InProgress => Strings.TaskState_InProgress,
        TaskState.Done => Strings.TaskState_Done,
        _ => Strings.TaskState_Todo,
    };

    public static string For(TaskPriority priority) => priority switch
    {
        TaskPriority.Low => Strings.TaskPriority_Low,
        TaskPriority.High => Strings.TaskPriority_High,
        _ => Strings.TaskPriority_Normal,
    };

    public static string For(PaymentStatus status) => status switch
    {
        PaymentStatus.Posted => Strings.PaymentStatus_Posted,
        PaymentStatus.Cancelled => Strings.PaymentStatus_Cancelled,
        _ => Strings.PaymentStatus_Planned,
    };

    /// <summary>Labels for <see cref="QuoteFields"/> names listed as missing in a comparison.</summary>
    public static string ForQuoteField(string field) => field switch
    {
        QuoteFields.Transport => Strings.QuoteField_Transport,
        QuoteFields.TransportPrice => Strings.QuoteField_TransportPrice,
        QuoteFields.PeopleCount => Strings.QuoteField_PeopleCount,
        QuoteFields.Exclusions => Strings.QuoteField_Exclusions,
        QuoteFields.ValidUntil => Strings.QuoteField_ValidUntil,
        _ => field,
    };

    public static IReadOnlyList<DisplayOption<T>> Options<T>(Func<T, string> label)
        where T : struct, Enum =>
        [.. Enum.GetValues<T>().Select(v => new DisplayOption<T>(v, label(v)))];
}
