using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Tests.Server;

public class WorldStatePortalResetTests
{
    private const int RoomId = 12060;
    private const int PortalId = 12037;

    [Fact]
    public void ResetZone_LoadPortal_PlacesPortalWithResetDestination()
    {
        var worldState = CreateWorld(
            LoadPortal(destination: 12071, ifFlag: false));

        worldState.ResetZone(120);

        var portal = Assert.Single(worldState.GetObjectsInRoom(RoomId));
        Assert.Equal(PortalId, portal.Definition.Id);
        Assert.Equal(12071, portal.Portal!.Destination); // reset overrides the object's own destination
    }

    [Fact]
    public void ResetZone_ActivatePortalAfterLoad_SetsStateAndKey()
    {
        // Legacy 120.zon: "L 0 12060 12037 12070" then "A 1 12060 1 12038"
        var worldState = CreateWorld(
            LoadPortal(destination: 12070, ifFlag: false),
            ActivatePortal(flags: 1, keyId: 12038, ifFlag: true));

        worldState.ResetZone(120);

        var portal = Assert.Single(worldState.GetObjectsInRoom(RoomId)).Portal!;
        Assert.True(portal.IsClosed);
        Assert.Equal(12038, portal.KeyObjectId);
    }

    [Fact]
    public void ResetZone_ActivatePortalWithIfFlag_SkippedWhenLoadFails()
    {
        var worldState = CreateWorld(
            LoadPortal(destination: -5, ifFlag: false), // invalid destination, so the load fails
            ActivatePortal(flags: 2, keyId: 999, ifFlag: true));

        worldState.ResetZone(120);

        Assert.Empty(worldState.GetObjectsInRoom(RoomId));
    }

    private static ZoneResetDefinition LoadPortal(int destination, bool ifFlag) =>
        new("LoadPortal", ObjectId: PortalId, MobId: null, RoomId: RoomId, MaxExisting: null,
            SpawnChance: null, EquipSlot: null, ContainerId: null, DoorDirection: null, DoorState: null,
            IfFlag: ifFlag, PortalDestination: destination);

    private static ZoneResetDefinition ActivatePortal(int flags, int keyId, bool ifFlag) =>
        new("ActivatePortal", ObjectId: null, MobId: null, RoomId: RoomId, MaxExisting: null,
            SpawnChance: null, EquipSlot: null, ContainerId: null, DoorDirection: null, DoorState: null,
            IfFlag: ifFlag, PortalFlags: flags, PortalKeyId: keyId);

    private static WorldState CreateWorld(params ZoneResetDefinition[] resets)
    {
        var worldDef = new WorldDefinition(new Dictionary<int, RoomDefinition>
        {
            [RoomId] = new(RoomId, "Circus Maximus - Entrance", "Desc", new List<ExitDefinition>())
        });

        var portalDef = new ObjectDefinition(
            PortalId, "entrance circus", "Circus Maximus entrance", "Long", "Desc", "Portal",
            new List<string>(), new List<string>(), null, new List<int> { 12070, 2, 12038, -1, -1, -1 },
            0, 0, new List<ObjectAffect>());

        var zone = new ZoneDefinition(120, "Rome", new RoomRange(12000, 12199), "ResetAlways", resets);

        return new WorldState(
            worldDef,
            new Dictionary<int, MobDefinition>(),
            new Dictionary<int, ObjectDefinition> { [PortalId] = portalDef },
            new Dictionary<int, List<MobInstance>> { [RoomId] = new() },
            new Dictionary<int, List<ObjectInstance>> { [RoomId] = new() },
            new List<ZoneDefinition> { zone });
    }
}
