using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EventPlanner.Desktop.Composition;
using EventPlanner.Desktop.Localization;
using EventPlanner.Desktop.ViewModels.Guests;
using EventPlanner.Desktop.ViewModels.Shell;
using EventPlanner.Desktop.Views;
using EventPlanner.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventPlanner.Tests.Ui;

/// <summary>
/// The switcher menu closes itself when one of its buttons is clicked. The click must still run the
/// button's command: a menu closed first used to detach the button, so "Nový projekt" did nothing.
/// The button is clicked through its automation peer, the same path as a real click (OnClick).
/// </summary>
public sealed class ProjectSwitcherTests
{
    [AvaloniaFact]
    public async Task New_project_in_the_switcher_menu_opens_an_empty_project_form()
    {
        var root = TestProfile.NewRoot();
        var window = new MainWindow { Width = 1280, Height = 800 };
        using var shell = new ShellViewModel(AppOptions.ForTests(root), NullLoggerFactory.Instance, new FakePlatformServices());
        window.DataContext = shell;
        window.Show();
        Assert.True(await shell.InitializeAsync(CancellationToken.None));
        var projects = shell.ProjectsPage!;
        projects.Name = "Svadba";
        projects.StartDate = new DateTime(2027, 6, 12);
        projects.EndDate = new DateTime(2027, 6, 12);
        projects.TimeZoneId = "Europe/Bratislava";
        await projects.SaveCommand.ExecuteAsync(null);
        shell.SelectedPage = shell.Pages.OfType<GuestsViewModel>().Single();
        Dispatcher.UIThread.RunJobs();

        var switcher = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ProjectSwitcher");
        new ButtonAutomationPeer(switcher).Invoke();
        Dispatcher.UIThread.RunJobs();

        // Flyout content lives in its own popup root, outside the window's visual tree.
        var menu = Assert.IsType<StackPanel>(((Flyout)switcher.Flyout!).Content);
        var newButton = menu.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, Strings.Events_New));
        new ButtonAutomationPeer(newButton).Invoke();
        Dispatcher.UIThread.RunJobs();

        Assert.Same(projects, shell.SelectedPage);
        Assert.True(projects.IsNew);
        Assert.Equal(string.Empty, projects.Name);
        Assert.False(switcher.Flyout!.IsOpen);
        window.Close();
    }
}
