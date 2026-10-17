using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using EventPlanner.Contracts.Budget;
using EventPlanner.Contracts.Events;
using EventPlanner.Contracts.Guests;
using EventPlanner.Contracts.Vendors;
using EventPlanner.Desktop.Composition;
using EventPlanner.Desktop.Localization;
using EventPlanner.Desktop.ViewModels.Backup;
using EventPlanner.Desktop.ViewModels.Budget;
using EventPlanner.Desktop.ViewModels.Events;
using EventPlanner.Desktop.ViewModels.Guests;
using EventPlanner.Desktop.ViewModels.Overview;
using EventPlanner.Desktop.ViewModels.Shell;
using EventPlanner.Desktop.ViewModels.Vendors;
using EventPlanner.Desktop.Views;
using EventPlanner.Infrastructure.Profiles;
using EventPlanner.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventPlanner.Tests.Ui;

public sealed class ShellFlowTests
{
    /// <summary>Set to a directory to save rendered frames for manual visual review.</summary>
    private const string ScreenshotDirectoryVariable = "EVENTPLANNER_SCREENSHOTS";

    [AvaloniaFact]
    public async Task Organizer_creates_project_guests_and_group_rsvp_through_the_ui_layer()
    {
        var root = TestProfile.NewRoot();
        var window = new MainWindow { Width = 1280, Height = 800 };
        using var shell = new ShellViewModel(AppOptions.ForTests(root), NullLoggerFactory.Instance, new FakePlatformServices());
        window.DataContext = shell;
        window.Show();

        Assert.True(await shell.InitializeAsync(CancellationToken.None));
        var events = Assert.IsType<EventsViewModel>(shell.SelectedPage);
        await events.ActivateAsync(CancellationToken.None);

        // Validation errors are attached to fields and nothing is saved.
        await events.SaveCommand.ExecuteAsync(null);
        Assert.NotNull(events.Errors[nameof(EventProjectDto.LocalStartDate)]);

        events.Name = "Svadba Anny a Petra";
        events.StartDate = new DateTime(2027, 6, 12);
        events.EndDate = new DateTime(2027, 6, 13);
        events.TimeZoneId = "Europe/Bratislava";
        events.SelectedTemplate = events.TemplateOptions.Single(t => t.Value == EventTemplate.Wedding);
        await events.SaveCommand.ExecuteAsync(null);
        Assert.Null(events.ErrorMessage);
        Assert.Single(events.Projects);
        Assert.Equal(3, events.ProgramParts.Count);
        Capture(window, "01-projects");

        var guests = shell.Pages.OfType<GuestsViewModel>().Single();
        shell.SelectedPage = guests;
        await guests.ActivateAsync(CancellationToken.None);

        guests.NewGroupCommand.Execute(null);
        guests.GroupLabel = "Rodina Novákových";
        guests.GroupContactEmail = "novak@example.invalid";
        await guests.SaveGroupCommand.ExecuteAsync(null);
        var groupId = Assert.Single(guests.Groups).Id;

        foreach (var name in new[] { "Ján Novák", "Eva Nováková", "Tomáš Novák" })
        {
            guests.NewGuestCommand.Execute(null);
            guests.EditName = name;
            guests.EditGroup = guests.GroupOptions.Single(o => o.Value == groupId);
            guests.PartChoices[0].IsSelected = true;
            guests.PartChoices[1].IsSelected = name != "Tomáš Novák";
            await guests.SaveGuestCommand.ExecuteAsync(null);
            Assert.Null(guests.ErrorMessage);
        }

        guests.NewGuestCommand.Execute(null);
        guests.EditName = "Ján Novák";
        guests.EditPrivateNote = "Bezlepková strava";
        await guests.SaveGuestCommand.ExecuteAsync(null);
        Assert.NotNull(guests.WarningMessage); // duplicate name is a warning only
        Assert.Equal(4, guests.Guests.Count);

        // Group RSVP with an exception: Tomáš is unchecked and keeps his state.
        guests.SelectedGroup = guests.Groups[0];

        // Deleting the group asks first; cancelling keeps the group and its members.
        guests.RequestDeleteGroupCommand.Execute(null);
        Assert.True(guests.IsConfirmingGroupDelete);
        guests.CancelDeleteGroupCommand.Execute(null);
        Assert.False(guests.IsConfirmingGroupDelete);
        Assert.Single(guests.Groups);
        Assert.Equal(3, guests.GroupMembers.Count);
        guests.GroupMembers.Single(m => m.Label.StartsWith("Tomáš", StringComparison.Ordinal)).IsSelected = false;
        await guests.ApplyGroupRsvpCommand.ExecuteAsync(null);
        Assert.Null(guests.ErrorMessage);

        var ceremony = guests.Counts[0];
        Assert.Equal(2, ceremony.Accepted);
        Assert.Equal(1, ceremony.Invited); // Tomáš; the duplicate Ján has no program part

        // Private note is not loaded until explicitly revealed.
        guests.SelectedGuest = guests.Guests.Single(g => g.HasPrivateNote);
        Assert.False(guests.IsPrivateNoteRevealed);
        Assert.Null(guests.EditPrivateNote);
        await guests.RevealPrivateNoteCommand.ExecuteAsync(null);
        Assert.Equal("Bezlepková strava", guests.EditPrivateNote);
        Capture(window, "02-guests");

        var overview = shell.Pages.OfType<OverviewViewModel>().Single();
        shell.SelectedPage = overview;
        await overview.ActivateAsync(CancellationToken.None);
        Assert.Contains(overview.Issues, i => i.Contains('1', StringComparison.Ordinal)); // one guest without program part
        Capture(window, "03-overview");

        var backup = shell.Pages.OfType<BackupViewModel>().Single();
        shell.SelectedPage = backup;
        await backup.ActivateAsync(CancellationToken.None);
        Capture(window, "04-backup");

        window.Close();
        TryDelete(root);
    }

    [AvaloniaFact]
    public async Task Organizer_compares_quotes_selects_one_and_records_payments_through_the_ui_layer()
    {
        var root = TestProfile.NewRoot();
        var window = new MainWindow { Width = 1280, Height = 800 };
        using var shell = new ShellViewModel(AppOptions.ForTests(root), NullLoggerFactory.Instance, new FakePlatformServices());
        window.DataContext = shell;
        window.Show();
        Assert.True(await shell.InitializeAsync(CancellationToken.None));

        var events = shell.Pages.OfType<EventsViewModel>().Single();
        await events.ActivateAsync(CancellationToken.None);
        events.Name = "Oslava";
        events.StartDate = new DateTime(2027, 6, 12);
        events.EndDate = new DateTime(2027, 6, 12);
        events.TimeZoneId = "Europe/Bratislava";
        await events.SaveCommand.ExecuteAsync(null);
        Assert.Null(events.ErrorMessage);

        var vendors = shell.Pages.OfType<VendorsViewModel>().Single();
        shell.SelectedPage = vendors;
        await vendors.ActivateAsync(CancellationToken.None);
        foreach (var name in new[] { "Kvetinárstvo", "Praha dekor" })
        {
            vendors.NewVendorCommand.Execute(null);
            vendors.VendorName = name;
            vendors.VendorCategory = "Výzdoba";
            await vendors.SaveVendorCommand.ExecuteAsync(null);
            Assert.Null(vendors.ErrorMessage);
        }

        // EUR quote without transport information, and a complete CZK quote.
        await AddQuoteAsync(vendors, "Kvetinárstvo", "300", "EUR", TransportCoverage.Unknown);
        await AddQuoteAsync(vendors, "Praha dekor", "9000", "CZK", TransportCoverage.Included);
        Assert.Equal(2, vendors.Quotes.Count);
        Assert.NotNull(vendors.ComparisonSummary); // two currencies: no shared ranking or total
        var eurRow = vendors.Quotes.Single(q => q.Quote.Currency == "EUR");
        Assert.Equal(Strings.Vendors_TotalIncomplete, eurRow.TotalText);
        Assert.True(vendors.Quotes.Single(q => q.Quote.Currency == "CZK").IsForeignCurrency);

        // An unparsable amount is a field error, nothing is saved.
        vendors.NewQuoteCommand.Execute(null);
        vendors.QuoteVendor = vendors.VendorOptions.Single(o => o.Label.StartsWith("Kvetinárstvo", StringComparison.Ordinal));
        vendors.QuoteAmount = "12,345";
        await vendors.SaveQuoteCommand.ExecuteAsync(null);
        Assert.NotNull(vendors.Errors[nameof(QuoteDto.AmountMinor)]);
        Assert.Equal(2, vendors.Quotes.Count);

        vendors.SelectedQuote = vendors.Quotes.Single(q => q.Quote.Currency == "EUR");
        vendors.SelectionPolicy = vendors.PolicyOptions.Single(o => o.Value == BudgetImpactPolicy.CreateBudgetItem);
        vendors.DecisionNote = "Overená kvalita";
        await vendors.SelectQuoteCommand.ExecuteAsync(null);
        Assert.Null(vendors.ErrorMessage);
        Assert.NotNull(vendors.WarningMessage); // committed amount is incomplete (transport unknown)
        Assert.True(vendors.Quotes.Single(q => q.Quote.Currency == "EUR").IsSelected);

        // Deleting a vendor (with its quotes and history) asks first; cancelling keeps everything.
        vendors.SelectedVendor = vendors.Vendors.Single(v => v.Name == "Praha dekor");
        vendors.RequestDeleteVendorCommand.Execute(null);
        Assert.True(vendors.IsConfirmingVendorDelete);
        vendors.CancelDeleteCommand.Execute(null);
        Assert.False(vendors.IsConfirmingVendorDelete);
        Assert.Equal(2, vendors.Vendors.Count);
        vendors.SelectedQuote = vendors.Quotes.Single(q => q.Quote.Currency == "EUR");
        Assert.False(vendors.IsConfirmingVendorDelete);

        // Reloading while the bound combo boxes are shown must keep their choices (a cleared list
        // makes the ComboBox write null back, which once crashed the next reload).
        vendors.SelectionPolicy = vendors.PolicyOptions.Single(o => o.Value == BudgetImpactPolicy.UpdateBudgetItem);
        Dispatcher.UIThread.RunJobs();
        await vendors.ActivateAsync(CancellationToken.None);
        await vendors.ActivateAsync(CancellationToken.None);
        Assert.NotNull(vendors.SelectionBudgetItem);
        Assert.NotNull(vendors.QuoteVendor);
        vendors.SelectionPolicy = vendors.PolicyOptions[0];

        // The reloaded detail shows the decision with its reason and an incomplete price.
        await vendors.DecisionsLoad;
        Assert.True(vendors.ShowDecisions);
        Assert.Null(vendors.DecisionNote);
        var decision = Assert.Single(vendors.Decisions);
        Assert.Equal("Overená kvalita", decision.Note);
        Assert.Contains(Strings.Vendors_TotalIncomplete, decision.PriceText, StringComparison.Ordinal);
        Capture(window, "06-vendors");

        var budget = shell.Pages.OfType<BudgetViewModel>().Single();
        shell.SelectedPage = budget;
        await budget.ActivateAsync(CancellationToken.None);
        budget.SelectedItem = Assert.Single(budget.Items);
        budget.ItemEstimated = "1000";
        budget.ItemCommitted = "900";
        await budget.SaveItemCommand.ExecuteAsync(null);
        Assert.Null(budget.ErrorMessage);

        await RecordPaymentAsync(budget, PaymentKind.Deposit, PaymentStatus.Posted, "200");
        await RecordPaymentAsync(budget, PaymentKind.Balance, PaymentStatus.Planned, "700");
        Assert.Equal(MoneyText.Format(700_00, "EUR"), budget.ProjectTotals!.ScheduledText);

        budget.SelectedPayment = budget.Payments.Single(p => p.Payment.Status == PaymentStatus.Planned);
        Assert.True(budget.CanPostSelectedPayment);
        await budget.PostSelectedPaymentCommand.ExecuteAsync(null);
        await RecordPaymentAsync(budget, PaymentKind.Refund, PaymentStatus.Posted, "50");

        var totals = budget.ProjectTotals!;
        Assert.Equal(MoneyText.Format(850_00, "EUR"), totals.PaidText);
        Assert.Equal(MoneyText.Format(50_00, "EUR"), totals.RemainingText);
        Assert.Equal(MoneyText.Format(0, "EUR"), totals.ScheduledText);

        // Paying more than committed is shown as an overpayment, not as zero remaining.
        await RecordPaymentAsync(budget, PaymentKind.Balance, PaymentStatus.Posted, "100");
        Assert.True(budget.Items.Single().IsOverpaid);
        Assert.Equal(
            string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Budget_Overpaid, MoneyText.Format(50_00, "EUR")),
            budget.Items.Single().BalanceText);
        Assert.True(budget.ProjectTotals!.HasOverpaid);
        Capture(window, "07-budget");

        window.Close();
        TryDelete(root);
    }

    [AvaloniaFact]
    public async Task Switching_project_in_the_header_reloads_the_open_tab()
    {
        var root = TestProfile.NewRoot();
        var window = new MainWindow { Width = 1280, Height = 800 };
        using var shell = new ShellViewModel(AppOptions.ForTests(root), NullLoggerFactory.Instance, new FakePlatformServices());
        window.DataContext = shell;
        window.Show();
        Assert.True(await shell.InitializeAsync(CancellationToken.None));

        // Without any project the app opens on the project editor, outside the tabs.
        var projects = shell.ProjectsPage!;
        Assert.Same(projects, shell.SelectedPage);
        Assert.True(shell.IsProjectsOpen);
        Assert.Null(shell.SelectedTab);
        Assert.DoesNotContain(projects, shell.TabPages);

        foreach (var name in new[] { "Svadba", "Krstiny" })
        {
            shell.NewProjectCommand.Execute(null);
            projects.Name = name;
            projects.StartDate = new DateTime(2027, 6, 12);
            projects.EndDate = new DateTime(2027, 6, 12);
            projects.TimeZoneId = "Europe/Bratislava";
            await projects.SaveCommand.ExecuteAsync(null);
            Assert.Null(projects.ErrorMessage);
        }

        var guests = shell.Pages.OfType<GuestsViewModel>().Single();
        shell.SelectedTab = guests;
        await guests.ActivateAsync(CancellationToken.None);
        guests.NewGuestCommand.Execute(null);
        guests.EditName = "Hosť na krstinách";
        await guests.SaveGuestCommand.ExecuteAsync(null);
        Assert.Single(guests.Guests);
        Assert.StartsWith("Krstiny", shell.ProjectTitle, StringComparison.Ordinal);

        // Picking the other project in the switcher (bound to the projects page) reloads the guests tab.
        projects.SelectedProject = projects.Projects.Single(p => p.Name == "Svadba");
        await WaitUntilAsync(() => shell.ProjectTitle.StartsWith("Svadba", StringComparison.Ordinal) && guests.Guests.Count == 0);
        Assert.Same(guests, shell.SelectedTab);

        shell.OpenBackupCommand.Execute(null);
        Assert.True(shell.IsBackupOpen);
        Assert.Null(shell.SelectedTab);

        // "New project" from another page must open an empty form, also when used repeatedly; loading
        // the projects page must not put the active project back into the editor.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            shell.SelectedTab = guests;
            shell.NewProjectCommand.Execute(null);
            await SettleAsync();
            Assert.True(projects.IsNew);
            Assert.Equal(string.Empty, projects.Name);
            Assert.Null(projects.SelectedProject);
        }

        // Picking a project afterwards still loads it into the editor.
        projects.SelectedProject = projects.Projects.Single(p => p.Name == "Krstiny");
        await WaitUntilAsync(() => !projects.IsNew && projects.Name == "Krstiny");

        // Deleting needs the typed name; afterwards the remaining project becomes active.
        projects.RequestDeleteCommand.Execute(null);
        Assert.False(projects.CanConfirmDelete);
        projects.DeleteConfirmationText = "Krstiny";
        Assert.True(projects.CanConfirmDelete);
        await projects.ConfirmDeleteCommand.ExecuteAsync(null);
        Assert.Null(projects.ErrorMessage);
        Assert.Equal(["Svadba"], projects.Projects.Select(p => p.Name));
        await WaitUntilAsync(() => shell.ProjectName == "Svadba");

        // Deleting the last project leaves no active project and an empty editor.
        projects.RequestDeleteCommand.Execute(null);
        projects.DeleteConfirmationText = "Svadba";
        await projects.ConfirmDeleteCommand.ExecuteAsync(null);
        Assert.Empty(projects.Projects);
        Assert.True(projects.IsNew);
        Assert.Null(shell.ProjectName);
        Assert.False(shell.ShowProjectBar);

        window.Close();
        TryDelete(root);
    }

    [AvaloniaFact]
    public async Task Second_start_of_an_open_profile_brings_the_running_window_forward_and_exits()
    {
        var root = TestProfile.NewRoot();
        using var holder = ProfileLock.TryAcquire(root.ProfileDirectory(DataRoot.DefaultProfileName));
        var platform = new FakePlatformServices { RunningInstanceAnswers = true };
        using var shell = new ShellViewModel(AppOptions.ForTests(root), NullLoggerFactory.Instance, platform);

        Assert.False(await shell.InitializeAsync(CancellationToken.None));

        Assert.Equal(0, platform.ShutdownCode);
        Assert.False(shell.IsFailed);
        Assert.Null(platform.ListeningFor);
        TryDelete(root);
    }

    [AvaloniaFact]
    public async Task Opened_profile_listens_for_later_starts()
    {
        var root = TestProfile.NewRoot();
        var platform = new FakePlatformServices();
        using var shell = new ShellViewModel(AppOptions.ForTests(root), NullLoggerFactory.Instance, platform);

        Assert.True(await shell.InitializeAsync(CancellationToken.None));

        Assert.Equal(root.ProfileDirectory(DataRoot.DefaultProfileName), platform.ListeningFor);
        shell.Dispose();
        TryDelete(root);
    }

    [AvaloniaFact]
    public async Task Locked_profile_shows_failure_without_touching_data()
    {
        var root = TestProfile.NewRoot();
        var profileDir = root.ProfileDirectory(DataRoot.DefaultProfileName);
        using (var holder = ProfileLock.TryAcquire(profileDir))
        {
            var window = new MainWindow();
            using var shell = new ShellViewModel(AppOptions.ForTests(root), NullLoggerFactory.Instance, new FakePlatformServices());
            window.DataContext = shell;
            window.Show();

            Assert.False(await shell.InitializeAsync(CancellationToken.None));
            Assert.True(shell.IsFailed);
            Assert.False(shell.CanRecoverFromBackup);
            Assert.Empty(shell.Pages);
            Capture(window, "05-locked");
            window.Close();
        }

        TryDelete(root);
    }

    private static void Capture(MainWindow window, string name)
    {
        var directory = Environment.GetEnvironmentVariable(ScreenshotDirectoryVariable);
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Directory.CreateDirectory(directory);
        window.CaptureRenderedFrame()?.Save(Path.Combine(directory, name + ".png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    private static async Task AddQuoteAsync(VendorsViewModel vendors, string vendorName, string amount, string currency, TransportCoverage transport)
    {
        vendors.NewQuoteCommand.Execute(null);
        vendors.QuoteVendor = vendors.VendorOptions.Single(o => o.Label.StartsWith(vendorName, StringComparison.Ordinal));
        vendors.QuoteDescription = "Výzdoba sály";
        vendors.QuoteAmount = amount;
        vendors.QuoteCurrency = currency;
        vendors.QuoteScope = "kvety, stuhy";
        vendors.QuoteTransport = vendors.TransportOptions.Single(o => o.Value == transport);
        await vendors.SaveQuoteCommand.ExecuteAsync(null);
        Assert.Null(vendors.ErrorMessage);
    }

    private static async Task RecordPaymentAsync(BudgetViewModel budget, PaymentKind kind, PaymentStatus status, string amount)
    {
        budget.NewPaymentKind = budget.KindOptions.Single(o => o.Value == kind);
        budget.NewPaymentStatus = budget.StatusOptions.Single(o => o.Value == status);
        budget.PaymentAmount = amount;
        await budget.RecordPaymentCommand.ExecuteAsync(null);
        Assert.Null(budget.ErrorMessage);
    }

    /// <summary>Lets background page loads started by the shell finish (for asserting that nothing changes).</summary>
    private static async Task SettleAsync()
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
    }

    /// <summary>Page loads started by the shell run in the background; this waits for their effect.</summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 200 && !condition(); attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        Assert.True(condition(), "The expected state was not reached in time.");
    }

    private static void TryDelete(DataRoot root)
    {
        try
        {
            Directory.Delete(root.Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
