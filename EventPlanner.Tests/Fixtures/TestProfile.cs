using EventPlanner.Core.Services.Budget;
using EventPlanner.Core.Services.Events;
using EventPlanner.Core.Services.Guests;
using EventPlanner.Core.Services.Tasks;
using EventPlanner.Core.Services.Vendors;
using EventPlanner.Infrastructure.Data;
using EventPlanner.Infrastructure.Profiles;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventPlanner.Tests.Fixtures;

/// <summary>
/// A real SQLite profile in an isolated temp data root. Never touches the user's profile;
/// cleanup is limited to the directory this fixture created.
/// </summary>
public sealed class TestProfile : IAsyncDisposable
{
    private TestProfile(DataRoot root, OpenedProfile profile)
    {
        Root = root;
        Profile = profile;
        Sessions = new DataSessionFactory(profile.Database);
        Events = new EventProjectService(Sessions, TimeProvider.System, NullLogger<EventProjectService>.Instance);
        Guests = new GuestService(Sessions, NullLogger<GuestService>.Instance);
        Vendors = new VendorService(Sessions, TimeProvider.System);
        Quotes = new QuoteComparisonService(Sessions, TimeProvider.System, NullLogger<QuoteComparisonService>.Instance);
        Budget = new BudgetService(Sessions, NullLogger<BudgetService>.Instance);
        Tasks = new TaskService(Sessions, TimeProvider.System, NullLogger<TaskService>.Instance);
    }

    public DataRoot Root { get; }

    public OpenedProfile Profile { get; private set; }

    public DataSessionFactory Sessions { get; private set; }

    public EventProjectService Events { get; private set; }

    public GuestService Guests { get; private set; }

    public VendorService Vendors { get; private set; }

    public QuoteComparisonService Quotes { get; private set; }

    public BudgetService Budget { get; private set; }

    public TaskService Tasks { get; private set; }

    public static ProfileOpener Opener() => new(TimeProvider.System, NullLogger<ProfileOpener>.Instance);

    public static DataRoot NewRoot() =>
        new(Path.Combine(Path.GetTempPath(), "eventplanner-tests", Guid.NewGuid().ToString("N")));

    public static async Task<TestProfile> CreateAsync()
    {
        var root = NewRoot();
        var result = await Opener().OpenAsync(DataRoot.DefaultProfileName, root.ProfileDirectory(DataRoot.DefaultProfileName), CancellationToken.None);
        Assert.Equal(ProfileOpenStatus.Ready, result.Status);
        return new TestProfile(root, result.Profile!);
    }

    /// <summary>Closes and reopens the profile so tests verify persisted state, not memory.</summary>
    public async Task ReopenAsync()
    {
        Profile.Dispose();
        var result = await Opener().OpenAsync(Profile.Name, Profile.Directory, CancellationToken.None);
        Assert.Equal(ProfileOpenStatus.Ready, result.Status);
        Profile = result.Profile!;
        Sessions = new DataSessionFactory(Profile.Database);
        Events = new EventProjectService(Sessions, TimeProvider.System, NullLogger<EventProjectService>.Instance);
        Guests = new GuestService(Sessions, NullLogger<GuestService>.Instance);
        Vendors = new VendorService(Sessions, TimeProvider.System);
        Quotes = new QuoteComparisonService(Sessions, TimeProvider.System, NullLogger<QuoteComparisonService>.Instance);
        Budget = new BudgetService(Sessions, NullLogger<BudgetService>.Instance);
        Tasks = new TaskService(Sessions, TimeProvider.System, NullLogger<TaskService>.Instance);
    }

    public ValueTask DisposeAsync()
    {
        Profile.Dispose();
        try
        {
            Directory.Delete(Root.Path, recursive: true);
        }
        catch (IOException)
        {
            // Temp leftovers are acceptable; failing a test on cleanup would hide the real result.
        }

        return ValueTask.CompletedTask;
    }
}
