using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using EventPlanner.Desktop.Composition;
using EventPlanner.Desktop.Platform;
using EventPlanner.Desktop.ViewModels.Shell;
using EventPlanner.Desktop.Views;
using Microsoft.Extensions.Logging;

namespace EventPlanner.Desktop;

public sealed partial class App : Application
{
    private static readonly TimeSpan SmokeTestTimeout = TimeSpan.FromSeconds(60);
    private readonly AppOptions _options;

    // Parameterless constructor for the XAML previewer.
    public App()
        : this(AppOptions.Parse([]))
    {
    }

    public App(AppOptions options)
    {
        _options = options;
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var loggerFactory = AppServices.CreateLoggerFactory(_options.DataRoot);
            var logger = loggerFactory.CreateLogger<App>();

            var window = new MainWindow();
            var platform = new AvaloniaPlatformServices(window, desktop, loggerFactory.CreateLogger<AvaloniaPlatformServices>());
            var shell = new ShellViewModel(_options, loggerFactory, platform);
            RegisterGlobalExceptionHandling(logger, shell);
            window.DataContext = shell;
            desktop.MainWindow = window;
            desktop.Exit += (_, _) =>
            {
                platform.Dispose();
                shell.Dispose();
                loggerFactory.Dispose();
            };

            window.Opened += async (_, _) =>
            {
                var ready = await shell.InitializeAsync(CancellationToken.None);
                if (_options.IsSmokeTest)
                {
                    logger.LogInformation("Smoke test finished: {Result}", ready ? "ok" : "failed");
                    desktop.Shutdown(ready ? 0 : 1);
                }
            };

            if (_options.IsSmokeTest)
            {
                DispatcherTimer.RunOnce(() => desktop.Shutdown(2), SmokeTestTimeout);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void RegisterGlobalExceptionHandling(ILogger logger, ShellViewModel shell)
    {
        // Every unhandled UI exception is logged. Ones the app can survive are shown as a banner and
        // the page reloads (data writes are transactional); runtime faults still end the process.
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            logger.LogCritical(e.Exception, "Unhandled UI exception");
            if (UnexpectedErrors.CanContinue(e.Exception))
            {
                e.Handled = true;
                shell.ReportUnexpectedError();
            }
        };
        TaskScheduler.UnobservedTaskException += (_, e) => logger.LogError(e.Exception, "Unobserved task exception");
    }
}
