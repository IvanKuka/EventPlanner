using Avalonia;
using Avalonia.Headless;
using EventPlanner.Desktop;
using EventPlanner.Tests.Ui;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace EventPlanner.Tests.Ui;

/// <summary>
/// Headless Avalonia with real Skia rendering and the same Inter font as the app, so captured frames
/// show real text metrics (clipping, wrapping). Headless tests cover bindings and view-model flows;
/// they do not replace OS smoke tests (pickers, packaging).
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .WithInterFont()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
