using EventPlanner.Contracts.Budget;
using EventPlanner.Contracts.Results;
using EventPlanner.Contracts.Vendors;
using EventPlanner.Tests.Fixtures;

namespace EventPlanner.Tests.Integration.Storage;

public sealed class QuoteComparisonServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>E-A05 over SQLite: two currencies and missing transport, then selection into the budget.</summary>
    [Fact]
    public async Task Quotes_in_two_currencies_with_missing_transport_never_produce_a_shared_total()
    {
        await using var profile = await TestProfile.CreateAsync();
        var project = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;
        var florist = await VendorAsync(profile, project.Id, "Kvetinárstvo");
        var prague = await VendorAsync(profile, project.Id, "Praha dekor");
        var noTransport = (await profile.Vendors.AddQuoteAsync(
            new AddQuote(florist.Id, Commands.Quote(300_00) with { Transport = TransportCoverage.Unknown, ExclusionsText = "  " }), Ct)).Value;
        var czk = (await profile.Vendors.AddQuoteAsync(new AddQuote(prague.Id, Commands.Quote(9000_00, "CZK")), Ct)).Value;

        await profile.ReopenAsync();
        var comparison = (await profile.Quotes.CompareQuotesAsync(project.Id, "výzdoba", Ct)).Value;

        Assert.Equal(["EUR", "CZK"], comparison.Currencies);
        var eurRow = comparison.Rows.Single(r => r.Quote.Id == noTransport.Id);
        Assert.Null(eurRow.Quote.KnownTotalMinor);
        Assert.Null(eurRow.Quote.ExclusionsText); // blank is "not stated", not "nothing excluded"
        Assert.Contains(QuoteFields.Transport, eurRow.MissingFields);
        Assert.Contains(QuoteFields.Exclusions, eurRow.MissingFields);
        Assert.False(comparison.Rows.Single(r => r.Quote.Id == czk.Id).IsProjectCurrency);

        // Selecting the incomplete EUR quote commits only the known amount and says so.
        var selected = await profile.Quotes.SelectQuoteAsync(new SelectQuote(noTransport.Id, noTransport.Revision, BudgetImpactPolicy.CreateBudgetItem), Ct);
        Assert.Contains(selected.Warnings, w => w.Code == WarningCodes.QuoteTotalIncomplete);

        // Selecting the CZK quote into the budget keeps it in its own currency, outside project totals.
        var czkSelection = await profile.Quotes.SelectQuoteAsync(new SelectQuote(czk.Id, czk.Revision, BudgetImpactPolicy.CreateBudgetItem), Ct);
        Assert.Contains(czkSelection.Warnings, w => w.Code == WarningCodes.ForeignCurrencyNotInTotals);

        var budget = (await profile.Budget.GetOverviewAsync(project.Id, Ct)).Value;
        Assert.Equal(300_00, budget.ProjectTotals.CommittedMinor);
        var other = Assert.Single(budget.OtherCurrencyTotals);
        Assert.Equal(("CZK", 9000_00L), (other.Currency, other.CommittedMinor));
    }

    [Fact]
    public async Task Selecting_another_quote_in_category_returns_the_previous_one_to_received()
    {
        await using var profile = await TestProfile.CreateAsync();
        var project = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;
        var a = (await profile.Vendors.AddQuoteAsync(new AddQuote((await VendorAsync(profile, project.Id, "A")).Id, Commands.Quote(100_00)), Ct)).Value;
        var b = (await profile.Vendors.AddQuoteAsync(new AddQuote((await VendorAsync(profile, project.Id, "B")).Id, Commands.Quote(200_00)), Ct)).Value;

        await profile.Quotes.SelectQuoteAsync(new SelectQuote(a.Id, a.Revision, BudgetImpactPolicy.LeaveBudgetUnchanged), Ct);
        var second = await profile.Quotes.SelectQuoteAsync(new SelectQuote(b.Id, b.Revision, BudgetImpactPolicy.LeaveBudgetUnchanged), Ct);

        Assert.True(second.IsSuccess);
        Assert.Null(second.Value.BudgetItemId);
        var statuses = (await profile.Vendors.ListQuotesAsync(project.Id, Ct)).ToDictionary(q => q.Id, q => q.Status);
        Assert.Equal(QuoteStatus.Received, statuses[a.Id]);
        Assert.Equal(QuoteStatus.Selected, statuses[b.Id]);
        Assert.Empty((await profile.Budget.GetOverviewAsync(project.Id, Ct)).Value.Items);
    }

    [Fact]
    public async Task Changed_selected_quote_does_not_flow_into_budget_without_explicit_update()
    {
        await using var profile = await TestProfile.CreateAsync();
        var project = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;
        var vendor = await VendorAsync(profile, project.Id, "Kvetinárstvo");
        var quote = (await profile.Vendors.AddQuoteAsync(new AddQuote(vendor.Id, Commands.Quote(400_00)), Ct)).Value;
        var selection = (await profile.Quotes.SelectQuoteAsync(new SelectQuote(quote.Id, quote.Revision, BudgetImpactPolicy.CreateBudgetItem), Ct)).Value;

        var edited = await profile.Vendors.UpdateQuoteAsync(new UpdateQuote(quote.Id, selection.Quote.Revision, Commands.Quote(450_00)), Ct);

        Assert.Contains(edited.Warnings, w => w.Code == WarningCodes.LinkedBudgetNotUpdated);
        var item = (await profile.Budget.GetOverviewAsync(project.Id, Ct)).Value.Items.Single().Item;
        Assert.Equal(400_00, item.CommittedMinor);

        // The organizer decides explicitly; only then the committed amount follows the quote.
        var update = await profile.Quotes.SelectQuoteAsync(
            new SelectQuote(quote.Id, edited.Value.Revision, BudgetImpactPolicy.UpdateBudgetItem, item.Id, item.Revision), Ct);
        Assert.True(update.IsSuccess, update.Error?.Detail);
        Assert.Equal(450_00, (await profile.Budget.GetOverviewAsync(project.Id, Ct)).Value.Items.Single().Item.CommittedMinor);
    }

    [Fact]
    public async Task Update_policy_rejects_other_currency_stale_revision_and_cross_event_item()
    {
        await using var profile = await TestProfile.CreateAsync();
        var project = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;
        var other = (await profile.Events.CreateAsync(Commands.Wedding("Iný projekt"), Ct)).Value;
        var vendor = await VendorAsync(profile, project.Id, "Kvetinárstvo");
        var czk = (await profile.Vendors.AddQuoteAsync(new AddQuote(vendor.Id, Commands.Quote(9000_00, "CZK")), Ct)).Value;
        var eur = (await profile.Vendors.AddQuoteAsync(new AddQuote(vendor.Id, Commands.Quote(300_00)), Ct)).Value;
        var eurItem = (await profile.Budget.CreateItemAsync(new CreateBudgetItem(project.Id, "Výzdoba", "Kvety", 500_00, null, "EUR"), Ct)).Value;
        var foreignEventItem = (await profile.Budget.CreateItemAsync(new CreateBudgetItem(other.Id, "Výzdoba", "Kvety", null, null, "EUR"), Ct)).Value;

        var mismatch = await profile.Quotes.SelectQuoteAsync(new SelectQuote(czk.Id, czk.Revision, BudgetImpactPolicy.UpdateBudgetItem, eurItem.Id, eurItem.Revision), Ct);
        var stale = await profile.Quotes.SelectQuoteAsync(new SelectQuote(eur.Id, eur.Revision, BudgetImpactPolicy.UpdateBudgetItem, eurItem.Id, eurItem.Revision + 5), Ct);
        var crossEvent = await profile.Quotes.SelectQuoteAsync(
            new SelectQuote(eur.Id, eur.Revision, BudgetImpactPolicy.UpdateBudgetItem, foreignEventItem.Id, foreignEventItem.Revision), Ct);
        var staleQuote = await profile.Quotes.SelectQuoteAsync(new SelectQuote(eur.Id, eur.Revision + 1, BudgetImpactPolicy.LeaveBudgetUnchanged), Ct);

        Assert.Contains(mismatch.Error!.Issues, i => i.Rule == ValidationRules.CurrencyMismatch);
        Assert.Equal(ErrorCode.Conflict, stale.Error?.Code);
        Assert.Contains(crossEvent.Error!.Issues, i => i.Rule == ValidationRules.UnknownReference);
        Assert.Equal(ErrorCode.Conflict, staleQuote.Error?.Code);

        // Nothing was written by the refused commands.
        var item = (await profile.Budget.GetOverviewAsync(project.Id, Ct)).Value.Items.Single().Item;
        Assert.Equal((eurItem.Revision, (long?)null, (Guid?)null), (item.Revision, item.CommittedMinor, item.SelectedQuoteId));
        Assert.All(await profile.Vendors.ListQuotesAsync(project.Id, Ct), q => Assert.Equal(QuoteStatus.Received, q.Status));
    }

    [Fact]
    public async Task Deleting_vendor_keeps_budget_item_and_clears_quote_link()
    {
        await using var profile = await TestProfile.CreateAsync();
        var project = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;
        var vendor = await VendorAsync(profile, project.Id, "Kvetinárstvo");
        var quote = (await profile.Vendors.AddQuoteAsync(new AddQuote(vendor.Id, Commands.Quote(400_00)), Ct)).Value;
        await profile.Quotes.SelectQuoteAsync(new SelectQuote(quote.Id, quote.Revision, BudgetImpactPolicy.CreateBudgetItem), Ct);

        Assert.True((await profile.Vendors.DeleteVendorAsync(new DeleteVendor(vendor.Id, vendor.Revision), Ct)).IsSuccess);

        var item = (await profile.Budget.GetOverviewAsync(project.Id, Ct)).Value.Items.Single().Item;
        Assert.Equal(400_00, item.CommittedMinor);
        Assert.Null(item.SelectedQuoteId);
        Assert.Empty(await profile.Vendors.ListQuotesAsync(project.Id, Ct));
    }

    [Fact]
    public async Task Stale_vendor_edit_or_delete_is_a_conflict_and_keeps_the_newer_data()
    {
        await using var profile = await TestProfile.CreateAsync();
        var project = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;
        var original = await VendorAsync(profile, project.Id, "Kvetinárstvo");

        // Two windows opened the same vendor; the first save wins, the second must not overwrite it.
        var first = await profile.Vendors.UpdateVendorAsync(
            new UpdateVendor(original.Id, original.Revision, original.Category, "Kvetinárstvo Ruža", null, null), Ct);
        var second = await profile.Vendors.UpdateVendorAsync(
            new UpdateVendor(original.Id, original.Revision, original.Category, "Kvetinárstvo Tulipán", null, null), Ct);
        var staleDelete = await profile.Vendors.DeleteVendorAsync(new DeleteVendor(original.Id, original.Revision), Ct);

        Assert.Equal(original.Revision + 1, first.Value.Revision);
        Assert.Equal(ErrorCode.Conflict, second.Error?.Code);
        Assert.Equal(ErrorCode.Conflict, staleDelete.Error?.Code);
        var stored = Assert.Single(await profile.Vendors.ListVendorsAsync(project.Id, Ct));
        Assert.Equal(("Kvetinárstvo Ruža", first.Value.Revision), (stored.Name, stored.Revision));
    }

    [Fact]
    public async Task Decisions_are_recorded_with_price_snapshot_and_survive_quote_edits()
    {
        await using var profile = await TestProfile.CreateAsync();
        var project = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;
        var vendor = await VendorAsync(profile, project.Id, "Kvetinárstvo");
        var first = (await profile.Vendors.AddQuoteAsync(new AddQuote(vendor.Id, Commands.Quote(400_00) with { Description = "Základ" }), Ct)).Value;
        var second = (await profile.Vendors.AddQuoteAsync(
            new AddQuote(vendor.Id, Commands.Quote(500_00, transport: TransportCoverage.Unknown) with { Description = "Premium" }), Ct)).Value;

        Assert.True((await profile.Quotes.SelectQuoteAsync(
            new SelectQuote(first.Id, first.Revision, BudgetImpactPolicy.CreateBudgetItem, Note: "  najlepší pomer  "), Ct)).IsSuccess);
        var selectedSecond = (await profile.Quotes.SelectQuoteAsync(
            new SelectQuote(second.Id, second.Revision, BudgetImpactPolicy.LeaveBudgetUnchanged), Ct)).Value.Quote;
        Assert.True((await profile.Vendors.DeclineQuoteAsync(new DeclineQuote(selectedSecond.Id, selectedSecond.Revision, " "), Ct)).IsSuccess);

        // Editing the quote later must not rewrite what was decided.
        var reloaded = (await profile.Vendors.ListQuotesAsync(project.Id, Ct)).Single(q => q.Id == first.Id);
        Assert.True((await profile.Vendors.UpdateQuoteAsync(new UpdateQuote(first.Id, reloaded.Revision, Commands.Quote(999_00)), Ct)).IsSuccess);

        await profile.ReopenAsync();
        var history = await profile.Vendors.ListDecisionsAsync(vendor.Id, Ct);
        Assert.Equal(
            [
                (second.Id, QuoteDecisionKind.Declined),
                (second.Id, QuoteDecisionKind.Selected),
                (first.Id, QuoteDecisionKind.Replaced),
                (first.Id, QuoteDecisionKind.Selected),
            ],
            history.Select(d => (d.QuoteId, d.Kind)));

        var firstSelection = history[^1];
        // The price is the snapshot; the description is the quote's current label (it identifies the quote).
        Assert.Equal(("Výzdoba sály", 400_00L, (long?)400_00, "EUR"), (firstSelection.QuoteDescription, firstSelection.AmountMinor, firstSelection.KnownTotalMinor, firstSelection.Currency));
        Assert.Equal((BudgetImpactPolicy.CreateBudgetItem, "najlepší pomer"), (firstSelection.BudgetImpact, firstSelection.Note));
        Assert.Null(history[1].KnownTotalMinor); // transport unknown: incomplete price, not a pretended total
        Assert.Null(history[0].Note); // blank reason is "not stated"
        Assert.Null(history[0].BudgetImpact);
    }

    [Fact]
    public async Task Repeated_decline_adds_no_history_and_overlong_note_is_rejected()
    {
        await using var profile = await TestProfile.CreateAsync();
        var project = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;
        var vendor = await VendorAsync(profile, project.Id, "Kvetinárstvo");
        var quote = (await profile.Vendors.AddQuoteAsync(new AddQuote(vendor.Id, Commands.Quote(400_00)), Ct)).Value;

        var tooLong = await profile.Quotes.SelectQuoteAsync(
            new SelectQuote(quote.Id, quote.Revision, BudgetImpactPolicy.LeaveBudgetUnchanged, Note: new string('x', 2001)), Ct);
        var declined = (await profile.Vendors.DeclineQuoteAsync(new DeclineQuote(quote.Id, quote.Revision), Ct)).Value;
        Assert.True((await profile.Vendors.DeclineQuoteAsync(new DeclineQuote(declined.Id, declined.Revision), Ct)).IsSuccess);

        Assert.Equal(nameof(QuoteDecisionDto.Note), Assert.Single(tooLong.Error!.Issues).Field);
        Assert.Equal(QuoteDecisionKind.Declined, Assert.Single(await profile.Vendors.ListDecisionsAsync(vendor.Id, Ct)).Kind);
    }

    [Fact]
    public async Task Archived_event_refuses_vendor_changes_and_selection()
    {
        await using var profile = await TestProfile.CreateAsync();
        var project = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;
        var vendor = await VendorAsync(profile, project.Id, "Kvetinárstvo");
        var quote = (await profile.Vendors.AddQuoteAsync(new AddQuote(vendor.Id, Commands.Quote(400_00)), Ct)).Value;
        await BudgetServiceTests.ArchiveAsync(profile, project.Id);

        var select = await profile.Quotes.SelectQuoteAsync(new SelectQuote(quote.Id, quote.Revision, BudgetImpactPolicy.LeaveBudgetUnchanged), Ct);
        var add = await profile.Vendors.AddQuoteAsync(new AddQuote(vendor.Id, Commands.Quote(1_00)), Ct);

        Assert.Equal(ErrorCode.ReadOnly, select.Error?.Code);
        Assert.Equal(ErrorCode.ReadOnly, add.Error?.Code);
    }

    [Fact]
    public async Task Quote_validation_requires_scope_and_rejects_negative_amounts()
    {
        await using var profile = await TestProfile.CreateAsync();
        var project = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;
        var vendor = await VendorAsync(profile, project.Id, "Kvetinárstvo");

        var result = await profile.Vendors.AddQuoteAsync(
            new AddQuote(vendor.Id, Commands.Quote(-1) with { ScopeText = " ", Currency = "euro", PeopleCount = 0 }), Ct);

        var rules = result.Error!.Issues.ToDictionary(i => i.Field, i => i.Rule);
        Assert.Equal(ValidationRules.AmountOutOfRange, rules[nameof(QuoteDto.AmountMinor)]);
        Assert.Equal(ValidationRules.Required, rules[nameof(QuoteDto.ScopeText)]);
        Assert.Equal(ValidationRules.InvalidCurrency, rules[nameof(QuoteDto.Currency)]);
        Assert.Equal(ValidationRules.InvalidValue, rules[nameof(QuoteDto.PeopleCount)]);
    }

    private static async Task<VendorDto> VendorAsync(TestProfile profile, Guid eventId, string name) =>
        (await profile.Vendors.CreateVendorAsync(new CreateVendor(eventId, "Výzdoba", name, null, null), Ct)).Value;
}
