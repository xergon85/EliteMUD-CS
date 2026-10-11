using EliteMud.Game;

namespace EliteMud.Tests.Server;

/// <summary>
/// Door states in the real zone data after the startup reset.
/// </summary>
public class ZoneContentDoorTests
{
    [Fact]
    public void Playroom_GreenDoor_StartsClosed()
    {
        // 64.zon "D 0 6461 1 1"; the door type was fixed in content/meta/legacy-import.json
        Assert.True(RealWorld.State.IsExitClosed(6461, Direction.East));
    }

    [Fact]
    public void DesertMoongate_IsALockedBarrier()
    {
        // 26.zon "D 0 2747 4 2" on an exit that isn't a door
        var state = RealWorld.State.GetDoorState(2747, Direction.Up);

        Assert.NotNull(state);
        Assert.True(state.IsClosed);
        Assert.True(state.IsLocked);
    }
}
