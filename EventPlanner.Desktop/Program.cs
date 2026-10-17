using Avalonia;
using EventPlanner.Desktop.Composition;

namespace EventPlanner.Desktop;

internal static class Program
{
    // Avalonia is not initialized before AppMain; keep this method free of UI or sync-context code.
    [STAThread]
    public static int Main(string[] args)
    {
        var options = AppOptions.Parse(args);
        try
        {
            return BuildAvaloniaApp(options).StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            options.CleanupSmokeTestRoot();
        }
    }

    // Used by the visual designer as well.
    public static AppBuilder BuildAvaloniaApp() => BuildAvaloniaApp(AppOptions.Parse([]));

    private static AppBuilder BuildAvaloniaApp(AppOptions options) =>
        AppBuilder.Configure(() => new App(options))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
