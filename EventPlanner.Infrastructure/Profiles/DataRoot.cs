using System.Text.RegularExpressions;

namespace EventPlanner.Infrastructure.Profiles;

/// <summary>
/// Application data root: Windows %LOCALAPPDATA%\EventPlanner, macOS ~/Library/Application Support/EventPlanner
/// (.NET maps LocalApplicationData to Application Support on macOS). Holds profiles, logs and staging.
/// </summary>
public sealed partial class DataRoot
{
    public const string AppName = "EventPlanner";
    public const string DefaultProfileName = "default";
    private const string ActiveProfileFileName = "active-profile.txt";

    public DataRoot(string path)
    {
        Path = System.IO.Path.GetFullPath(path);
    }

    public string Path { get; }

    public string ProfilesDirectory => System.IO.Path.Combine(Path, "profiles");

    public string LogsDirectory => System.IO.Path.Combine(Path, "logs");

    public string StagingDirectory => System.IO.Path.Combine(Path, "staging");

    public static DataRoot ForCurrentUser() =>
        new(System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
            AppName));

    public static bool IsValidProfileName(string name) => ProfileNamePattern().IsMatch(name);

    public string ProfileDirectory(string profileName)
    {
        if (!IsValidProfileName(profileName))
        {
            throw new ArgumentException("Invalid profile name.", nameof(profileName));
        }

        return System.IO.Path.Combine(ProfilesDirectory, profileName);
    }

    /// <summary>Returns the active profile; an unreadable or invalid pointer falls back to the default profile.</summary>
    public string ReadActiveProfileName()
    {
        var pointer = System.IO.Path.Combine(Path, ActiveProfileFileName);
        if (!File.Exists(pointer))
        {
            return DefaultProfileName;
        }

        var name = File.ReadAllText(pointer).Trim();
        return IsValidProfileName(name) ? name : DefaultProfileName;
    }

    /// <summary>Switches the active profile atomically (write temp file, then replace).</summary>
    public void SetActiveProfile(string profileName)
    {
        if (!IsValidProfileName(profileName) || !Directory.Exists(ProfileDirectory(profileName)))
        {
            throw new ArgumentException("Profile does not exist.", nameof(profileName));
        }

        Directory.CreateDirectory(Path);
        var pointer = System.IO.Path.Combine(Path, ActiveProfileFileName);
        var temp = pointer + ".tmp";
        File.WriteAllText(temp, profileName);
        File.Move(temp, pointer, overwrite: true);
    }

    public string CreateStagingDirectory()
    {
        var dir = System.IO.Path.Combine(StagingDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$")]
    private static partial Regex ProfileNamePattern();
}
