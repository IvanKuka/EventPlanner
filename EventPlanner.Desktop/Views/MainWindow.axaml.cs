using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace EventPlanner.Desktop.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    // View-only behaviour: the project switcher closes once a project is picked or a command runs.
    // The selection itself is bound to the projects page, so no logic lives here.
    private void OnProjectPicked(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0)
        {
            ProjectSwitcher.Flyout?.Hide();
        }
    }

    // Click is raised before the button runs its command. Closing the menu right away detaches the
    // button, its command binding goes away and the click does nothing, so the menu closes afterwards.
    private void OnFlyoutCommand(object? sender, RoutedEventArgs e) =>
        Dispatcher.UIThread.Post(() => ProjectSwitcher.Flyout?.Hide());
}
