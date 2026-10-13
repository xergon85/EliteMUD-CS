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
        // The client never reads; small buffers on both ends, so the kernel can't absorb what we send
        var (session, _) = await ConnectAsync(smallBuffers: true);
        var line = new string('x', 1000);

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < TelnetSession.MaxQueuedBytes / line.Length * 3 && !session.IsClosed; i++)
        {
            await session.SendLineAsync(line, CancellationToken.None);
        }

        Assert.True(session.IsClosed);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"sending took {stopwatch.Elapsed}");
    }

    /// <summary>
    /// When writing fails but the client is still connected (it could keep sending commands), the session drops
    /// the connection, so the read loop ends and the player leaves instead of playing on with every reply lost.
    /// </summary>
    [Fact]
    public async Task AFailedWrite_EndsTheReadLoop()
    {
        var (session, _) = await ConnectAsync();
        var reading = session.ReadLineAsync(CancellationToken.None).AsTask();

        _server!.Client.Shutdown(SocketShutdown.Send); // writes fail from now on; reading still works
        await session.SendLineAsync("anyone there?", CancellationToken.None);

        var ended = await Task.WhenAny(reading, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(reading, ended);
        Assert.Null(await reading); // ends like a disconnect, not an error
        Assert.True(session.IsClosed);
    }

    private async Task<(TelnetSession Session, NetworkStream ClientStream)> ConnectAsync(bool smallBuffers = false)
    {
        if (smallBuffers)
        {
            _client.ReceiveBufferSize = 4096; // before connecting, so the advertised window stays small
        }

        _listener.Start();
        await _client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)_listener.LocalEndpoint).Port);
        _server = await _listener.AcceptTcpClientAsync();
        if (smallBuffers)
        {
            _server.SendBufferSize = 4096;
        }

        return (new TelnetSession(_server.GetStream()), _client.GetStream());
    }

    public void Dispose()
    {
        _server?.Dispose();
        _client.Dispose();
        _listener.Stop();
    }
}
