using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace EventPlanner.Desktop.Views;

/// <summary>
/// Optional line icon in front of a button's text: <c>views:ButtonIcon.Data="{StaticResource Icon.Add}"</c>.
/// Setting it adds the <c>icon</c> class, whose content template (Styles/AppStyles.axaml) draws the icon,
/// so buttons keep plain string content and their accessible name.
/// </summary>
public static class ButtonIcon
{
    public static readonly AttachedProperty<Geometry?> DataProperty =
        AvaloniaProperty.RegisterAttached<Button, Geometry?>("Data", typeof(ButtonIcon));

    static ButtonIcon()
    {
        DataProperty.Changed.AddClassHandler<Button>((button, e) => button.Classes.Set("icon", e.NewValue is not null));
    }

    public static Geometry? GetData(Button button) => button.GetValue(DataProperty);

    public static void SetData(Button button, Geometry? value) => button.SetValue(DataProperty, value);
}
