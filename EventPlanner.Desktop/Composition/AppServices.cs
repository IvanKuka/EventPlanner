using System.Globalization;
using EventPlanner.Core.Ports;
using EventPlanner.Core.Services.Budget;
using EventPlanner.Core.Services.Events;
using EventPlanner.Core.Services.Guests;
using EventPlanner.Core.Services.Tasks;
using EventPlanner.Core.Services.Vendors;
using EventPlanner.Desktop.Platform;
using EventPlanner.Desktop.ViewModels.Backup;
using EventPlanner.Desktop.ViewModels.Budget;
using EventPlanner.Desktop.ViewModels.Events;
using EventPlanner.Desktop.ViewModels.Guests;
using EventPlanner.Desktop.ViewModels.Overview;
using EventPlanner.Desktop.ViewModels.Shell;
using EventPlanner.Desktop.ViewModels.Tasks;
using EventPlanner.Desktop.ViewModels.Vendors;
using EventPlanner.Infrastructure.Backup;
using EventPlanner.Infrastructure.Data;
using EventPlanner.Infrastructure.Profiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace EventPlanner.Desktop.Composition;

public static class AppServices
{
    /// <summary>
    /// Rotating local log files: daily files, 7 days retained, 3 MB per file (≈ 20 MB cap).
    /// Logs never contain guest names, notes or contacts — only ids, codes and phases.
    /// </summary>
    public static ILoggerFactory CreateLoggerFactory(DataRoot dataRoot)
    {
        var serilog = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(dataRoot.LogsDirectory, "eventplanner-.log"),
                formatProvider: CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                fileSizeLimitBytes: 3 * 1024 * 1024,
                rollOnFileSizeLimit: true)
            .CreateLogger();
        return LoggerFactory.Create(builder => builder.AddSerilog(serilog, dispose: true));
    }

    /// <summary>
    /// Services bound to one opened profile. The profile instance (and its lock) is owned by the
    /// caller; the container does not dispose instances it did not create.
    /// </summary>
    public static ServiceProvider BuildForProfile(AppOptions options, ILoggerFactory loggerFactory, OpenedProfile profile, IPlatformServices platform)
    {
        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddSingleton(options.DataRoot);
        services.AddSingleton(loggerFactory);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(platform);

        services.AddSingleton(profile);
        services.AddSingleton(profile.Database);
        services.AddSingleton<IDataSessionFactory, DataSessionFactory>();
        services.AddSingleton<ProfileOpener>();
        services.AddSingleton<ProfileBackupService>();
        services.AddSingleton<EventProjectService>();
        services.AddSingleton<GuestService>();
        services.AddSingleton<VendorService>();
        services.AddSingleton<QuoteComparisonService>();
        services.AddSingleton<BudgetService>();
        services.AddSingleton<TaskService>();

        services.AddSingleton<ActiveProjectContext>();
        services.AddSingleton<EventsViewModel>();
        services.AddSingleton<OverviewViewModel>();
        services.AddSingleton<GuestsViewModel>();
        services.AddSingleton<VendorsViewModel>();
        services.AddSingleton<BudgetViewModel>();
        services.AddSingleton<TasksViewModel>();
        services.AddSingleton<BackupViewModel>();
        return services.BuildServiceProvider();
    }
}
