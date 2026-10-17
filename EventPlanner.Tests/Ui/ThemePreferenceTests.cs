using EventPlanner.Desktop.Composition;
using EventPlanner.Desktop.ViewModels.Shell;
using EventPlanner.Infrastructure.Profiles;
using EventPlanner.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventPlanner.Tests.Ui;

public sealed class ThemePreferenceTests : IDisposable
{
    private readonly DataRoot _root = TestProfile.NewRoot();

    [Fact]
    public void Without_a_saved_choice_the_system_theme_is_applied()
    {
        var platform = new FakePlatformServices();
        using var shell = NewShell(platform);

        Assert.Equal(AppTheme.System, shell.Theme);
        Assert.Equal(AppTheme.System, platform.AppliedTheme);
        Assert.True(shell.IsSystemTheme);
    }

    [Fact]
    public void Chosen_theme_is_applied_at_once_and_restored_on_next_start()
    {
        var platform = new FakePlatformServices();
        using (var shell = NewShell(platform))
        {
            shell.SetThemeCommand.Execute(AppTheme.Light);
            Assert.Equal(AppTheme.Light, platform.AppliedTheme);
            Assert.True(shell.IsLightTheme);
        }

        var next = new FakePlatformServices();
        using var restarted = NewShell(next);

        Assert.Equal(AppTheme.Light, restarted.Theme);
        Assert.Equal(AppTheme.Light, next.AppliedTheme);
    }

    [Theory]
    [InlineData("2")]
    [InlineData("dark")]
    [InlineData("Neon")]
    public void Unknown_saved_value_falls_back_to_the_system_theme(string content)
    {
        Directory.CreateDirectory(_root.Path);
        File.WriteAllText(Path.Combine(_root.Path, "ui-theme.txt"), content);

        using var shell = NewShell(new FakePlatformServices());

        Assert.Equal(AppTheme.System, shell.Theme);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root.Path, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (IOException)
        {
        }
    }

    private ShellViewModel NewShell(FakePlatformServices platform) =>
        new(AppOptions.ForTests(_root), NullLoggerFactory.Instance, platform);
}
