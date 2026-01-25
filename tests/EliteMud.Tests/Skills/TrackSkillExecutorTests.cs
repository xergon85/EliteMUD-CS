using EliteMud.Application.Ai;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Skills;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Scripting;
using EliteMud.Server;

namespace EliteMud.Tests.Skills;

/// <summary>
/// Tests for TrackSkillExecutor - pathfinding skill that shows direction to target mobs.
/// </summary>
public class TrackSkillExecutorTests
{
    [Fact]
    public void Track_NoArgument_ReturnsError()
    {
        // Arrange
        var (executor, worldState, tracker) = CreateTestEnvironment();
        var context = new SkillContext(tracker, 1, null, null, "");

        // Act
        var result = executor.Execute(context);

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Messages);
        Assert.Equal(SkillMessageTarget.Actor, result.Messages[0].Target);
        Assert.Contains("Track whom?", result.Messages[0].Template);
    }

    [Fact]
    public void Track_NoSkillProficiency_ReturnsError()
    {
        // Arrange
        var (executor, worldState, tracker) = CreateTestEnvironment();
        tracker.SetSkill(SkillType.Track, 0); // No skill
        var context = new SkillContext(tracker, 1, null, null, "goblin");

        // Act
        var result = executor.Execute(context);

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Messages);
        Assert.Contains("don't know how to track", result.Messages[0].Template);
    }

    [Fact]
    public void Track_TargetNotFound_ReturnsError()
    {
        // Arrange
        var (executor, worldState, tracker) = CreateTestEnvironment();
        tracker.SetSkill(SkillType.Track, 95);
        var context = new SkillContext(tracker, 1, null, null, "dragon");

        // Act
        var result = executor.Execute(context);

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Messages);
        Assert.Contains("can't find a trail", result.Messages[0].Template);
    }

    [Fact]
    public void Track_TargetInSameRoom_ReturnsError()
    {
        // Arrange
        var (executor, worldState, tracker) = CreateTestEnvironment();
        tracker.SetSkill(SkillType.Track, 95);
        
        // Add goblin in same room as tracker (room 100)
        var goblin = CreateMob(1, "goblin warrior");
        AddMobToWorld(worldState, goblin, 100);

        var context = new SkillContext(tracker, 1, null, null, "goblin");

        // Act
        var result = executor.Execute(context);

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Messages);
        Assert.Contains("right here", result.Messages[0].Template);
    }

    [Fact]
    public void Track_SuccessfulTrack_ShowsDirection()
    {
        // Arrange
        var (executor, worldState, tracker) = CreateTestEnvironment();
        tracker.SetSkill(SkillType.Track, 100); // 100% success rate
        
        // Add goblin in room 101 (north of tracker in room 100)
        var goblin = CreateMob(1, "goblin warrior");
        AddMobToWorld(worldState, goblin, 101);

        var context = new SkillContext(tracker, 1, null, null, "goblin");

        // Act
        var result = executor.Execute(context);

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.Messages);
        
        // Should show direction
        var directionMsg = result.Messages[0];
        Assert.Equal(SkillMessageTarget.Actor, directionMsg.Target);
        Assert.Contains("sense a trail", directionMsg.Template);
        Assert.Contains("north", directionMsg.Template.ToLower());
    }

    [Fact]
    public void Track_AppliesWaitState()
    {
        // Arrange
        var (executor, worldState, tracker) = CreateTestEnvironment();
        tracker.SetSkill(SkillType.Track, 100);
        tracker.WaitState = 0; // No wait state
        
        // Add goblin in adjacent room
        var goblin = CreateMob(1, "goblin");
        AddMobToWorld(worldState, goblin, 101);

        var context = new SkillContext(tracker, 1, null, null, "goblin");

        // Act
        executor.Execute(context);

        // Assert
        Assert.Equal(CombatConstants.WaitStates.Track, tracker.WaitState);
    }

    [Fact]
    public void Track_CaseInsensitiveSearch_FindsTarget()
    {
        // Arrange
        var (executor, worldState, tracker) = CreateTestEnvironment();
        tracker.SetSkill(SkillType.Track, 100);
        
        // Add goblin with uppercase name
        var goblin = CreateMob(1, "GOBLIN Warrior");
        AddMobToWorld(worldState, goblin, 101);

        var context = new SkillContext(tracker, 1, null, null, "goblin");

        // Act
        var result = executor.Execute(context);

        // Assert
        Assert.True(result.Success);
        Assert.Contains(result.Messages, m => m.Template.Contains("sense a trail"));
    }

    [Fact]
    public void Track_PartialNameMatch_FindsTarget()
    {
        // Arrange
        var (executor, worldState, tracker) = CreateTestEnvironment();
        tracker.SetSkill(SkillType.Track, 100);
        
        // Add goblin with specific description
        var goblin = CreateMob(1, "fierce goblin warrior");
        AddMobToWorld(worldState, goblin, 101);

        var context = new SkillContext(tracker, 1, null, null, "warrior");

        // Act
        var result = executor.Execute(context);

        // Assert
        Assert.True(result.Success);
        Assert.Contains(result.Messages, m => m.Template.Contains("sense a trail"));
    }

    // ===== Helper Methods =====

    private (TrackSkillExecutor executor, WorldState worldState, PlayerState tracker) CreateTestEnvironment()
    {
        // Create world with simple graph
        var rooms = new Dictionary<int, RoomDefinition>
        {
            [100] = new(100, "Start Room", "Starting room",
                new List<ExitDefinition>
                {
                    new(Direction.North, 101),
                    new(Direction.East, 102)
                },
                RoomFlags.None, ZoneId: null),
            [101] = new(101, "North Room", "Room to the north",
                new List<ExitDefinition> { new(Direction.South, 100) },
                RoomFlags.None, ZoneId: null),
            [102] = new(102, "East Room", "Room to the east",
                new List<ExitDefinition> { new(Direction.West, 100) },
                RoomFlags.None, ZoneId: null)
        };

        var worldDef = new WorldDefinition(rooms);
        var mobDefs = new Dictionary<int, MobDefinition>();
        var objDefs = new Dictionary<int, ObjectDefinition>();
        var roomMobs = new Dictionary<int, List<MobInstance>>
        {
            [100] = new(),
            [101] = new(),
            [102] = new()
        };
        var roomObjs = new Dictionary<int, List<ObjectInstance>>();
        var zones = new List<ZoneDefinition>();

        var worldState = new WorldState(worldDef, mobDefs, objDefs, roomMobs, roomObjs, zones);

        // Create skill registry with track skill
        var contentRoot = FindContentRoot();
        var skills = ContentLoader.LoadSkills(contentRoot);
        var metadataRegistry = new SkillMetadataRegistry(skills);
        var formulaEvaluator = new FormulaEvaluator();
        var skillRegistry = new SkillRegistry(metadataRegistry, formulaEvaluator);

        // Create pathfinding service
        var pathfindingService = new PathfindingService();

        // Create track executor
        var executor = new TrackSkillExecutor(skillRegistry, pathfindingService, worldState);

        // Create test player
        var tracker = new PlayerState(1, "TestRanger", 100)
        {
            Level = 10,
            Position = Position.Standing
        };
        tracker.SetSkill(SkillType.Track, 75); // Default 75% proficiency

        return (executor, worldState, tracker);
    }

    private MobInstance CreateMob(int instanceId, string name)
    {
        var def = new MobDefinition(
            Id: instanceId,
            Name: name,
            ShortDescription: $"a {name}",
            LongDescription: $"A {name} is here.",
            Description: $"A {name}.",
            Level: 5,
            Race: "Goblin",
            Class: "Warrior",
            Flags: new List<string>(),
            Stats: new StatBlock(10, 10, 10, 10, 10, 10),
            Resistances: new List<string>(),
            Skills: new List<string>(),
            ArmorClass: 50,
            MaxHitPoints: 50,
            Alignment: -500,
            Attacks: new List<MobAttack>(),
            Combat: null
        );

        return new MobInstance(instanceId, def)
        {
            HitPoints = 50
        };
    }

    private void AddMobToWorld(WorldState worldState, MobInstance mob, int roomId)
    {
        // Access the internal mutable list via reflection
        var roomMobsField = typeof(WorldState).GetField("_roomMobs",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (roomMobsField != null)
        {
            var roomMobs = (Dictionary<int, List<MobInstance>>)roomMobsField.GetValue(worldState)!;
            if (!roomMobs.ContainsKey(roomId))
            {
                roomMobs[roomId] = new List<MobInstance>();
            }
            roomMobs[roomId].Add(mob);
        }
    }

    private static string FindContentRoot()
    {
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "content");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find content directory");
    }
}
