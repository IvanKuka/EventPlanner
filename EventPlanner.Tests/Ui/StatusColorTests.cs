using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace EventPlanner.Tests.Ui;

/// <summary>
/// Status text classes must actually resolve to their colour in both theme variants; an unresolved
/// DynamicResource silently falls back to the default text colour.
/// </summary>
public sealed class StatusColorTests
{
    public static TheoryData<string, string> Cases => new()
    {
        { "field-error", "Light" },
        { "field-error", "Dark" },
        { "notice", "Light" },
        { "notice", "Dark" },
        { "positive", "Light" },
        { "positive", "Dark" },
    };

    [AvaloniaTheory]
    [MemberData(nameof(Cases))]
    public void Status_class_resolves_to_its_own_colour(string styleClass, string variant)
    {
        var themed = new TextBlock { Text = "x", Classes = { styleClass } };
        var plain = new TextBlock { Text = "x" };
        var window = new Window
        {
            RequestedThemeVariant = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = new StackPanel { Children = { themed, plain } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // Styles are detached with the window, so colours are read while it is shown.
        var brush = Assert.IsAssignableFrom<ISolidColorBrush>(themed.Foreground);
        var plainBrush = Assert.IsAssignableFrom<ISolidColorBrush>(plain.Foreground);
        window.Close();
        Assert.NotEqual(plainBrush.Color, brush.Color);
    }
}
