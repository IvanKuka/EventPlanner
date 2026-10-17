using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EventPlanner.Contracts.Events;
using EventPlanner.Contracts.Results;
using EventPlanner.Contracts.Vendors;
using EventPlanner.Core.Services.Budget;
using EventPlanner.Core.Services.Vendors;
using EventPlanner.Desktop.Localization;
using EventPlanner.Desktop.ViewModels.Shell;
using Microsoft.Extensions.Logging;

namespace EventPlanner.Desktop.ViewModels.Vendors;

/// <summary>
/// Vendors and the per-category quote comparison. Selecting a quote is the organizer's decision
/// only (no booking) and touches the budget only through the explicitly chosen policy.
/// </summary>
public sealed partial class VendorsViewModel : ViewModelBase, IPage
{
    private readonly VendorService _vendors;
    private readonly QuoteComparisonService _comparison;
    private readonly BudgetService _budget;
    private readonly ActiveProjectContext _active;
    private readonly ILogger<VendorsViewModel> _logger;
    private EventProjectDto? _project;
    private List<VendorDto> _allVendors = [];
    private bool _isReloading;
    private Guid? _decisionVendorId;

    public VendorsViewModel(
        VendorService vendors, QuoteComparisonService comparison, BudgetService budget, ActiveProjectContext active, ILogger<VendorsViewModel> logger)
    {
        _vendors = vendors;
        _comparison = comparison;
        _budget = budget;
        _active = active;
        _logger = logger;
        QuoteTransport = TransportOptions[0];
        SelectionPolicy = PolicyOptions[0];
        QuoteVendor = NoVendor;
        SelectionBudgetItem = NoBudgetItem;
    }

    public string Title => Strings.Nav_Vendors;

    private static DisplayOption<Guid?> NoVendor { get; } = new(null, Strings.Vendors_ChooseVendor);

    private static DisplayOption<Guid?> NoBudgetItem { get; } = new(null, Strings.Vendors_ChooseBudgetItem);

    public IReadOnlyList<DisplayOption<TransportCoverage>> TransportOptions { get; } = EnumText.Options<TransportCoverage>(EnumText.For);

    public IReadOnlyList<DisplayOption<BudgetImpactPolicy>> PolicyOptions { get; } = EnumText.Options<BudgetImpactPolicy>(EnumText.For);

    public ObservableCollection<VendorDto> Vendors { get; } = [];

    public ObservableCollection<string> Categories { get; } = [];

    public ObservableCollection<QuoteRow> Quotes { get; } = [];

    public ObservableCollection<DisplayOption<Guid?>> VendorOptions { get; } = [];

    public ObservableCollection<DisplayOption<Guid?>> BudgetItemOptions { get; } = [];

    /// <summary>Decision history of the vendor whose detail (or quote) is open.</summary>
    public ObservableCollection<QuoteDecisionRow> Decisions { get; } = [];

    [ObservableProperty]
    public partial bool HasProject { get; set; }

    [ObservableProperty]
    public partial bool IsReadOnly { get; set; }

    [ObservableProperty]
    public partial string? SelectedCategory { get; set; }

    [ObservableProperty]
    public partial string? ComparisonSummary { get; set; }

    [ObservableProperty]
    public partial VendorDto? SelectedVendor { get; set; }

    [ObservableProperty]
    public partial QuoteRow? SelectedQuote { get; set; }

    [ObservableProperty]
    public partial bool IsEditingVendor { get; set; }

    [ObservableProperty]
    public partial bool IsEditingQuote { get; set; }

    [ObservableProperty]
    public partial bool IsNewVendor { get; set; }

    // Deleting a vendor removes its quotes and decision history; deleting a quote removes its history.
    // Both are one click away in the ribbon, so they ask first.
    [ObservableProperty]
    public partial bool IsConfirmingVendorDelete { get; set; }

    [ObservableProperty]
    public partial bool IsConfirmingQuoteDelete { get; set; }

    [ObservableProperty]
    public partial string VendorCategory { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string VendorName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? VendorContact { get; set; }

    [ObservableProperty]
    public partial string? VendorNotes { get; set; }

    [ObservableProperty]
    public partial DisplayOption<Guid?> QuoteVendor { get; set; }

    [ObservableProperty]
    public partial string QuoteDescription { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? QuoteAmount { get; set; }

    [ObservableProperty]
    public partial string QuoteCurrency { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? QuoteScope { get; set; }

    [ObservableProperty]
    public partial string? QuoteExclusions { get; set; }

    [ObservableProperty]
    public partial DateTime? QuoteValidUntil { get; set; }

    [ObservableProperty]
    public partial DisplayOption<TransportCoverage> QuoteTransport { get; set; }

    [ObservableProperty]
    public partial string? QuoteTransportAmount { get; set; }

    [ObservableProperty]
    public partial string? QuotePeopleCount { get; set; }

    [ObservableProperty]
    public partial DisplayOption<BudgetImpactPolicy> SelectionPolicy { get; set; }

    [ObservableProperty]
    public partial DisplayOption<Guid?> SelectionBudgetItem { get; set; }

    [ObservableProperty]
    public partial string? DecisionNote { get; set; }

    [ObservableProperty]
    public partial bool ShowDecisions { get; set; }

    [ObservableProperty]
    public partial FieldErrors Errors { get; set; } = FieldErrors.None;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string? InfoMessage { get; set; }

    [ObservableProperty]
    public partial string? WarningMessage { get; set; }

    public bool IsTransportSeparate => QuoteTransport.Value == TransportCoverage.Separate;

    public bool IsUpdatePolicy => SelectionPolicy.Value == BudgetImpactPolicy.UpdateBudgetItem;

    public bool CanSelectQuote => SelectedQuote is not null && !IsReadOnly;

    /// <summary>The latest decision history load; awaited by tests, the UI just observes <see cref="Decisions"/>.</summary>
    internal Task DecisionsLoad { get; private set; } = Task.CompletedTask;

    public async Task ActivateAsync(CancellationToken cancellationToken)
    {
        _project = _active.Project;
        HasProject = _project is not null;
        IsReadOnly = _project?.IsReadOnly ?? false;
        IsEditingVendor = false;
        IsEditingQuote = false;
        ClearMessages();
        if (_project is null)
        {
            Vendors.Clear();
            Quotes.Clear();
            Categories.Clear();
            LoadDecisions(null);
            return;
        }

        await ReloadAsync(_project, cancellationToken);
    }

    partial void OnSelectedCategoryChanged(string? value)
    {
        // During a full reload the comparison is loaded (and awaited) by ReloadAsync itself.
        if (!_isReloading)
        {
            _ = ReloadComparisonSafelyAsync();
        }
    }

    partial void OnSelectedVendorChanged(VendorDto? value)
    {
        if (value is not null)
        {
            EditVendor(value);
        }
    }

    partial void OnSelectedQuoteChanged(QuoteRow? value)
    {
        OnPropertyChanged(nameof(CanSelectQuote));
        if (value is not null)
        {
            EditQuote(value.Quote);
        }
    }

    partial void OnQuoteTransportChanged(DisplayOption<TransportCoverage> value) => OnPropertyChanged(nameof(IsTransportSeparate));

    partial void OnSelectionPolicyChanged(DisplayOption<BudgetImpactPolicy> value) => OnPropertyChanged(nameof(IsUpdatePolicy));

    [RelayCommand]
    private void NewVendor()
    {
        SelectedVendor = null;
        SelectedQuote = null;
        IsEditingQuote = false;
        IsEditingVendor = true;
        IsNewVendor = true;
        VendorCategory = SelectedCategory ?? string.Empty;
        VendorName = string.Empty;
        VendorContact = null;
        VendorNotes = null;
        LoadDecisions(null);
        ClearMessages();
    }

    [RelayCommand]
    private async Task SaveVendorAsync()
    {
        if (_project is null)
        {
            return;
        }

        var result = SelectedVendor is null
            ? await _vendors.CreateVendorAsync(new CreateVendor(_project.Id, VendorCategory, VendorName, VendorContact, VendorNotes), CancellationToken.None)
            : await _vendors.UpdateVendorAsync(new UpdateVendor(SelectedVendor.Id, SelectedVendor.Revision, VendorCategory, VendorName, VendorContact, VendorNotes), CancellationToken.None);

        if (!ShowResult(result))
        {
            return;
        }

        InfoMessage = Strings.Info_Saved;
        await ReloadAsync(_project, CancellationToken.None, selectVendorId: result.Value.Id, category: result.Value.Category);
    }

    [RelayCommand]
    private void RequestDeleteVendor() => IsConfirmingVendorDelete = SelectedVendor is not null;

    [RelayCommand]
    private void RequestDeleteQuote() => IsConfirmingQuoteDelete = SelectedQuote is not null;

    [RelayCommand]
    private void CancelDelete()
    {
        IsConfirmingVendorDelete = false;
        IsConfirmingQuoteDelete = false;
    }

    [RelayCommand]
    private async Task DeleteVendorAsync()
    {
        IsConfirmingVendorDelete = false;
        if (SelectedVendor is null || _project is null)
        {
            return;
        }

        var result = await _vendors.DeleteVendorAsync(new DeleteVendor(SelectedVendor.Id, SelectedVendor.Revision), CancellationToken.None);
        if (!ShowResult(result))
        {
            return;
        }

        IsEditingVendor = false;
        InfoMessage = Strings.Vendors_VendorDeleted;
        await ReloadAsync(_project, CancellationToken.None);
    }

    [RelayCommand]
    private void NewQuote()
    {
        if (_project is null)
        {
            return;
        }

        var vendorId = SelectedVendor?.Id ?? SelectedQuote?.Quote.VendorId;
        SelectedQuote = null;
        IsEditingVendor = false;
        IsEditingQuote = true;
        QuoteVendor = VendorOptions.FirstOrDefault(o => o.Value == vendorId) ?? NoVendor;
        QuoteDescription = string.Empty;
        QuoteAmount = null;
        QuoteCurrency = _project.Currency;
        QuoteScope = null;
        QuoteExclusions = null;
        QuoteValidUntil = null;
        QuoteTransport = TransportOptions[0];
        QuoteTransportAmount = null;
        QuotePeopleCount = null;
        DecisionNote = null;
        LoadDecisions(null);
        ClearMessages();
    }

    [RelayCommand]
    private async Task SaveQuoteAsync()
    {
        if (_project is null)
        {
            return;
        }

        var input = ReadQuoteInput();
        if (input is null)
        {
            return;
        }

        Result<QuoteDto> result;
        if (SelectedQuote is { } editing)
        {
            result = await _vendors.UpdateQuoteAsync(new UpdateQuote(editing.Quote.Id, editing.Quote.Revision, input), CancellationToken.None);
        }
        else if (QuoteVendor.Value is { } vendorId)
        {
            result = await _vendors.AddQuoteAsync(new AddQuote(vendorId, input), CancellationToken.None);
        }
        else
        {
            Errors = new FieldErrors([KeyValuePair.Create(nameof(QuoteDto.VendorId), Strings.Validation_Required)]);
            ErrorMessage = Strings.Error_ValidationFailed;
            return;
        }

        if (!ShowResult(result))
        {
            return;
        }

        InfoMessage = Strings.Info_Saved;
        var category = _allVendors.FirstOrDefault(v => v.Id == result.Value.VendorId)?.Category;
        await ReloadAsync(_project, CancellationToken.None, selectQuoteId: result.Value.Id, category: category);
    }

    [RelayCommand]
    private async Task DeleteQuoteAsync()
    {
        IsConfirmingQuoteDelete = false;
        if (SelectedQuote is null || _project is null)
        {
            return;
        }

        var result = await _vendors.DeleteQuoteAsync(new DeleteQuote(SelectedQuote.Quote.Id, SelectedQuote.Quote.Revision), CancellationToken.None);
        if (!ShowResult(result))
        {
            return;
        }

        IsEditingQuote = false;
        InfoMessage = Strings.Info_Deleted;
        await ReloadAsync(_project, CancellationToken.None);
    }

    [RelayCommand]
    private async Task DeclineQuoteAsync()
    {
        if (SelectedQuote is null || _project is null)
        {
            return;
        }

        var result = await _vendors.DeclineQuoteAsync(new DeclineQuote(SelectedQuote.Quote.Id, SelectedQuote.Quote.Revision, DecisionNote), CancellationToken.None);
        if (!ShowResult(result))
        {
            return;
        }

        InfoMessage = Strings.Vendors_QuoteDeclined;
        await ReloadAsync(_project, CancellationToken.None, selectQuoteId: result.Value.Id);
    }

    [RelayCommand]
    private async Task SelectQuoteAsync()
    {
        if (SelectedQuote is null || _project is null)
        {
            return;
        }

        Guid? itemId = null;
        long? itemRevision = null;
        if (SelectionPolicy.Value == BudgetImpactPolicy.UpdateBudgetItem)
        {
            if (SelectionBudgetItem.Value is not { } chosen)
            {
                Errors = new FieldErrors([KeyValuePair.Create(nameof(SelectQuote.BudgetItemId), Strings.Validation_Required)]);
                ErrorMessage = Strings.Error_ValidationFailed;
                return;
            }

            // The revision is read right before the command so the update is checked against the current item.
            var overview = await _budget.GetOverviewAsync(_project.Id, CancellationToken.None);
            var item = overview.IsSuccess ? overview.Value.Items.FirstOrDefault(i => i.Item.Id == chosen)?.Item : null;
            itemId = chosen;
            itemRevision = item?.Revision ?? 0;
        }

        var result = await _comparison.SelectQuoteAsync(
            new SelectQuote(SelectedQuote.Quote.Id, SelectedQuote.Quote.Revision, SelectionPolicy.Value, itemId, itemRevision, DecisionNote), CancellationToken.None);
        if (!ShowResult(result))
        {
            return;
        }

        InfoMessage = result.Value.BudgetItemId is null ? Strings.Vendors_QuoteSelectedBudgetUnchanged : Strings.Vendors_QuoteSelectedBudgetUpdated;
        await ReloadAsync(_project, CancellationToken.None, selectQuoteId: result.Value.Quote.Id);
    }

    /// <summary>Applies the result to messages and field errors; returns true on success.</summary>
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

    /// <summary>Parses typed amounts; empty optional fields stay unknown (null), never zero.</summary>
    private QuoteInput? ReadQuoteInput()
    {
        var currency = QuoteCurrency.Trim().ToUpperInvariant();
        var errors = new List<KeyValuePair<string, string>>();
        if (!MoneyText.TryParse(QuoteAmount, currency, out var amount) || amount is null)
        {
            errors.Add(KeyValuePair.Create(nameof(QuoteDto.AmountMinor), amount is null && string.IsNullOrWhiteSpace(QuoteAmount)
                ? Strings.Validation_Required
                : Strings.Validation_InvalidAmount));
        }

        long? transport = null;
        if (IsTransportSeparate && !MoneyText.TryParse(QuoteTransportAmount, currency, out transport))
        {
            errors.Add(KeyValuePair.Create(nameof(QuoteDto.TransportMinor), Strings.Validation_InvalidAmount));
        }

        int? people = null;
        if (!string.IsNullOrWhiteSpace(QuotePeopleCount))
        {
            if (int.TryParse(QuotePeopleCount.Trim(), NumberStyles.None, CultureInfo.CurrentCulture, out var parsed))
            {
                people = parsed;
            }
            else
            {
                errors.Add(KeyValuePair.Create(nameof(QuoteDto.PeopleCount), Strings.Validation_InvalidValue));
            }
        }

        if (errors.Count > 0)
        {
            Errors = new FieldErrors(errors);
            ErrorMessage = Strings.Error_ValidationFailed;
            return null;
        }

        return new QuoteInput(
            QuoteDescription,
            amount!.Value,
            currency,
            QuoteScope ?? string.Empty,
            QuoteExclusions,
            QuoteValidUntil is { } until ? DateOnly.FromDateTime(until) : null,
            QuoteTransport.Value,
            transport,
            people);
    }

    private async Task ReloadAsync(EventProjectDto project, CancellationToken cancellationToken, Guid? selectVendorId = null, Guid? selectQuoteId = null, string? category = null)
    {
        var vendorsTask = _vendors.ListVendorsAsync(project.Id, cancellationToken);
        var categoriesTask = _comparison.ListCategoriesAsync(project.Id, cancellationToken);
        var budgetTask = _budget.GetOverviewAsync(project.Id, cancellationToken);
        await Task.WhenAll(vendorsTask, categoriesTask, budgetTask);
        cancellationToken.ThrowIfCancellationRequested();
        if (_active.Project?.Id != project.Id)
        {
            return; // Results for a project that is no longer active are dropped.
        }

        // A bound ComboBox writes null back when its items are cleared, so the choices are captured
        // first and restored below; a null option would break every later ".Value" read.
        var quoteVendorId = QuoteVendor?.Value;
        var budgetItemId = SelectionBudgetItem?.Value;

        _allVendors = [.. vendorsTask.Result];
        Vendors.Clear();
        VendorOptions.Clear();
        VendorOptions.Add(NoVendor);
        foreach (var vendor in _allVendors)
        {
            Vendors.Add(vendor);
            VendorOptions.Add(new DisplayOption<Guid?>(vendor.Id, $"{vendor.Name} ({vendor.Category})"));
        }

        BudgetItemOptions.Clear();
        BudgetItemOptions.Add(NoBudgetItem);
        if (budgetTask.Result.IsSuccess)
        {
            foreach (var item in budgetTask.Result.Value.Items)
            {
                BudgetItemOptions.Add(new DisplayOption<Guid?>(item.Item.Id, $"{item.Item.Category}: {item.Item.Title} ({item.Item.Currency})"));
            }
        }

        QuoteVendor = VendorOptions.FirstOrDefault(o => o.Value == quoteVendorId) ?? NoVendor;
        SelectionBudgetItem = BudgetItemOptions.FirstOrDefault(o => o.Value == budgetItemId) ?? NoBudgetItem;

        var wantedCategory = category ?? SelectedCategory;
        _isReloading = true;
        try
        {
            // Clearing the bound list resets the combo box selection; that must not start its own load.
            Categories.Clear();
            foreach (var name in categoriesTask.Result)
            {
                Categories.Add(name);
            }

            SelectedCategory = Categories.FirstOrDefault(c => string.Equals(c, wantedCategory, StringComparison.CurrentCultureIgnoreCase))
                ?? Categories.FirstOrDefault();
        }
        finally
        {
            _isReloading = false;
        }

        await ReloadComparisonAsync(project, cancellationToken);

        // DTOs are records: an unchanged reloaded item equals the old one and the setter would not
        // fire, so the open editor is refreshed explicitly.
        if (selectVendorId is { } vendorId && Vendors.FirstOrDefault(v => v.Id == vendorId) is { } selectedVendor)
        {
            SelectedVendor = selectedVendor;
            EditVendor(selectedVendor);
        }

        if (selectQuoteId is { } quoteId)
        {
            if (Quotes.FirstOrDefault(q => q.Quote.Id == quoteId) is { } row)
            {
                SelectedQuote = row;
                EditQuote(row.Quote);
            }
        }
    }

    /// <summary>Started from a combo box change (fire-and-forget), so failures are logged and shown here.</summary>
    private async Task ReloadComparisonSafelyAsync()
    {
        if (_project is null)
        {
            return;
        }

        try
        {
            await ReloadComparisonAsync(_project, CancellationToken.None);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
        {
            _logger.LogError(ex, "Quote comparison could not be loaded");
            ErrorMessage = Strings.Error_Unexpected;
        }
    }

    private async Task ReloadComparisonAsync(EventProjectDto project, CancellationToken cancellationToken)
    {
        if (SelectedCategory is not { } category)
        {
            Quotes.Clear();
            ComparisonSummary = null;
            return;
        }

        var result = await _comparison.CompareQuotesAsync(project.Id, category, cancellationToken);
        if (_active.Project?.Id != project.Id || SelectedCategory != category)
        {
            return;
        }

        if (!result.IsSuccess)
        {
            ErrorMessage = ErrorText.For(result.Error!);
            return;
        }

        var comparison = result.Value;
        Quotes.Clear();
        foreach (var row in comparison.Rows)
        {
            Quotes.Add(new QuoteRow(row, comparison.ProjectCurrency));
        }

        ComparisonSummary = comparison.Currencies.Count > 1
            ? string.Format(CultureInfo.CurrentCulture, Strings.Vendors_MultipleCurrencies, string.Join(", ", comparison.Currencies))
            : null;
    }

    private void EditVendor(VendorDto vendor)
    {
        CancelDelete();
        SelectedQuote = null;
        IsEditingQuote = false;
        IsEditingVendor = true;
        IsNewVendor = false;
        VendorCategory = vendor.Category;
        VendorName = vendor.Name;
        VendorContact = vendor.Contact;
        VendorNotes = vendor.Notes;
        LoadDecisions(vendor.Id);
        Errors = FieldErrors.None;
    }

    private void EditQuote(QuoteDto quote)
    {
        CancelDelete();
        // Clear the vendor selection so clicking the same vendor again reopens its editor.
        SelectedVendor = null;
        IsEditingVendor = false;
        IsEditingQuote = true;
        QuoteVendor = VendorOptions.FirstOrDefault(o => o.Value == quote.VendorId) ?? NoVendor;
        QuoteDescription = quote.Description;
        QuoteAmount = MoneyText.FormatNumber(quote.AmountMinor, quote.Currency);
        QuoteCurrency = quote.Currency;
        QuoteScope = quote.ScopeText;
        QuoteExclusions = quote.ExclusionsText;
        QuoteValidUntil = quote.ValidUntilDate?.ToDateTime(TimeOnly.MinValue);
        QuoteTransport = TransportOptions.First(o => o.Value == quote.Transport);
        QuoteTransportAmount = MoneyText.FormatNumber(quote.TransportMinor, quote.Currency);
        QuotePeopleCount = quote.PeopleCount?.ToString(CultureInfo.CurrentCulture);
        DecisionNote = null;
        LoadDecisions(quote.VendorId);
        Errors = FieldErrors.None;
    }

    private void LoadDecisions(Guid? vendorId)
    {
        _decisionVendorId = vendorId;
        ShowDecisions = vendorId is not null;
        DecisionsLoad = LoadDecisionsAsync(vendorId);
    }

    private async Task LoadDecisionsAsync(Guid? vendorId)
    {
        if (vendorId is not { } id)
        {
            Decisions.Clear();
            return;
        }

        IReadOnlyList<QuoteDecisionDto> decisions;
        try
        {
            decisions = await _vendors.ListDecisionsAsync(id, CancellationToken.None);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
        {
            // Not awaited by the UI: without this the failure would only surface as an unobserved task.
            _logger.LogError(ex, "Decision history of vendor {VendorId} could not be loaded", id);
            ErrorMessage = Strings.Error_Unexpected;
            return;
        }

        if (_decisionVendorId != id)
        {
            return; // Another vendor was opened meanwhile.
        }

        Decisions.Clear();
        foreach (var decision in decisions)
        {
            Decisions.Add(new QuoteDecisionRow(decision));
        }
    }

    private void ClearMessages()
    {
        CancelDelete();
        Errors = FieldErrors.None;
        ErrorMessage = null;
        InfoMessage = null;
        WarningMessage = null;
    }
}
