using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server;

namespace EliteMud.Tests.Commands;

public class KillTargetingTests
{
    [Fact]
    public void TargetParser_FindsFirstSoldier()
    {
        // Arrange
        var soldier1 = CreateMob(1, "soldier guard", "A soldier");
        var soldier2 = CreateMob(2, "soldier guard", "A soldier");
        var mobs = new List<MobInstance> { soldier1, soldier2 };

        // Act - "kill soldier" (should find first)
        var (index, name) = TargetParser.ParseTarget("soldier");
        var result = TargetParser.FindNthMatch(mobs, name, index);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.InstanceId);
    }

    [Fact]
    public void TargetParser_FindsSecondSoldier()
    {
        // Arrange
        var soldier1 = CreateMob(1, "soldier guard", "A soldier");
        var soldier2 = CreateMob(2, "soldier guard", "A soldier");
        var mobs = new List<MobInstance> { soldier1, soldier2 };

        // Act - "kill 2.soldier"
        var (index, name) = TargetParser.ParseTarget("2.soldier");
        var result = TargetParser.FindNthMatch(mobs, name, index);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.InstanceId);
    }

    [Fact]
    public void TargetParser_FindsThirdGuard()
    {
        // Arrange
        var guard1 = CreateMob(1, "guard soldier", "First guard");
        var guard2 = CreateMob(2, "guard soldier", "Second guard");
        var guard3 = CreateMob(3, "guard soldier", "Third guard");
        var mobs = new List<MobInstance> { guard1, guard2, guard3 };

        // Act - "kill 3.guard"
        var (index, name) = TargetParser.ParseTarget("3.guard");
        var result = TargetParser.FindNthMatch(mobs, name, index);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.InstanceId);
    }

    [Fact]
    public void TargetParser_ReturnsNullForInvalidIndex()
    {
        // Arrange
        var soldier = CreateMob(1, "soldier", "A soldier");
        var mobs = new List<MobInstance> { soldier };

        // Act - "kill 5.soldier" (only 1 soldier exists)
        var (index, name) = TargetParser.ParseTarget("5.soldier");
        var result = TargetParser.FindNthMatch(mobs, name, index);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TargetParser_InvalidFormat_ReturnsZeroIndex()
    {
        // Arrange
        var (index, name) = TargetParser.ParseTarget("abc.soldier");

        // Assert
        Assert.Equal(0, index);
        Assert.Equal("abc.soldier", name);
    }

    [Fact]
    public void TargetParser_NoPrefix_DefaultsToFirstMatch()
    {
        // Arrange
        var soldier1 = CreateMob(1, "soldier", "First soldier");
        var soldier2 = CreateMob(2, "soldier", "Second soldier");
        var mobs = new List<MobInstance> { soldier1, soldier2 };

        // Act - "kill soldier" (no index prefix)
        var (index, name) = TargetParser.ParseTarget("soldier");
        var result = TargetParser.FindNthMatch(mobs, name, index);

        // Assert
        Assert.Equal(1, index);
        Assert.NotNull(result);
        Assert.Equal(1, result.InstanceId);
    }

    private static MobInstance CreateMob(int instanceId, string name, string shortDesc)
    {
        var def = new MobDefinition(
            Id: instanceId,
            Name: name,
            ShortDescription: shortDesc,
            LongDescription: $"{shortDesc} is standing here.",
            Description: "A mob.",
            Level: 5,
            Race: "Human",
            Class: "Warrior",
            Flags: new List<string>(),
            Stats: new StatBlock(10, 10, 10, 10, 10, 10),
            Resistances: new List<string>(),
            Skills: new List<string>()
        );

        return new MobInstance(instanceId, def);
    }
}
