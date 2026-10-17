namespace EventPlanner.Infrastructure.Profiles;

/// <summary>
/// Single-writer lock per profile. An exclusive FileStream maps to a share-deny open on Windows and
/// to an advisory flock on macOS, so a second instance (or second open in the same process) fails.
/// This guards one machine only; it is not a network collaboration mechanism.
/// </summary>
public sealed class ProfileLock : IDisposable
{
    private const string LockFileName = "profile.lock";
    private readonly FileStream _stream;

    private ProfileLock(FileStream stream)
    {
        _stream = stream;
    }

    public static ProfileLock? TryAcquire(string profileDirectory)
    {
        Directory.CreateDirectory(profileDirectory);
        try
        {
            var stream = new FileStream(
                Path.Combine(profileDirectory, LockFileName),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
            return new ProfileLock(stream);
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Dispose() => _stream.Dispose();
}
