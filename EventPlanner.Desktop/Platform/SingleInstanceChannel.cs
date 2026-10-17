using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace EventPlanner.Desktop.Platform;

/// <summary>
/// Lets a second start of the app bring the running window of the same profile to the front instead
/// of only reporting a locked profile. A local named pipe (a Unix domain socket on macOS) restricted
/// to the current user carries one fixed token; nothing received is used as data or as a command.
/// </summary>
public sealed class SingleInstanceChannel(string profileDirectory, ILogger logger) : IDisposable
{
    private const string ActivateToken = "activate";
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(1);
    private readonly CancellationTokenSource _stop = new();

    /// <summary>One pipe per profile directory; hashed so the name is short and contains no path.</summary>
    public string PipeName { get; } = "EventPlanner-" + Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(profileDirectory).ToUpperInvariant())))[..24];

    /// <summary>Asks the instance holding the profile to show its window. False when none answers.</summary>
    public async Task<bool> TryActivateRunningInstanceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous);
            await client.ConnectAsync(ConnectTimeout, cancellationToken);
            await using var writer = new StreamWriter(client, Encoding.UTF8);
            await writer.WriteLineAsync(ActivateToken.AsMemory(), cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            logger.LogInformation(ex, "No running instance answered on {Pipe}", PipeName);
            return false;
        }
    }

    /// <summary>Calls <paramref name="activate"/> for every activation request until disposed.</summary>
    public void Listen(Action activate) => _ = ListenAsync(activate, _stop.Token);

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }

    private async Task ListenAsync(Action activate, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(cancellationToken);
                using var reader = new StreamReader(server, Encoding.UTF8);

                // Bounded read: only the short token is expected, a peer cannot make this buffer grow.
                var buffer = new char[ActivateToken.Length + 2];
                var read = await reader.ReadBlockAsync(buffer.AsMemory(), cancellationToken);
                if (new string(buffer, 0, read).Trim() == ActivateToken)
                {
                    activate();
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException ex)
            {
                // A broken connection must not stop listening for the next start.
                logger.LogWarning(ex, "Activation request on {Pipe} failed", PipeName);
            }
        }
    }
}
