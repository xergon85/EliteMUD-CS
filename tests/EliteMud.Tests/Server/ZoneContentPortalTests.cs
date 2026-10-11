using EliteMud.Application.World;

namespace EliteMud.Tests.Server;

/// <summary>
/// Checks that the real zone files place portals the way the legacy game did.
/// Loads zones/ from the repository, like server startup does.
/// </summary>
public class ZoneContentPortalTests
{
    [Fact]
    public void Rome_CircusPortal_IsClosedAndOpenedByTicket()
    {
        // Legacy 120.zon: "L 0 12060 12037 12070" and "A 1 12060 1 12038"
        var portal = FindPortal(roomId: 12060, objectId: 12037);

        Assert.Equal(12070, portal.Destination);
        Assert.True(portal.IsClosed);
        Assert.Equal(12038, portal.KeyObjectId); // an circus ticket
    }

    [Fact]
    public void Rome_ArenaPortal_UsesDestinationFromReset()
    {
        // Legacy 120.zon: "L 0 12125 12032 12141"; the object itself says 12041
        var portal = FindPortal(roomId: 12125, objectId: 12032);

        Assert.Equal(12141, portal.Destination);
    }

    private static PortalState FindPortal(int roomId, int objectId)
    {
        var portal = RealWorld.State.GetObjectsInRoom(roomId)
            .SingleOrDefault(obj => obj.Definition.Id == objectId);

        Assert.NotNull(portal);
        Assert.NotNull(portal.Portal);
        return portal.Portal;
    }
}
