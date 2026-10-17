using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EventPlanner.Contracts.Budget;
using EventPlanner.Contracts.Events;
using EventPlanner.Contracts.Results;
using EventPlanner.Core.Services.Budget;
using EventPlanner.Desktop.Localization;
using EventPlanner.Desktop.ViewModels.Shell;

namespace EventPlanner.Desktop.ViewModels.Budget;

/// <summary>
/// Budget of the active project: totals per currency (foreign currencies apart), items with
/// estimated / committed / paid, and payments whose paid status is separate from the due date.
/// </summary>
public sealed partial class BudgetViewModel : ViewModelBase, IPage
{
    private readonly BudgetService _budget;
    private readonly ActiveProjectContext _active;
    private readonly TimeProvider _clock;
    private EventProjectDto? _project;

    public BudgetViewModel(BudgetService budget, ActiveProjectContext active, TimeProvider clock)
    {
        _budget = budget;
        _active = active;
        _clock = clock;
        NewPaymentKind = KindOptions[0];
        NewPaymentStatus = StatusOptions[0];
        PaymentPaidDate = Today();
    }

    public string Title => Strings.Nav_Budget;

    public IReadOnlyList<DisplayOption<PaymentKind>> KindOptions { get; } = EnumText.Options<PaymentKind>(EnumText.For);

    /// <summary>Only planned or posted can be recorded; cancelling is a separate action.</summary>
    public IReadOnlyList<DisplayOption<PaymentStatus>> StatusOptions { get; } =
        [.. EnumText.Options<PaymentStatus>(EnumText.For).Where(o => o.Value != PaymentStatus.Cancelled)];

    public ObservableCollection<BudgetItemRow> Items { get; } = [];

    public ObservableCollection<PaymentRow> Payments { get; } = [];

    public ObservableCollection<BudgetTotalsView> OtherCurrencyTotals { get; } = [];

    [ObservableProperty]
    public partial bool HasProject { get; set; }

    [ObservableProperty]
    public partial bool IsReadOnly { get; set; }

    [ObservableProperty]
    public partial BudgetTotalsView? ProjectTotals { get; set; }

    [ObservableProperty]
    public partial BudgetItemRow? SelectedItem { get; set; }

    [ObservableProperty]
    public partial bool IsEditingItem { get; set; }

    [ObservableProperty]
    public partial bool IsNewItem { get; set; }

    [ObservableProperty]
    public partial string ItemCategory { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ItemTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ItemEstimated { get; set; }

    [ObservableProperty]
    public partial string? ItemCommitted { get; set; }

    [ObservableProperty]
    public partial string ItemCurrency { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ItemBalanceText { get; set; }

    [ObservableProperty]
    public partial bool IsConfirmingDelete { get; set; }

    [ObservableProperty]
    public partial PaymentRow? SelectedPayment { get; set; }

    [ObservableProperty]
    public partial DisplayOption<PaymentKind> NewPaymentKind { get; set; }

    [ObservableProperty]
    public partial DisplayOption<PaymentStatus> NewPaymentStatus { get; set; }

    [ObservableProperty]
    public partial string? PaymentAmount { get; set; }

    [ObservableProperty]
    public partial DateTime? PaymentPaidDate { get; set; }

    [ObservableProperty]
    public partial DateTime? PaymentDueDate { get; set; }

    [ObservableProperty]
    public partial string? PaymentReference { get; set; }

    [ObservableProperty]
    public partial DateTime? PostDate { get; set; }

    /// <summary>
    /// Identity of the payment being entered. It changes only after a successful record, so a
    /// repeated click or retry of the same form can never create a second payment.
    /// </summary>
    public Guid PaymentOperationId { get; private set; } = Guid.NewGuid();

    [ObservableProperty]
    public partial FieldErrors Errors { get; set; } = FieldErrors.None;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string? InfoMessage { get; set; }

    [ObservableProperty]
    public partial string? WarningMessage { get; set; }

    public bool IsPostedPayment => NewPaymentStatus.Value == PaymentStatus.Posted;

    public bool CanChangeSelectedPayment => SelectedPayment is { IsCancelled: false } && !IsReadOnly;

    public bool CanPostSelectedPayment => SelectedPayment?.Payment.Status == PaymentStatus.Planned && !IsReadOnly;

    public async Task ActivateAsync(CancellationToken cancellationToken)
    {
        _project = _active.Project;
        HasProject = _project is not null;
        IsReadOnly = _project?.IsReadOnly ?? false;
        IsEditingItem = false;
        ClearMessages();
        if (_project is null)
        {
            Items.Clear();
            Payments.Clear();
            OtherCurrencyTotals.Clear();
            ProjectTotals = null;
            return;
        }

        await ReloadAsync(_project, cancellationToken);
    }

    partial void OnSelectedItemChanged(BudgetItemRow? value)
    {
        if (value is not null)
        {
            EditItem(value);
        }
    }

    partial void OnSelectedPaymentChanged(PaymentRow? value)
    {
        OnPropertyChanged(nameof(CanChangeSelectedPayment));
        OnPropertyChanged(nameof(CanPostSelectedPayment));
    }

    partial void OnNewPaymentStatusChanged(DisplayOption<PaymentStatus> value) => OnPropertyChanged(nameof(IsPostedPayment));

    [RelayCommand]
    private void NewItem()
    {
        if (_project is null)
        {
            return;
        }

        SelectedItem = null;
        IsEditingItem = true;
        IsNewItem = true;
        IsConfirmingDelete = false;
        ItemCategory = string.Empty;
        ItemTitle = string.Empty;
        ItemEstimated = null;
        ItemCommitted = null;
        ItemCurrency = _project.Currency;
        ItemBalanceText = null;
        Payments.Clear();
        ClearMessages();
    }

    [RelayCommand]
    private async Task SaveItemAsync()
    {
        if (_project is null)
        {
            return;
        }

        var currency = ItemCurrency.Trim().ToUpperInvariant();
        var errors = new List<KeyValuePair<string, string>>();
        var estimated = ParseAmount(ItemEstimated, currency, nameof(BudgetItemDto.EstimatedMinor), errors);
        var committed = ParseAmount(ItemCommitted, currency, nameof(BudgetItemDto.CommittedMinor), errors);
        if (errors.Count > 0)
        {
            Errors = new FieldErrors(errors);
            ErrorMessage = Strings.Error_ValidationFailed;
            return;
        }

        var result = SelectedItem is { } editing
            ? await _budget.UpdateItemAsync(
                new UpdateBudgetItem(editing.Item.Id, editing.Item.Revision, ItemCategory, ItemTitle, estimated, committed), CancellationToken.None)
            : await _budget.CreateItemAsync(
                new CreateBudgetItem(_project.Id, ItemCategory, ItemTitle, estimated, committed, currency), CancellationToken.None);
        if (!ShowResult(result))
        {
            return;
        }

        InfoMessage = Strings.Info_Saved;
        await ReloadAsync(_project, CancellationToken.None, selectItemId: result.Value.Id);
    }

    [RelayCommand]
    private void RequestDeleteItem() => IsConfirmingDelete = SelectedItem is not null;

    [RelayCommand]
    private void CancelDeleteItem() => IsConfirmingDelete = false;

    [RelayCommand]
    private async Task ConfirmDeleteItemAsync()
    {
        IsConfirmingDelete = false;
        if (SelectedItem is null || _project is null)
        {
            return;
        }

        var result = await _budget.DeleteItemAsync(new DeleteBudgetItem(SelectedItem.Item.Id, SelectedItem.Item.Revision), CancellationToken.None);
        if (!ShowResult(result))
        {
            return;
        }

        IsEditingItem = false;
        InfoMessage = Strings.Info_Deleted;
        await ReloadAsync(_project, CancellationToken.None);
    }

    [RelayCommand]
    private async Task RecordPaymentAsync()
    {
        if (SelectedItem is null || _project is null)
        {
            return;
        }

        var errors = new List<KeyValuePair<string, string>>();
        var amount = ParseAmount(PaymentAmount, SelectedItem.Item.Currency, nameof(PaymentDto.AmountMinor), errors);
        if (amount is null && errors.Count == 0)
        {
            errors.Add(KeyValuePair.Create(nameof(PaymentDto.AmountMinor), Strings.Validation_Required));
        }

        if (errors.Count > 0)
        {
            Errors = new FieldErrors(errors);
            ErrorMessage = Strings.Error_ValidationFailed;
            return;
        }

        var posted = IsPostedPayment;
        var result = await _budget.RecordPaymentAsync(
            new RecordPayment(
                SelectedItem.Item.Id,
                PaymentOperationId,
                NewPaymentKind.Value,
                amount!.Value,
                NewPaymentStatus.Value,
                posted ? ToDate(PaymentPaidDate) : null,
                ToDate(PaymentDueDate),
                PaymentReference),
            CancellationToken.None);
        if (!ShowResult(result))
        {
            return;
        }

        InfoMessage = Strings.Budget_PaymentRecorded;
        ResetPaymentForm();
        await ReloadAsync(_project, CancellationToken.None, selectItemId: SelectedItem.Item.Id);
    }

    [RelayCommand]
    private async Task PostSelectedPaymentAsync()
    {
        if (SelectedPayment is null || SelectedItem is null || _project is null)
        {
            return;
        }

        if (PostDate is null)
        {
            Errors = new FieldErrors([KeyValuePair.Create(nameof(PaymentDto.PaidLocalDate), Strings.Validation_Required)]);
            ErrorMessage = Strings.Error_ValidationFailed;
            return;
        }

        var result = await _budget.PostPaymentAsync(
            new PostPayment(SelectedPayment.Payment.Id, SelectedPayment.Payment.Revision, DateOnly.FromDateTime(PostDate.Value)), CancellationToken.None);
        if (!ShowResult(result))
        {
            return;
        }

        InfoMessage = Strings.Budget_PaymentPosted;
        await ReloadAsync(_project, CancellationToken.None, selectItemId: SelectedItem.Item.Id);
    }

    [RelayCommand]
    private async Task CancelSelectedPaymentAsync()
    {
        if (SelectedPayment is null || SelectedItem is null || _project is null)
        {
            return;
        }

        var result = await _budget.CancelPaymentAsync(new CancelPayment(SelectedPayment.Payment.Id, SelectedPayment.Payment.Revision), CancellationToken.None);
        if (!ShowResult(result))
        {
            return;
        }

        InfoMessage = Strings.Budget_PaymentCancelled;
        await ReloadAsync(_project, CancellationToken.None, selectItemId: SelectedItem.Item.Id);
    }

    private static long? ParseAmount(string? text, string currency, string field, List<KeyValuePair<string, string>> errors)
    {
        if (MoneyText.TryParse(text, currency, out var amount))
        {
            return amount;
        }

        errors.Add(KeyValuePair.Create(field, Strings.Validation_InvalidAmount));
        return null;
    }

    private static DateOnly? ToDate(DateTime? value) => value is { } date ? DateOnly.FromDateTime(date) : null;

    private bool ShowResult<T>(Result<T> result)
    {
        Errors = ErrorText.FieldsOf(result.Error);
        if (!result.IsSuccess)
        {
            ErrorMessage = ErrorText.For(result.Error!);
            InfoMessage = null;
            return false;
        }

        ErrorMessage = null;
        WarningMessage = result.Warnings.Count > 0 ? string.Join(" ", result.Warnings.Select(ErrorText.For)) : null;
        return true;
    }

    private async Task ReloadAsync(EventProjectDto project, CancellationToken cancellationToken, Guid? selectItemId = null)
    {
        var result = await _budget.GetOverviewAsync(project.Id, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (_active.Project?.Id != project.Id)
        {
            return; // Results for a project that is no longer active are dropped.
        }

        if (!result.IsSuccess)
        {
            ErrorMessage = ErrorText.For(result.Error!);
            return;
        }

        var overview = result.Value;
        ProjectTotals = new BudgetTotalsView(overview.ProjectTotals);
        OtherCurrencyTotals.Clear();
        foreach (var totals in overview.OtherCurrencyTotals)
        {
            OtherCurrencyTotals.Add(new BudgetTotalsView(totals));
        }

        var keepId = selectItemId ?? (IsEditingItem ? SelectedItem?.Item.Id : null);
        Items.Clear();
        foreach (var summary in overview.Items)
        {
            Items.Add(new BudgetItemRow(summary, string.Equals(summary.Item.Currency, overview.ProjectCurrency, StringComparison.Ordinal)));
        }

        // Records compare by value, so the editor is refreshed explicitly after a reload.
        if (keepId is { } id && Items.FirstOrDefault(i => i.Item.Id == id) is { } row)
        {
            SelectedItem = row;
            EditItem(row);
        }
    }

    private void EditItem(BudgetItemRow row)
    {
        var keepPaymentId = SelectedPayment?.Payment.Id;
        IsEditingItem = true;
        IsNewItem = false;
        IsConfirmingDelete = false;
        ItemCategory = row.Item.Category;
        ItemTitle = row.Item.Title;
        ItemEstimated = MoneyText.FormatNumber(row.Item.EstimatedMinor, row.Item.Currency);
        ItemCommitted = MoneyText.FormatNumber(row.Item.CommittedMinor, row.Item.Currency);
        ItemCurrency = row.Item.Currency;
        ItemBalanceText = row.BalanceText;
        Payments.Clear();
        foreach (var payment in row.Summary.Payments)
        {
            Payments.Add(new PaymentRow(payment, row.Item.Currency));
        }

        SelectedPayment = Payments.FirstOrDefault(p => p.Payment.Id == keepPaymentId);
        PostDate ??= Today();
        Errors = FieldErrors.None;
    }

    private void ResetPaymentForm()
    {
        PaymentOperationId = Guid.NewGuid();
        PaymentAmount = null;
        PaymentReference = null;
        PaymentDueDate = null;
        PaymentPaidDate = Today();
    }

    private DateTime Today() => _clock.GetLocalNow().Date;

    private void ClearMessages()
    {
        Errors = FieldErrors.None;
        ErrorMessage = null;
        InfoMessage = null;
        WarningMessage = null;
    }
}
