using EliteMud.Application.Progression;
using EliteMud.Application.World.Validation;
using EliteMud.Game;
using EliteMud.Server;

namespace EliteMud.Tests.Server;

public class ContentValidatorTests
{
    private const int Temple = CharacterStart.StartRoom;

    [Fact]
    public void Validate_ExitToMissingRoom_IsAnError()
    {
        var findings = Validate(Rooms(new RoomDefinition(Temple, "Temple", "", new[] { new ExitDefinition(Direction.North, 1501) })));

        var finding = Assert.Single(findings, f => f.Category == "Exit to missing room");
        Assert.Equal(ContentSeverity.Error, finding.Severity);
        Assert.Contains("room 1501", finding.Message);
    }

    [Fact]
    public void Validate_DoorResetOnMissingExit_IsAnError()
    {
        // Closes a north exit the room doesn't have (legacy logs this as SYSERR)
        var rooms = Rooms(new RoomDefinition(Temple, "Temple", "", Array.Empty<ExitDefinition>()));

        var findings = Validate(rooms, zones: Zone(DoorReset(doorState: 1)));

        Assert.Contains(findings, f => f.Category == "Door reset on missing exit" && f.Severity == ContentSeverity.Error);
    }

    [Fact]
    public void Validate_ClosingAPlainExit_IsABarrier_AndOpeningItIsFine()
    {
        // Legacy 'D' sets closed/locked on any exit; on a non-door that makes a barrier (e.g. the Desert Moongate)
        var rooms = Rooms(
            new RoomDefinition(Temple, "Temple", "", new[] { new ExitDefinition(Direction.North, 2) }),
            new RoomDefinition(2, "Shrine", "", Array.Empty<ExitDefinition>()));

        var closed = Validate(rooms, zones: Zone(DoorReset(doorState: 2)));
        var opened = Validate(rooms, zones: Zone(DoorReset(doorState: 0)));

        Assert.Equal(ContentSeverity.Info, Assert.Single(closed, f => f.Category == "Barrier exit").Severity);
        Assert.DoesNotContain(opened, f => f.Category.StartsWith("Door reset") || f.Category == "Barrier exit");
    }

    [Fact]
    public void Validate_AcceptedFinding_IsMarkedWithItsReason()
    {
        var rooms = Rooms(new RoomDefinition(Temple, "Temple", "", new[] { new ExitDefinition(Direction.North, 1501) }));
        var accepted = new[] { new AcceptedFinding("Exit to missing room", "room 3001", "room 1501", "old Gnome City copy") };

        var findings = Validate(rooms, accepted: accepted);

        var finding = Assert.Single(findings, f => f.Category == "Exit to missing room");
        Assert.True(finding.IsAccepted);
        Assert.Equal("old Gnome City copy", finding.AcceptedReason);
        Assert.Equal(0, ContentValidationReport.CountErrors(findings));
    }

    [Fact]
    public void Validate_AcceptedEntryThatMatchesNothing_IsReported()
    {
        var rooms = Rooms(new RoomDefinition(Temple, "Temple", "", Array.Empty<ExitDefinition>()));
        var accepted = new[] { new AcceptedFinding("Exit to missing room", "room 23038", "", "fixed long ago") };

        var findings = Validate(rooms, accepted: accepted);

        Assert.Contains(findings, f => f.Category == "Accepted finding not found" && f.Severity == ContentSeverity.Warning);
    }

    [Fact]
    public void Validate_KeyThatNothingCreates_IsAWarning()
    {
        var door = new ExitDefinition(Direction.North, 2, ExitFlags: new[] { "IsDoor" }, KeyId: 7900);
        var rooms = Rooms(
            new RoomDefinition(Temple, "Temple", "", new[] { door }),
            new RoomDefinition(2, "Residence", "", Array.Empty<ExitDefinition>()));
        var objects = new Dictionary<int, ObjectDefinition> { [7900] = Key(7900) };

        var findings = Validate(rooms, objects: objects);

        var finding = Assert.Single(findings, f => f.Category == "Door key never created");
        Assert.Equal(ContentSeverity.Warning, finding.Severity);
    }

    [Fact]
    public void Validate_ZoneWithNoWayIn_IsUnreachable()
    {
        var rooms = Rooms(
            new RoomDefinition(Temple, "Temple", "", Array.Empty<ExitDefinition>()),
            new RoomDefinition(5700, "Dancing Daemon Inn", "", Array.Empty<ExitDefinition>()));
        var zones = new[]
        {
            new ZoneDefinition(30, "Midgaard", new RoomRange(3000, 3099), "", Array.Empty<ZoneResetDefinition>()),
            new ZoneDefinition(57, "New Thalos", new RoomRange(5600, 5999), "", Array.Empty<ZoneResetDefinition>())
        };

        var findings = Validate(rooms, zoneList: zones);

        var finding = Assert.Single(findings, f => f.Category == "Zone unreachable");
        Assert.Contains("zone 57", finding.Location);
    }

    [Fact]
    public void Validate_PortalCountsAsAWayIn()
    {
        var rooms = Rooms(
            new RoomDefinition(Temple, "Temple", "", Array.Empty<ExitDefinition>()),
            new RoomDefinition(12070, "Racetrack Seating", "", Array.Empty<ExitDefinition>()));
        var objects = new Dictionary<int, ObjectDefinition> { [12037] = Portal(12037) };
        var zones = new[]
        {
            new ZoneDefinition(30, "Midgaard", new RoomRange(3000, 3099), "", new[]
            {
                new ZoneResetDefinition("LoadPortal", ObjectId: 12037, null, RoomId: Temple, null, null, null, null, null, null,
                    IfFlag: false, PortalDestination: 12070)
            }),
            new ZoneDefinition(120, "Rome", new RoomRange(12000, 12199), "", Array.Empty<ZoneResetDefinition>())
        };

        var findings = Validate(rooms, objects: objects, zoneList: zones);

        Assert.DoesNotContain(findings, f => f.Category == "Zone unreachable");
    }

    /// <summary>
    /// The real zone data, checked like the server does, with content/meta/accepted-findings.json.
    /// Every error and warning must be fixed or accepted with a reason; a new one fails here, and so does
    /// an accept entry that no longer matches anything.
    /// </summary>
    [Fact]
    public void Validate_RealZones_HaveNoOpenErrorsOrWarnings()
    {
        var root = FindRepositoryRoot();
        var (world, mobs, objects, zones) = ContentLoader.LoadFromZoneFiles(Path.Combine(root, "zones"));
        Assert.NotNull(world);

        var findings = ContentValidator.Validate(
            world,
            mobs.GroupBy(mob => mob.Id).ToDictionary(group => group.Key, group => group.First()),
            objects.GroupBy(obj => obj.Id).ToDictionary(group => group.Key, group => group.First()),
            zones,
            ContentLoader.LoadAcceptedFindings(Path.Combine(root, "content")));

        var open = findings
            .Where(f => !f.IsAccepted && f.Severity != ContentSeverity.Info)
            .Select(f => $"{f.Severity} {f.Category}: {f.Location}: {f.Message}")
            .ToList();
        Assert.True(open.Count == 0, "Open content findings:\n" + string.Join("\n", open));
    }

    private static IReadOnlyList<ContentFinding> Validate(
        Dictionary<int, RoomDefinition> rooms,
        Dictionary<int, ObjectDefinition>? objects = null,
        ZoneDefinition? zones = null,
        IReadOnlyList<ZoneDefinition>? zoneList = null,
        IReadOnlyList<AcceptedFinding>? accepted = null) =>
        ContentValidator.Validate(
            new WorldDefinition(rooms),
            new Dictionary<int, MobDefinition>(),
            objects ?? new Dictionary<int, ObjectDefinition>(),
            zoneList ?? (zones is null ? Array.Empty<ZoneDefinition>() : new[] { zones }),
            accepted);

    private static ZoneResetDefinition DoorReset(int doorState) =>
        new("DoorState", null, null, Temple, null, null, null, null, DoorDirection: 0, DoorState: doorState, IfFlag: false);

    private static Dictionary<int, RoomDefinition> Rooms(params RoomDefinition[] rooms) => rooms.ToDictionary(room => room.Id);

    private static ZoneDefinition Zone(params ZoneResetDefinition[] resets) =>
        new(30, "Midgaard", new RoomRange(0, 9999), "", resets);

    private static ObjectDefinition Key(int id) =>
        new(id, "key", "a large key", "", "", "Key", new List<string>(), new List<string>(), null, new List<int>(), 1, 0, new List<ObjectAffect>());

    private static ObjectDefinition Portal(int id) =>
        new(id, "entrance", "an entrance", "", "", "Portal", new List<string>(), new List<string>(), null, new List<int> { 12070, 0, -1, -1, -1, -1 }, 0, 0, new List<ObjectAffect>());

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EliteMUD.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
