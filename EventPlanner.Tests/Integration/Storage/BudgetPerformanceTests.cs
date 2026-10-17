using System.Diagnostics;
using EventPlanner.Contracts.Budget;
using EventPlanner.Core.Models.Budget;
using EventPlanner.Tests.Fixtures;

namespace EventPlanner.Tests.Integration.Storage;

/// <summary>
/// E-A09 budget slice: 1000 payments. The bound is a regression guard (N+1 queries, per-payment
/// round trips), not a benchmark; measured times are written to the test output.
/// </summary>
public sealed class BudgetPerformanceTests
{
    private const int ItemCount = 50;
    private const int PaymentsPerItem = 20;
    private static readonly TimeSpan OverviewBudget = TimeSpan.FromSeconds(1);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Overview_of_1000_payments_loads_within_budget_and_sums_exactly()
    {
        await using var profile = await TestProfile.CreateAsync();
        var project = (await profile.Events.CreateAsync(Commands.Wedding(), Ct)).Value;

        // Payments are seeded in one transaction: 1000 separate commands would only measure fsync.
        await using (var context = profile.Profile.Database.CreateContext())
        {
            for (var i = 0; i < ItemCount; i++)
            {
                var item = (await profile.Budget.CreateItemAsync(
                    new CreateBudgetItem(project.Id, "Kategória " + (i % 5), "Položka " + i, 1000_00, 1000_00, "EUR"), Ct)).Value;
                for (var p = 0; p < PaymentsPerItem; p++)
                {
                    var posted = p % 2 == 0;
                    context.Payments.Add(new Payment(
                        Guid.CreateVersion7(), item.Id, Guid.NewGuid(), PaymentKind.Deposit, 10_01,
                        posted ? PaymentStatus.Posted : PaymentStatus.Planned, posted ? new DateOnly(2027, 1, 1) : null, null, null));
                }
            }

            await context.SaveChangesAsync(Ct);
        }

        await profile.ReopenAsync();
        var cold = Stopwatch.StartNew();
        var overview = (await profile.Budget.GetOverviewAsync(project.Id, Ct)).Value;
        cold.Stop();
        var warm = Stopwatch.StartNew();
        overview = (await profile.Budget.GetOverviewAsync(project.Id, Ct)).Value;
        warm.Stop();
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Budget overview, {ItemCount * PaymentsPerItem} payments: cold {cold.ElapsedMilliseconds} ms, warm {warm.ElapsedMilliseconds} ms");

        Assert.Equal(ItemCount * PaymentsPerItem, overview.Items.Sum(i => i.Payments.Count));
        var totals = overview.ProjectTotals;
        const long postedPerItem = PaymentsPerItem / 2 * 10_01;
        Assert.Equal(ItemCount * postedPerItem, totals.PaidNetMinor);
        Assert.Equal(ItemCount * postedPerItem, totals.ScheduledMinor);
        Assert.Equal(ItemCount * (1000_00 - postedPerItem), totals.RemainingMinor);
        Assert.True(warm.Elapsed < OverviewBudget, $"Warm overview took {warm.ElapsedMilliseconds} ms.");
    }
}
