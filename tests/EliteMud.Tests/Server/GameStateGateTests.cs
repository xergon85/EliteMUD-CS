using EliteMud.Game;
using EliteMud.Server;

namespace EliteMud.Tests.Server;

/// <summary>One command, tick pass, join, leave or save read at a time (#61).</summary>
public class GameStateGateTests
{
    [Fact]
    public async Task TheNextOne_WaitsUntilTheGateIsReleased()
    {
        var gate = new GameStateGate();
        var first = await gate.EnterAsync(CancellationToken.None);

        var second = gate.EnterAsync(CancellationToken.None);
        await Task.Delay(100);
        Assert.False(second.IsCompleted);

        first.Dispose();
        (await second.WaitAsync(TimeSpan.FromSeconds(5))).Dispose();
    }

    /// <summary>A failed requested save asks again, but only on the game's turn: the flag is player state.</summary>
    [Fact]
    public async Task AFailedRequestedSave_SetsTheRetryFlag_OnlyUnderTheGate()
    {
        var gate = new GameStateGate();
        var player = new PlayerState(1, "Tester", 3001);

        Task retry;
        using (await gate.EnterAsync(CancellationToken.None))
        {
            retry = GameTickService.RetryIfFailedAsync(Task.FromResult(false), player, gate);
            await Task.Delay(100);
            Assert.False(player.SaveRequested); // the save failed, but the game still holds the gate
        }

        await retry.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(player.SaveRequested);
    }

    [Fact]
    public async Task ASuccessfulRequestedSave_IsNotRetried()
    {
        var player = new PlayerState(1, "Tester", 3001);

        await GameTickService.RetryIfFailedAsync(Task.FromResult(true), player, new GameStateGate());

        Assert.False(player.SaveRequested);
    }
}
