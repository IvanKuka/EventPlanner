using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using EventPlanner.Desktop.ViewModels.Backup;
using EventPlanner.Desktop.ViewModels.Budget;
using EventPlanner.Desktop.ViewModels.Events;
using EventPlanner.Desktop.ViewModels.Guests;
using EventPlanner.Desktop.ViewModels.Overview;
using EventPlanner.Desktop.ViewModels.Tasks;
using EventPlanner.Desktop.ViewModels.Vendors;

namespace EventPlanner.Desktop.Views;

/// <summary>
/// Navigation icon of a page, mapped explicitly like the views in App.axaml (no reflection on names),
/// so view models stay free of visual resources.
/// </summary>
public sealed class PageIconConverter : IValueConverter
{
    public static PageIconConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            EventsViewModel => "Icon.Projects",
            OverviewViewModel => "Icon.Overview",
            GuestsViewModel => "Icon.Guests",
            VendorsViewModel => "Icon.Vendors",
            BudgetViewModel => "Icon.Budget",
            TasksViewModel => "Icon.Tasks",
            BackupViewModel => "Icon.Backup",
            _ => null,
        };

        return key is not null && Application.Current?.TryGetResource(key, Application.Current.ActualThemeVariant, out var geometry) == true
            ? geometry
            : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
