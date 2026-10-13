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
}
