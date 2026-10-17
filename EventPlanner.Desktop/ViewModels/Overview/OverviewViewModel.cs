using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using EventPlanner.Contracts.Budget;
using EventPlanner.Contracts.Guests;
using EventPlanner.Contracts.Results;
using EventPlanner.Core.Services.Budget;
using EventPlanner.Core.Services.Guests;
using EventPlanner.Desktop.Localization;
using EventPlanner.Desktop.ViewModels.Shell;

namespace EventPlanner.Desktop.ViewModels.Overview;

/// <summary>
/// Event dashboard. Only figures that the app actually knows are shown; unknown answers and
/// unassigned guests are listed as open issues instead of being hidden as zero.
/// </summary>
public sealed partial class OverviewViewModel(GuestService guests, BudgetService budget, ActiveProjectContext active) : ViewModelBase, IPage
{
    public string Title => Strings.Nav_Overview;

    public ObservableCollection<ProgramPartCountsDto> PartCounts { get; } = [];

    public ObservableCollection<string> Issues { get; } = [];

    [ObservableProperty]
    public partial bool HasProject { get; set; }

    [ObservableProperty]
    public partial string Heading { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DateText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? VenueText { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TotalGuestsText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? BudgetText { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public async Task ActivateAsync(CancellationToken cancellationToken)
    {
        var project = active.Project;
        HasProject = project is not null;
        PartCounts.Clear();
        Issues.Clear();
        if (project is null)
        {
            return;
        }

        Heading = project.Name;
        DateText = project.LocalStartDate == project.LocalEndDate
            ? string.Format(CultureInfo.CurrentCulture, Strings.Overview_DateSingle, project.LocalStartDate, project.TimeZoneId)
            : string.Format(CultureInfo.CurrentCulture, Strings.Overview_DateRange, project.LocalStartDate, project.LocalEndDate, project.TimeZoneId);
        VenueText = project.VenueText;
        StatusText = EnumText.For(project.Status);

        var countsTask = guests.GetCountsAsync(project.Id, cancellationToken);
        var budgetTask = budget.GetOverviewAsync(project.Id, cancellationToken);
        await Task.WhenAll(countsTask, budgetTask);
        var result = countsTask.Result;
        cancellationToken.ThrowIfCancellationRequested();
        if (active.Project?.Id != project.Id)
        {
            return; // Project switched while loading; drop the stale result.
        }

        if (!result.IsSuccess)
        {
            ErrorMessage = ErrorText.For(result.Error!);
            return;
        }

        ErrorMessage = null;
        var counts = result.Value;
        TotalGuestsText = string.Format(CultureInfo.CurrentCulture, Strings.Overview_TotalGuests, counts.TotalGuests);
        ShowBudget(budgetTask.Result);
        foreach (var part in counts.Parts)
        {
            PartCounts.Add(part);
        }

        if (project.ProgramParts.Count == 0)
        {
            Issues.Add(Strings.Overview_IssueNoProgramParts);
        }

        if (counts.GuestsWithoutProgramPart > 0)
        {
            Issues.Add(string.Format(CultureInfo.CurrentCulture, Strings.Overview_IssueGuestsWithoutPart, counts.GuestsWithoutProgramPart));
        }

        if (counts.GuestsAwaitingAnswer > 0)
        {
            Issues.Add(string.Format(CultureInfo.CurrentCulture, Strings.Overview_IssuePendingAnswers, counts.GuestsAwaitingAnswer));
        }
    }

    /// <summary>Plan / committed / paid in the project currency; overpayment and foreign-currency items become issues.</summary>
    private void ShowBudget(Result<BudgetOverviewDto> result)
    {
        if (!result.IsSuccess || (result.Value.ProjectTotals.ItemCount == 0 && result.Value.OtherCurrencyTotals.Count == 0))
        {
            BudgetText = null;
            return;
        }

        var totals = result.Value.ProjectTotals;
        BudgetText = string.Format(
            CultureInfo.CurrentCulture,
            Strings.Overview_Budget,
            MoneyText.Format(totals.EstimatedMinor, totals.Currency),
            MoneyText.Format(totals.CommittedMinor, totals.Currency),
            MoneyText.Format(totals.PaidNetMinor, totals.Currency),
            MoneyText.Format(totals.RemainingMinor, totals.Currency));

        if (totals.OverpaidMinor > 0)
        {
            Issues.Add(string.Format(CultureInfo.CurrentCulture, Strings.Overview_IssueOverpaid, MoneyText.Format(totals.OverpaidMinor, totals.Currency)));
        }

        foreach (var other in result.Value.OtherCurrencyTotals)
        {
            Issues.Add(string.Format(CultureInfo.CurrentCulture, Strings.Overview_IssueOtherCurrency, other.ItemCount, other.Currency));
        }
    }
}
