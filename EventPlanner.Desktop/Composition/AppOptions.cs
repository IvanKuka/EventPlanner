using EventPlanner.Infrastructure.Profiles;

namespace EventPlanner.Desktop.Composition;

/// <summary>
/// Command-line options. <c>--smoke-test</c> starts the UI against a fresh temp data root,
/// initializes a profile and exits with 0 on success; it never loads the real user profile.
/// </summary>
public sealed class AppOptions
{
    public const string SmokeTestArgument = "--smoke-test";

    private AppOptions(bool isSmokeTest, DataRoot dataRoot)
    {
        IsSmokeTest = isSmokeTest;
        DataRoot = dataRoot;
    }

    public bool IsSmokeTest { get; }

    public DataRoot DataRoot { get; }

    public static AppOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Contains(SmokeTestArgument, StringComparer.Ordinal))
        {
            var root = Path.Combine(Path.GetTempPath(), "eventplanner-smoke", Guid.NewGuid().ToString("N"));
            return new AppOptions(true, new DataRoot(root));
        }

        return new AppOptions(false, DataRoot.ForCurrentUser());
    }

    public static AppOptions ForTests(DataRoot root) => new(false, root);

    public void CleanupSmokeTestRoot()
    {
        if (!IsSmokeTest || !Directory.Exists(DataRoot.Path))
        {
            return;
        }

        try
        {
            Directory.Delete(DataRoot.Path, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory does not change the smoke test result.
        }
    }
}
