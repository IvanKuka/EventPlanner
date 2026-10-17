using EventPlanner.Infrastructure.Profiles;
using Microsoft.Extensions.Logging;

namespace EventPlanner.Desktop.Composition;

public enum AppTheme
{
    System,
    Light,
    Dark,
}

/// <summary>
/// The appearance choice of this computer's user. Stored in the data root, outside every profile,
/// so it also applies to the start-up and recovery screens and survives switching profiles.
/// </summary>
public sealed class ThemePreferenceStore(DataRoot dataRoot, ILogger<ThemePreferenceStore> logger)
{
    private const string FileName = "ui-theme.txt";

    private string FilePath => Path.Combine(dataRoot.Path, FileName);

    /// <summary>An unreadable or unknown value falls back to the system theme.</summary>
    public AppTheme Read()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return AppTheme.System;
            }

            // Exact names only (no numbers or other casing); anything else is the default, System.
            var text = File.ReadAllText(FilePath).Trim();
            return Enum.GetValues<AppTheme>().FirstOrDefault(theme => string.Equals(theme.ToString(), text, StringComparison.Ordinal));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Theme preference could not be read");
            return AppTheme.System;
        }
    }

    /// <summary>Writes atomically (temp file, then replace). A failure keeps the theme for this session only.</summary>
    public void Write(AppTheme theme)
    {
        try
        {
            Directory.CreateDirectory(dataRoot.Path);
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, theme.ToString());
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Theme preference could not be saved");
        }
    }
}
