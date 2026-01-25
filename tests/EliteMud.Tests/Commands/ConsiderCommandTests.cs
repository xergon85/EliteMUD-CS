using EliteMud.Application.Commands.Consider;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Tests.Commands;

/// <summary>
/// Tests for the ConsiderHandler.
/// Tests difficulty estimation based on AC, HP, and combat rating comparisons.
/// Legacy reference: do_consider() in act.informative.c:2320-2411
/// </summary>
public class ConsiderCommandTests
{
    [Fact]
    public void Consider_NoTargetName_ReturnsError()
    {
        // Arrange
        var (handler, player, _) = CreateTestEnvironment();

        // Act
        var result = handler.Handle(player, "");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Consider killing who?", result.Message);
        Assert.Null(result.Target);
    }

    [Fact]
    public void Consider_NullTargetName_ReturnsError()
    {
        // Arrange
        var (handler, player, _) = CreateTestEnvironment();

        // Act
        var result = handler.Handle(player, null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Consider killing who?", result.Message);
        Assert.Null(result.Target);
    }

    [Fact]
    public void Consider_TargetNotInRoom_ReturnsError()
    {
        // Arrange
        var (handler, player, _) = CreateTestEnvironment();

        // Act
        var result = handler.Handle(player, "dragon");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Consider killing who?", result.Message);
        Assert.Null(result.Target);
    }

    [Fact]
    public void Consider_DeadTarget_ReturnsDeadMessage()
    {
        // Arrange
        var mob = CreateMob(1, "rat", level: 1, hp: 0, maxHp: 10);
        var (handler, player, _) = CreateTestEnvironment(mobs: new List<MobInstance> { mob });

        // Act
        var result = handler.Handle(player, "rat");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("I think it's dead already.", result.Message);
        Assert.NotNull(result.Target);
        Assert.Equal(mob, result.Target);
    }

    [Fact]
    public void Consider_EasyTarget_LowLevelMob()
    {
        // Arrange - Level 10 player vs Level 1 rat
        var mob = CreateMob(1, "rat", level: 1, hp: 10, maxHp: 10, ac: 100);
        var player = CreatePlayer(level: 10, hp: 100, maxHp: 100, ac: 50);
        var (handler, _, _) = CreateTestEnvironment(mobs: new List<MobInstance> { mob });

        // Act
        var result = handler.Handle(player, "rat");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Target);
        // Rating diff = 1 - 10 = -9, should give "You could do it with a needle!"
        var lines = result.Message.Split('\n');
        Assert.Contains("needle", lines[2], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Consider_HardTarget_HighLevelMob()
    {
        // Arrange - Level 1 player vs Level 20 dragon
        var mob = CreateMob(1, "dragon", level: 20, hp: 500, maxHp: 500, ac: 0);
        var player = CreatePlayer(level: 1, hp: 20, maxHp: 20, ac: 100);
        var (handler, _, _) = CreateTestEnvironment(mobs: new List<MobInstance> { mob });

        // Act
        var result = handler.Handle(player, "dragon");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Target);
        // Rating diff = 20 - 1 = 19, should give "Why not pretend you are dead instead?"
        var lines = result.Message.Split('\n');
        Assert.Contains("pretend you are dead", lines[2], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Consider_PerfectMatch_SameStats()
    {
        // Arrange - Level 5 player vs Level 5 guard with similar stats
        var mob = CreateMob(1, "guard", level: 5, hp: 50, maxHp: 50, ac: 60);
        var player = CreatePlayer(level: 5, hp: 50, maxHp: 50, ac: 60);
        var (handler, _, _) = CreateTestEnvironment(mobs: new List<MobInstance> { mob });

        // Act
        var result = handler.Handle(player, "guard");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Target);
        Assert.Contains("perfect match", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Consider_ArmorComparison_BetterArmored()
    {
        // Arrange - Player has AC 20 (better), mob has AC 100 (worse)
        var mob = CreateMob(1, "goblin", level: 5, hp: 50, maxHp: 50, ac: 100);
        var player = CreatePlayer(level: 5, hp: 50, maxHp: 50, ac: 20);
        var (handler, _, _) = CreateTestEnvironment(mobs: new List<MobInstance> { mob });

        // Act
        var result = handler.Handle(player, "goblin");

        // Assert
        Assert.True(result.Success);
        // AC diff = 100 - 20 = 80, should indicate victim lacks protection
        Assert.Contains("lacks", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Consider_ArmorComparison_WorseArmored()
    {
        // Arrange - Player has AC 100 (worse), mob has AC 20 (better)
        var mob = CreateMob(1, "knight", level: 5, hp: 50, maxHp: 50, ac: 20);
        var player = CreatePlayer(level: 5, hp: 50, maxHp: 50, ac: 100);
        var (handler, _, _) = CreateTestEnvironment(mobs: new List<MobInstance> { mob });

        // Act
        var result = handler.Handle(player, "knight");

        // Assert
        Assert.True(result.Success);
        // AC diff = 20 - 100 = -80, should indicate victim is better armored
        Assert.Contains("better armored", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Consider_HealthComparison_HealthierTarget()
    {
        // Arrange - Player has 50 HP, mob has 150 HP (3x healthier)
        var mob = CreateMob(1, "troll", level: 5, hp: 150, maxHp: 150, ac: 60);
        var player = CreatePlayer(level: 5, hp: 50, maxHp: 50, ac: 60);
        var (handler, _, _) = CreateTestEnvironment(mobs: new List<MobInstance> { mob });

        // Act
        var result = handler.Handle(player, "troll");

        // Assert
        Assert.True(result.Success);
        // HP diff = 100 - (150*100/50) = 100 - 300 = -200, should indicate massively healthier
        Assert.Contains("healthier", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Consider_HealthComparison_WeakerTarget()
    {
        // Arrange - Player has 150 HP, mob has 30 HP (much weaker)
        var mob = CreateMob(1, "rat", level: 5, hp: 30, maxHp: 30, ac: 60);
        var player = CreatePlayer(level: 5, hp: 150, maxHp: 150, ac: 60);
        var (handler, _, _) = CreateTestEnvironment(mobs: new List<MobInstance> { mob });

        // Act
        var result = handler.Handle(player, "rat");

        // Assert
        Assert.True(result.Success);
        // HP diff = 100 - (30*100/150) = 100 - 20 = 80, should indicate puny/weaker
        Assert.Contains("puny", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Consider_CaseInsensitiveTargetName()
    {
        // Arrange
        var mob = CreateMob(1, "GuardCaptain", level: 5, hp: 50, maxHp: 50, ac: 60);
        var (handler, player, _) = CreateTestEnvironment(mobs: new List<MobInstance> { mob });

        // Act - lowercase search for uppercase name
        var result = handler.Handle(player, "guardcaptain");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Target);
        Assert.Equal(mob, result.Target);
    }

    [Fact]
    public void Consider_PartialNameMatch()
    {
        // Arrange
        var mob = CreateMob(1, "city guard", level: 5, hp: 50, maxHp: 50, ac: 60);
        var (handler, player, _) = CreateTestEnvironment(mobs: new List<MobInstance> { mob });

        // Act - partial name "guard"
        var result = handler.Handle(player, "guard");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Target);
        Assert.Equal(mob, result.Target);
    }

    [Fact]
    public void Consider_FirstMatchingMob()
    {
        // Arrange - Multiple guards, should match first
        var guard1 = CreateMob(1, "guard", level: 5, hp: 50, maxHp: 50, ac: 60);
        var guard2 = CreateMob(2, "guard", level: 10, hp: 100, maxHp: 100, ac: 40);
        var (handler, player, _) = CreateTestEnvironment(mobs: new List<MobInstance> { guard1, guard2 });

        // Act
        var result = handler.Handle(player, "guard");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Target);
        Assert.Equal(guard1, result.Target);
    }

    [Fact]
    public void Consider_InjuredTarget_AffectsCombatRating()
    {
        // Arrange - Level 10 mob at 50% HP (effective rating = 5)
        var mob = CreateMob(1, "injured dragon", level: 10, hp: 50, maxHp: 100, ac: 60);
        var player = CreatePlayer(level: 5, hp: 50, maxHp: 50, ac: 60);
        var (handler, _, _) = CreateTestEnvironment(mobs: new List<MobInstance> { mob });

        // Act
        var result = handler.Handle(player, "dragon");

        // Assert
        Assert.True(result.Success);
        // Mob rating = 10 * (50/100) = 5
        // Player rating = 5 * (50/50) = 5
        // Rating diff = 0, should be "perfect match"
        Assert.Contains("perfect match", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Consider_InjuredPlayer_AffectsCombatRating()
    {
        // Arrange - Player at 25% HP
        var mob = CreateMob(1, "goblin", level: 5, hp: 50, maxHp: 50, ac: 60);
        var player = CreatePlayer(level: 8, hp: 20, maxHp: 80, ac: 60);
        var (handler, _, _) = CreateTestEnvironment(mobs: new List<MobInstance> { mob });

        // Act
        var result = handler.Handle(player, "goblin");

        // Assert
        Assert.True(result.Success);
        // Player rating = 8 * (20/80) = 2
        // Mob rating = 5 * (50/50) = 5
        // Rating diff = 3, should indicate difficulty
        Assert.Contains("luck", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Consider_MessageContainsThreeLines()
    {
        // Arrange
        var mob = CreateMob(1, "orc", level: 5, hp: 50, maxHp: 50, ac: 60);
        var (handler, player, _) = CreateTestEnvironment(mobs: new List<MobInstance> { mob });

        // Act
        var result = handler.Handle(player, "orc");

        // Assert
        Assert.True(result.Success);
        var lines = result.Message.Split('\n');
        Assert.Equal(3, lines.Length);
    }

    [Fact]
    public void Consider_VeryEasyTarget_ChickenMessage()
    {
        // Arrange - Level 20 player vs Level 1 rat
        var mob = CreateMob(1, "rat", level: 1, hp: 10, maxHp: 10, ac: 100);
        var player = CreatePlayer(level: 20, hp: 200, maxHp: 200, ac: 20);
        var (handler, _, _) = CreateTestEnvironment(mobs: new List<MobInstance> { mob });

        // Act
        var result = handler.Handle(player, "rat");

        // Assert
        Assert.True(result.Success);
        // Rating diff = 1 - 20 = -19, should be <= -10 (chicken message)
        Assert.Contains("chicken", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Consider_ImpossibleTarget_DumbPlayerMessage()
    {
        // Arrange - Level 1 player vs Level 50 ancient dragon
        var mob = CreateMob(1, "ancient dragon", level: 50, hp: 1000, maxHp: 1000, ac: -50);
        var player = CreatePlayer(level: 1, hp: 20, maxHp: 20, ac: 100);
        var (handler, _, _) = CreateTestEnvironment(mobs: new List<MobInstance> { mob });

        // Act
        var result = handler.Handle(player, "dragon");

        // Assert
        Assert.True(result.Success);
        // Rating diff = 50 - 1 = 49, should be > 30 (dumb player message)
        Assert.Contains("dumb player", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Helper methods

    private (ConsiderHandler handler, PlayerState player, WorldState worldState) CreateTestEnvironment(
        List<MobInstance>? mobs = null)
    {
        var rooms = new Dictionary<int, RoomDefinition>
        {
            [1] = new(1, "Test Room", "A test room.", new List<ExitDefinition>())
        };
        var worldDef = new WorldDefinition(rooms);

        var mobDefs = new Dictionary<int, MobDefinition>();
        var objDefs = new Dictionary<int, ObjectDefinition>();
        var roomMobs = new Dictionary<int, List<MobInstance>> { [1] = mobs ?? new() };
        var roomObjs = new Dictionary<int, List<ObjectInstance>>();
        var zones = new List<ZoneDefinition>();

        var worldState = new WorldState(worldDef, mobDefs, objDefs, roomMobs, roomObjs, zones);
        var player = CreatePlayer();
        var handler = new ConsiderHandler(worldState);

        return (handler, player, worldState);
    }

    private PlayerState CreatePlayer(
        byte level = 5,
        short hp = 50,
        short maxHp = 50,
        short ac = 60)
    {
        return new PlayerState(
            id: 1,
            name: "TestPlayer",
            roomId: 1,
            level: level)
        {
            HitPoints = hp,
            MaxHitPoints = maxHp,
            ArmorClass = ac
        };
    }

    private MobInstance CreateMob(
        int instanceId,
        string name,
        int level,
        short hp,
        int maxHp,
        int ac = 60)
    {
        var def = new MobDefinition(
            Id: instanceId,
            Name: name,
            ShortDescription: $"a {name}",
            LongDescription: $"A {name} is standing here.",
            Description: $"A typical {name}.",
            Level: level,
            Race: "Human",
            Class: "Warrior",
            Flags: new List<string>(),
            Stats: new StatBlock(10, 10, 10, 10, 10, 10),
            Resistances: new List<string>(),
            Skills: new List<string>(),
            ArmorClass: ac,
            MaxHitPoints: maxHp,
            Alignment: 0,
            Attacks: new List<MobAttack>(),
            Combat: null
        );

        return new MobInstance(instanceId, def)
        {
            HitPoints = hp
        };
    }
}
