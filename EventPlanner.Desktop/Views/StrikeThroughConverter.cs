using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace EventPlanner.Desktop.Views;

/// <summary>Strikes through the title of a finished task; the state is also written out as text.</summary>
public sealed class StrikeThroughConverter : IValueConverter
{
    public static StrikeThroughConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? TextDecorations.Strikethrough : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
