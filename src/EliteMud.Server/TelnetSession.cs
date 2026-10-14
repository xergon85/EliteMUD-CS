using System.Buffers;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;

namespace EliteMud.Server;

/// <summary>
/// One telnet connection. Output is queued and written by the session's own writer task, so sending never waits
/// on the network: game code can send while holding the game state gate (#61), and a client that stops reading
/// can't stall anyone else. A client too far behind (<see cref="MaxQueuedBytes"/>) is disconnected.
/// </summary>
internal sealed class TelnetSession
{
    /// <summary>Unsent output a session may build up before it is disconnected.</summary>
    internal const int MaxQueuedBytes = 1024 * 1024;

    private const byte TelnetIac = 255;
    private const byte TelnetWill = 251;
    private const byte TelnetWont = 252;
    private const byte TelnetOptionEcho = 1;
    private const byte TelnetCommandLength = 3;

    private static readonly TimeSpan CloseFlushTimeout = TimeSpan.FromSeconds(2);

    private readonly NetworkStream _stream;
    private readonly Encoding _encoding = Encoding.ASCII;
    private readonly Channel<byte[]> _output = Channel.CreateUnbounded<byte[]>(
        new UnboundedChannelOptions { SingleReader = true });
    private readonly Task _writer;
    private long _queuedBytes;
    private volatile bool _closed;

    public TelnetSession(NetworkStream stream)
    {
        _stream = stream;
        _writer = Task.Run(WriteOutputAsync);
    }

    /// <summary>The session can't send any more: closed, too far behind, or the connection failed.</summary>
    public bool IsClosed => _closed;

    public async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(1024);
        try
        {
            var builder = new StringBuilder();
            while (true)
            {
                int bytesRead;
                try
                {
                    bytesRead = await _stream.ReadAsync(buffer, cancellationToken);
                }
                catch (Exception ex) when (_closed && ex is IOException or ObjectDisposedException or SocketException)
                {
                    return null; // we dropped the connection ourselves (Abort): a disconnect, not an error
                }

                if (bytesRead == 0)
                {
                    return null;
                }

                var cursor = 0;
                while (cursor < bytesRead)
                {
                    var current = buffer[cursor];
                    if (current == TelnetIac)
                    {
                        cursor = SkipTelnetCommand(cursor, bytesRead);
                        continue;
                    }

                    if (current == '\n')
                    {
                        return builder.ToString().TrimEnd('\r');
                    }

                    builder.Append((char)current);
                    cursor++;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public ValueTask SendLineAsync(string message, CancellationToken cancellationToken)
    {
        // Translate color codes (#X format) to ANSI escape sequences
        var translatedMessage = ColorTranslator.TranslateColors(ToTelnetLineEndings(message));
        Queue(_encoding.GetBytes(translatedMessage + "\r\n"));
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Telnet needs "\r\n" for a new line; a bare "\n" (multi-line descriptions, StringBuilder.AppendLine on Linux)
    /// moves down without returning to the start of the line on some clients.
    /// </summary>
    internal static string ToTelnetLineEndings(string message) =>
        message.Contains('\n') ? message.Replace("\r\n", "\n").Replace("\n", "\r\n") : message;

    public ValueTask SendAsync(string message, CancellationToken cancellationToken)
    {
        // Translate color codes (#X format) to ANSI escape sequences
        var translatedMessage = ColorTranslator.TranslateColors(ToTelnetLineEndings(message));
        Queue(_encoding.GetBytes(translatedMessage));
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Turn the client's local echo on or off, e.g. off while a password is typed.
    /// "IAC WILL ECHO" tells the client the server echoes (so the client stops); "IAC WONT ECHO" undoes it.
    /// Legacy: echo_off() / echo_on() in utility.c
    /// </summary>
    public ValueTask SetEchoAsync(bool enabled, CancellationToken cancellationToken)
    {
        Queue(new[] { TelnetIac, enabled ? TelnetWont : TelnetWill, TelnetOptionEcho });
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Send what's still queued (waiting at most a moment), then stop the writer. Call before closing the socket,
    /// so last words (goodbye, ban message) still arrive.
    /// </summary>
    public async Task CloseAsync()
    {
        _output.Writer.TryComplete();
        await Task.WhenAny(_writer, Task.Delay(CloseFlushTimeout));
        _closed = true;
    }

    /// <summary>Wait until everything queued so far has been written (tests; at most a few seconds).</summary>
    internal async Task WaitUntilSentAsync()
    {
        var deadline = DateTime.UtcNow + CloseFlushTimeout;
        while (Interlocked.Read(ref _queuedBytes) > 0 && !IsClosed && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5);
        }
    }

    private void Queue(byte[] payload)
    {
        if (IsClosed)
        {
            return;
        }

        if (Interlocked.Add(ref _queuedBytes, payload.Length) > MaxQueuedBytes)
        {
            Console.WriteLine($"[Session] Disconnecting a client that stopped reading ({MaxQueuedBytes} bytes unsent)");
            Abort();
            return;
        }

        _output.Writer.TryWrite(payload);
    }

    private async Task WriteOutputAsync()
    {
        try
        {
            await foreach (var payload in _output.Reader.ReadAllAsync())
            {
                await _stream.WriteAsync(payload);
                Interlocked.Add(ref _queuedBytes, -payload.Length);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException)
        {
            // The client can't receive any more. Drop the connection, so the read loop ends and the player leaves
            // the game, instead of running commands whose replies go nowhere
            Abort();
        }
    }

    /// <summary>Stop sending and drop the connection, so the read loop ends too.</summary>
    private void Abort()
    {
        _closed = true;
        _output.Writer.TryComplete();
        _stream.Socket.Close();
    }

    private static int SkipTelnetCommand(int cursor, int bytesRead)
    {
        var next = cursor + TelnetCommandLength;
        return next <= bytesRead ? next : bytesRead;
    }
}
