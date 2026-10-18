using EventPlanner.Desktop.Platform;
using EventPlanner.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventPlanner.Tests.Integration.Platform;

/// <summary>Real local pipe between two channels of the same profile, as two app starts would use it.</summary>
public sealed class SingleInstanceChannelTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Second_start_reaches_the_running_instance_of_the_same_profile()
    {
        var profileDir = TestProfile.NewRoot().ProfileDirectory("default");
        using var running = new SingleInstanceChannel(profileDir, NullLogger.Instance);
        var activations = 0;
        var twice = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        running.Listen(() =>
        {
            if (Interlocked.Increment(ref activations) == 2)
            {
                twice.TrySetResult();
            }
        });

        // Two later starts in a row: the listener keeps serving after the first request.
        using var second = new SingleInstanceChannel(profileDir, NullLogger.Instance);
        Assert.True(await second.TryActivateRunningInstanceAsync(Ct));
        Assert.True(await second.TryActivateRunningInstanceAsync(Ct));
        await twice.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
    }

    /// <summary>The listener keeps a free instance while it serves one, so simultaneous starts all get through.</summary>
    [Fact]
    public async Task Simultaneous_starts_are_all_acknowledged()
    {
        var profileDir = TestProfile.NewRoot().ProfileDirectory("default");
        using var running = new SingleInstanceChannel(profileDir, NullLogger.Instance);
        var activations = 0;
        running.Listen(() => Interlocked.Increment(ref activations));

        var starts = Enumerable.Range(0, 20).Select(async _ =>
        {
            using var start = new SingleInstanceChannel(profileDir, NullLogger.Instance);
            return await start.TryActivateRunningInstanceAsync(Ct);
        });

        Assert.All(await Task.WhenAll(starts), Assert.True);
        Assert.Equal(20, Volatile.Read(ref activations));
    }

    [Fact]
    public async Task Without_a_running_instance_activation_reports_false()
    {
        using var channel = new SingleInstanceChannel(TestProfile.NewRoot().ProfileDirectory("default"), NullLogger.Instance);

        Assert.False(await channel.TryActivateRunningInstanceAsync(Ct));
    }

    [Fact]
    public void Each_profile_has_its_own_channel_name()
    {
        var root = TestProfile.NewRoot();
        using var first = new SingleInstanceChannel(root.ProfileDirectory("default"), NullLogger.Instance);
        using var other = new SingleInstanceChannel(root.ProfileDirectory("restored-1"), NullLogger.Instance);

        Assert.NotEqual(first.PipeName, other.PipeName);
        Assert.DoesNotContain(Path.DirectorySeparatorChar, first.PipeName);
    }
}
