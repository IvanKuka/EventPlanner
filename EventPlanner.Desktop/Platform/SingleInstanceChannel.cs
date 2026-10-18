using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace EventPlanner.Desktop.Platform;

/// <summary>
/// Lets a second start of the app bring the running window of the same profile to the front instead
/// of only reporting a locked profile. A local named pipe (a Unix domain socket on macOS) restricted
/// to the current user carries one fixed token; nothing received is used as data or as a command.
/// The running instance acknowledges each request, so a start only counts as handled once the window
/// was actually asked to show (on Unix a client can otherwise reach a socket that is just closing).
/// </summary>
public sealed class SingleInstanceChannel(string profileDirectory, ILogger logger) : IDisposable
{
    private const byte Acknowledge = 0x06;
    private const int Attempts = 3;
    private static readonly byte[] ActivateToken = Encoding.ASCII.GetBytes("activate\n");
    // Generous for a loaded machine; it only delays the "locked profile" message when nobody listens.
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ExchangeTimeout = TimeSpan.FromSeconds(2);
    private readonly CancellationTokenSource _stop = new();

    /// <summary>One pipe per profile directory; hashed so the name is short and contains no path.</summary>
    public string PipeName { get; } = "EventPlanner-" + Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(profileDirectory).ToUpperInvariant())))[..24];

    /// <summary>Asks the instance holding the profile to show its window. False when none answers.</summary>
    public async Task<bool> TryActivateRunningInstanceAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            try
            {
                await using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous);
                await client.ConnectAsync(ConnectTimeout, cancellationToken);
                using var exchange = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                exchange.CancelAfter(ExchangeTimeout);
                await client.WriteAsync(ActivateToken, exchange.Token);
                await client.FlushAsync(exchange.Token);

                var reply = new byte[1];
                if (await client.ReadAsync(reply, exchange.Token) == 1 && reply[0] == Acknowledge)
                {
                    return true;
                }
            }
            catch (TimeoutException ex)
            {
                // Nobody listens: there is no running instance (or an older one without this channel).
                logger.LogInformation(ex, "No running instance answered on {Pipe}", PipeName);
                return false;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation(ex, "Activation attempt {Attempt} on {Pipe} was not acknowledged", attempt, PipeName);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), cancellationToken);
        }

        return false;
    }

    /// <summary>Calls <paramref name="activate"/> for every activation request until disposed.</summary>
    public void Listen(Action activate) => _ = ListenAsync(activate, _stop.Token);

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }

    /// <summary>
    /// Accept loop. The next listening instance is created before the current connection is
    /// handled, so the pipe never disappears between two requests (with a single instance a quick
    /// second start could find no listener and time out, seen on a loaded CI runner).
    /// </summary>
    private async Task ListenAsync(Action activate, CancellationToken cancellationToken)
    {
        NamedPipeServerStream? next = null;
        try
        {
            next = CreateServer();
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await next.WaitForConnectionAsync(cancellationToken);
                }
                catch (IOException ex)
                {
                    logger.LogWarning(ex, "Activation listener on {Pipe} failed; restarting it", PipeName);
                    await next.DisposeAsync();
                    next = CreateServer();
                    continue;
                }

                var connected = next;
                next = CreateServer();
                _ = HandleAsync(connected, activate, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The app is closing.
        }
        catch (IOException ex)
        {
            // The pipe could not be created at all (e.g. name taken by another program): later starts
            // then show the locked-profile message instead of activating this window.
            logger.LogWarning(ex, "Activation listener on {Pipe} could not start", PipeName);
        }
        finally
        {
            if (next is not null)
            {
                await next.DisposeAsync();
            }
        }
    }

    private NamedPipeServerStream CreateServer() => new(
        PipeName,
        PipeDirection.InOut,
        NamedPipeServerStream.MaxAllowedServerInstances,
        PipeTransmissionMode.Byte,
        PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous);

    private async Task HandleAsync(NamedPipeServerStream server, Action activate, CancellationToken cancellationToken)
    {
        try
        {
            await using (server)
            {
                // Fixed-size read with a deadline: a peer that sends nothing or garbage cannot block
                // the listener or make it allocate.
                using var exchange = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                exchange.CancelAfter(ExchangeTimeout);
                var received = new byte[ActivateToken.Length];
                await server.ReadExactlyAsync(received, exchange.Token);
                if (received.AsSpan().SequenceEqual(ActivateToken))
                {
                    activate();
                    await server.WriteAsync(new[] { Acknowledge }, exchange.Token);
                    await server.FlushAsync(exchange.Token);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException)
        {
            // A broken or silent connection must not stop listening for the next start.
            logger.LogWarning(ex, "Activation request on {Pipe} failed", PipeName);
        }
    }
}
