using System.Net;
using System.Net.Sockets;
using EliteMud.Application.Session;
using EliteMud.Server;

namespace EliteMud.Tests.Server;

/// <summary>
/// Password prompts turn the client's echo off (security issue #2).
/// </summary>
public class TelnetEchoTests : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly TcpClient _client = new();
    private TcpClient? _server;

    [Theory]
    [InlineData(false, new byte[] { 255, 251, 1 })] // IAC WILL ECHO: the client stops echoing
    [InlineData(true, new byte[] { 255, 252, 1 })]  // IAC WONT ECHO: the client echoes again
    public async Task SetEchoAsync_SendsTelnetEchoCommand(bool enabled, byte[] expected)
    {
        var (session, clientStream) = await ConnectAsync();

        await session.SetEchoAsync(enabled, CancellationToken.None);

        var received = new byte[3];
        await clientStream.ReadExactlyAsync(received);
        Assert.Equal(expected, received);
    }

    [Fact]
    public async Task ReadLineAsync_IgnoresTheClientsEchoReply()
    {
        // The client answers "IAC DO ECHO" before the password; it must not end up in the input
        var (session, clientStream) = await ConnectAsync();

        await clientStream.WriteAsync(new byte[] { 255, 253, 1 });
        await clientStream.WriteAsync("secret\r\n"u8.ToArray());

        Assert.Equal("secret", await session.ReadLineAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData(ConnectionState.GetPassword, true)]
    [InlineData(ConnectionState.GetNewPassword, true)]
    [InlineData(ConnectionState.ConfirmNewPassword, true)]
    [InlineData(ConnectionState.GetAccountName, false)]
    [InlineData(ConnectionState.CharacterSelection, false)]
    [InlineData(ConnectionState.Playing, false)]
    public void IsPasswordState_OnlyForPasswordPrompts(ConnectionState state, bool expected)
    {
        Assert.Equal(expected, TelnetServer.IsPasswordState(state));
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
