using Avalonia.Headless.XUnit;
using EventPlanner.Desktop.Composition;
using EventPlanner.Desktop.Localization;
using EventPlanner.Desktop.ViewModels.Shell;
using EventPlanner.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventPlanner.Tests.Ui;

public sealed class UnexpectedErrorTests
{
    // The policy is about runtime-reserved exception types, so the test has to create them.
#pragma warning disable CA2201
    public static TheoryData<Exception> Survivable => new()
    {
        new InvalidOperationException(),
        new IOException(),
        new NullReferenceException(),
        new SqliteException("busy", 5),
    };

    public static TheoryData<Exception> Fatal => new()
    {
        new OutOfMemoryException(),
        new InsufficientExecutionStackException(),
        new AccessViolationException(),
        new TypeLoadException(),
    };

#pragma warning restore CA2201

    [Theory]
    [MemberData(nameof(Survivable))]
    public void Ordinary_failures_do_not_end_the_app(Exception exception) =>
        Assert.True(UnexpectedErrors.CanContinue(exception));

    [Theory]
    [MemberData(nameof(Fatal))]
    public void Runtime_faults_still_end_the_app(Exception exception) =>
        Assert.False(UnexpectedErrors.CanContinue(exception));

    [AvaloniaFact]
    public async Task Survived_failure_shows_a_banner_until_dismissed()
    {
        var root = TestProfile.NewRoot();
        using var shell = new ShellViewModel(AppOptions.ForTests(root), NullLoggerFactory.Instance, new FakePlatformServices());
        Assert.True(await shell.InitializeAsync(CancellationToken.None));

        shell.ReportUnexpectedError();
        Assert.Equal(Strings.Error_UnexpectedRecovered, shell.UnexpectedErrorMessage);

        shell.DismissUnexpectedErrorCommand.Execute(null);
        Assert.Null(shell.UnexpectedErrorMessage);
    }
}
