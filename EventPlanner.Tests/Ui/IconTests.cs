using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EventPlanner.Desktop.Composition;
using EventPlanner.Desktop.ViewModels.Shell;
using EventPlanner.Desktop.Views;
using EventPlanner.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using IconPath = Avalonia.Controls.Shapes.Path;

namespace EventPlanner.Tests.Ui;

/// <summary>
/// Line icons are stroked with the inherited text colour; a broken binding leaves the stroke empty and
/// the icon silently invisible, so every rendered icon must have geometry and a stroke.
/// </summary>
public sealed class IconTests
{
    [AvaloniaFact]
    public async Task Navigation_and_button_icons_are_drawn_with_a_stroke()
    {
        var root = TestProfile.NewRoot();
        var window = new MainWindow { Width = 1280, Height = 800 };
        using var shell = new ShellViewModel(AppOptions.ForTests(root), NullLoggerFactory.Instance, new FakePlatformServices());
        window.DataContext = shell;
        window.Show();
        Assert.True(await shell.InitializeAsync(CancellationToken.None));
        Dispatcher.UIThread.RunJobs();

        // Styles are detached with the window, so everything is read while it is shown.
        var icons = window.GetVisualDescendants().OfType<IconPath>().Where(p => p.Classes.Contains("icon"))
            .Select(p => (p.Data, p.Stroke)).ToList();
        var iconButtonClasses = window.GetVisualDescendants().OfType<Button>().Where(b => ButtonIcon.GetData(b) is not null)
            .Select(b => b.Classes.Contains("icon")).ToList();
        window.Close();

        Assert.True(icons.Count >= shell.TabPages.Count + 1, $"Expected tab and button icons, found {icons.Count}.");
        Assert.All(icons, icon =>
        {
            Assert.NotNull(icon.Data);
            Assert.NotNull(icon.Stroke);
        });
        Assert.NotEmpty(iconButtonClasses);
        Assert.All(iconButtonClasses, Assert.True);
    }
}
