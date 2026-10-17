using Avalonia.Controls;
using Avalonia.Interactivity;

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

    private void OnFlyoutCommand(object? sender, RoutedEventArgs e) => ProjectSwitcher.Flyout?.Hide();
}
