using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using EliteMud.Game;
using EliteMud.Server;

namespace EliteMud.Tests.Server;

/// <summary>
/// A connected player for handler tests: a real <see cref="TelnetSession"/> over a loopback socket, so what the
/// server sends can be read back as lines (ANSI colors stripped).
/// </summary>
internal sealed partial class TestConnection : IDisposable
{
    private readonly TcpListener _listener;
    private readonly TcpClient _client;
    private readonly TcpClient _server;
    private readonly StringBuilder _received = new();

    public TestConnection(int id, PlayerState player)
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        _client = new TcpClient();
        _client.Connect((IPEndPoint)_listener.LocalEndpoint);
        _server = _listener.AcceptTcpClient();
        Context = new ConnectionContext(id, new TelnetSession(_server.GetStream()), player, characterId: id, "test");
    }

    public ConnectionContext Context { get; }

    /// <summary>Everything received since the last call, one entry per line, empty lines included.</summary>
    public IReadOnlyList<string> Lines()
    {
        // Output is written by the session's own writer (#61); let it catch up first
        Context.Session.WaitUntilSentAsync().GetAwaiter().GetResult();

        var stream = _client.GetStream();
        var buffer = new byte[4096];
        // Writes are awaited before this is called; allow the loopback a moment to deliver them
        while (_client.Client.Poll(TimeSpan.FromMilliseconds(50), SelectMode.SelectRead) && stream.DataAvailable)
        {
            var read = stream.Read(buffer);
            _received.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }

        var text = Ansi().Replace(_received.ToString(), string.Empty);
        _received.Clear();
        // Keep empty lines (a stray blank line is a bug); only the final line ending ends nothing
        var lines = text.Split("\r\n");
        return lines[^1].Length == 0 ? lines[..^1] : lines;
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
        _listener.Stop();
    }

    [GeneratedRegex("\u001b\\[[0-9;]*m")]
    private static partial Regex Ansi();
}
