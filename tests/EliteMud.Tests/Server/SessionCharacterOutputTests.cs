using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Tests.Server;

/// <summary>
/// A character's output goes down their own connection; mobs and players without one see nothing (#79).
/// </summary>
public sealed class SessionCharacterOutputTests : IDisposable
{
    private readonly TestConnection _alice = new(1, new PlayerState(1, "Alice", 1, level: 5, "warrior", "human", sex: 1));
    private readonly TestConnection _bob = new(2, new PlayerState(2, "Bob", 1, level: 5, "warrior", "human", sex: 1));
    private readonly ConnectionRegistry _connections = new();
    private readonly SessionCharacterOutput _output;

    public SessionCharacterOutputTests()
    {
        _connections.Add(_alice.Context);
        _connections.Add(_bob.Context);
        _output = new SessionCharacterOutput(_connections);
    }

    public void Dispose()
    {
        _alice.Dispose();
        _bob.Dispose();
    }

    [Fact]
    public void SendLine_ReachesOnlyThatPlayer()
    {
        _output.SendLine(_alice.Context.Player, "Hello, Alice.");

        Assert.Equal(new[] { "Hello, Alice." }, _alice.Lines());
        Assert.Empty(_bob.Lines());
    }

    [Fact]
    public void Send_AddsNoLineEnding()
    {
        _output.Send(_alice.Context.Player, "> ");
        _output.SendLine(_alice.Context.Player, "look");

        Assert.Equal(new[] { "> look" }, _alice.Lines());
    }

    [Fact]
    public void APlayerWhoseConnectionIsGone_IsNotConnected_AndSeesNothing()
    {
        _connections.Remove(_alice.Context);

        _output.SendLine(_alice.Context.Player, "Anyone there?");

        Assert.False(_output.IsConnected(_alice.Context.Player));
        Assert.True(_output.IsConnected(_bob.Context.Player));
        Assert.Empty(_alice.Lines());
    }

    [Fact]
    public void APlayerWithTheSameIdButAnotherState_IsNotConnected()
    {
        // Found by the player themselves, not their id: a stale copy must not receive someone's output
        var copy = new PlayerState(1, "Alice", 1, level: 5, "warrior", "human", sex: 1);

        _output.SendLine(copy, "Not for you.");

        Assert.False(_output.IsConnected(copy));
        Assert.Empty(_alice.Lines());
    }

    [Fact]
    public void AMob_IsNotConnected_AndSendingToItDoesNothing()
    {
        var definition = new MobDefinition(
            100, "rat", "a rat", "A rat is here.", "Desc", 5, "Animal", "None",
            new List<string>(), new StatBlock(10, 10, 10, 10, 10, 10),
            new List<string>(), new List<string>(), 10, 10, 0, new List<MobAttack>(), null);
        var rat = new MobInstance(1, definition);

        _output.SendLine(rat, "Squeak.");

        Assert.False(_output.IsConnected(rat));
        Assert.Empty(_alice.Lines());
        Assert.Empty(_bob.Lines());
    }
}
