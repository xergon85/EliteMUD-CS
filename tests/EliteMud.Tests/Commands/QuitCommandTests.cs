using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Quit;
using EliteMud.Server.Adapters.Commands.Shared;
using EliteMud.Tests.Server;

namespace EliteMud.Tests.Commands;

/// <summary>Legacy do_quit (act.other.c): you can't quit in the middle of a fight.</summary>
public class QuitCommandTests
{
    [Fact]
    public async Task QuittingWhileFighting_IsRefused()
    {
        var player = new PlayerState(1, "Tester", 3001) { Position = Position.Fighting };
        using var connection = new TestConnection(1, player);

        var outcome = await new QuitCommandHandler()
            .HandleAsync(new CommandRequest("quit", null, null), connection.Context, CancellationToken.None);

        Assert.Equal(CommandOutcome.Continue, outcome);
        Assert.Equal(new[] { "No way!  You're fighting for your life!" }, connection.Lines());
    }

    [Fact]
    public async Task QuittingOutsideAFight_SaysGoodbye()
    {
        var player = new PlayerState(1, "Tester", 3001) { Position = Position.Standing };
        using var connection = new TestConnection(1, player);

        var outcome = await new QuitCommandHandler()
            .HandleAsync(new CommandRequest("quit", null, null), connection.Context, CancellationToken.None);

        Assert.Equal(CommandOutcome.Disconnect, outcome);
        Assert.Equal(new[] { "Goodbye!" }, connection.Lines());
    }
}
