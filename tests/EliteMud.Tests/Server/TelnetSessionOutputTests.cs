using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using EliteMud.Server;

namespace EliteMud.Tests.Server;

/// <summary>
/// Output is queued and written by the session's own writer (#61): sending never waits on the network, and a
/// client that stops reading is disconnected instead of stalling the game.
/// </summary>
public sealed class TelnetSessionOutputTests : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly TcpClient _client = new();
    private TcpClient? _server;

    [Fact]
    public async Task Output_ArrivesInOrder()
    {
        var (session, clientStream) = await ConnectAsync();

        for (var i = 0; i < 100; i++)
        {
            await session.SendLineAsync($"line {i}", CancellationToken.None);
        }

        var expected = string.Concat(Enumerable.Range(0, 100).Select(i => $"line {i}\r\n"));
        var received = new byte[expected.Length];
        await clientStream.ReadExactlyAsync(received);
        Assert.Equal(expected, Encoding.ASCII.GetString(received));
    }

    [Fact]
    public async Task CloseAsync_SendsWhatIsStillQueued()
    {
        var (session, clientStream) = await ConnectAsync();

        await session.SendLineAsync("Goodbye.", CancellationToken.None);
        await session.CloseAsync();
        _server!.Close();

        using var reader = new StreamReader(clientStream, Encoding.ASCII);
        Assert.Equal("Goodbye.", await reader.ReadLineAsync());
        Assert.True(session.IsClosed);
    }

    [Fact]
    public async Task AClientThatStopsReading_IsDisconnected_WithoutBlockingTheSender()
    {
        var (session, _) = await ConnectAsync(); // the client never reads
        var line = new string('x', 1000);

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < TelnetSession.MaxQueuedBytes / line.Length * 3 && !session.IsClosed; i++)
        {
            await session.SendLineAsync(line, CancellationToken.None);
        }

        Assert.True(session.IsClosed);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"sending took {stopwatch.Elapsed}");
    }

    private async Task<(TelnetSession Session, NetworkStream ClientStream)> ConnectAsync()
    {
        _listener.Start();
        await _client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)_listener.LocalEndpoint).Port);
        _server = await _listener.AcceptTcpClientAsync();
        return (new TelnetSession(_server.GetStream()), _client.GetStream());
    }

    public void Dispose()
    {
        _server?.Dispose();
        _client.Dispose();
        _listener.Stop();
    }
}
