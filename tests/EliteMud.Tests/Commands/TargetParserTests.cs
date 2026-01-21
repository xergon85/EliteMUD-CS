using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Tests.Commands;

public class TargetParserTests
{
    [Fact]
    public void ParseTarget_WithSimpleName_ReturnsIndex1()
    {
        // Arrange
        var input = "corpse";

        // Act
        var (index, name) = TargetParser.ParseTarget(input);

        // Assert
        Assert.Equal(1, index);
        Assert.Equal("corpse", name);
    }

    [Fact]
    public void ParseTarget_WithIndexedName_ReturnsCorrectIndexAndName()
    {
        // Arrange
        var input = "2.corpse";

        // Act
        var (index, name) = TargetParser.ParseTarget(input);

        // Assert
        Assert.Equal(2, index);
        Assert.Equal("corpse", name);
    }

    [Fact]
    public void ParseTarget_WithAllPrefix_ReturnsNegativeOne()
    {
        // Arrange
        var input = "all.corpse";

        // Act
        var (index, name) = TargetParser.ParseTarget(input);

        // Assert
        Assert.Equal(-1, index);
        Assert.Equal("corpse", name);
    }

    [Fact]
    public void ParseTarget_WithInvalidNumber_ReturnsZero()
    {
        // Arrange
        var input = "abc.corpse";

        // Act
        var (index, name) = TargetParser.ParseTarget(input);

        // Assert
        Assert.Equal(0, index);
        Assert.Equal("abc.corpse", name);
    }

    [Fact]
    public void ParseTarget_WithNoDot_ReturnsIndex1()
    {
        // Arrange
        var input = "sword";

        // Act
        var (index, name) = TargetParser.ParseTarget(input);

        // Assert
        Assert.Equal(1, index);
        Assert.Equal("sword", name);
    }

    [Fact]
    public void ParseTarget_WithEmptyString_ReturnsIndex1()
    {
        // Arrange
        var input = "";

        // Act
        var (index, name) = TargetParser.ParseTarget(input);

        // Assert
        Assert.Equal(1, index);
        Assert.Equal("", name);
    }

    [Fact]
    public void ParseTarget_WithLargeIndex_ParsesCorrectly()
    {
        // Arrange
        var input = "99.corpse";

        // Act
        var (index, name) = TargetParser.ParseTarget(input);

        // Assert
        Assert.Equal(99, index);
        Assert.Equal("corpse", name);
    }

    [Fact]
    public void FindNthMatch_WithFirstMatch_ReturnsFirstObject()
    {
        // Arrange
        var obj1 = CreateTestObject(1, "corpse");
        var obj2 = CreateTestObject(2, "sword");
        var obj3 = CreateTestObject(3, "corpse");
        var objects = new List<ObjectInstance> { obj1, obj2, obj3 };

        // Act
        var result = TargetParser.FindNthMatch(objects, "corpse", 1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.InstanceId);
    }

    [Fact]
    public void FindNthMatch_WithSecondMatch_ReturnsSecondObject()
    {
        // Arrange
        var obj1 = CreateTestObject(1, "corpse");
        var obj2 = CreateTestObject(2, "sword");
        var obj3 = CreateTestObject(3, "corpse");
        var objects = new List<ObjectInstance> { obj1, obj2, obj3 };

        // Act
        var result = TargetParser.FindNthMatch(objects, "corpse", 2);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.InstanceId);
    }

    [Fact]
    public void FindNthMatch_WithIndexTooHigh_ReturnsNull()
    {
        // Arrange
        var obj1 = CreateTestObject(1, "corpse");
        var objects = new List<ObjectInstance> { obj1 };

        // Act
        var result = TargetParser.FindNthMatch(objects, "corpse", 5);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void FindNthMatch_WithNoMatch_ReturnsNull()
    {
        // Arrange
        var obj1 = CreateTestObject(1, "sword");
        var objects = new List<ObjectInstance> { obj1 };

        // Act
        var result = TargetParser.FindNthMatch(objects, "corpse", 1);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void FindNthMatch_WithPartialKeyword_Matches()
    {
        // Arrange - name has "corpse pcorpse" keywords
        var obj1 = CreateTestObject(1, "corpse pcorpse");
        var objects = new List<ObjectInstance> { obj1 };

        // Act - search with partial match
        var result = TargetParser.FindNthMatch(objects, "corp", 1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.InstanceId);
    }

    [Fact]
    public void FindNthMatch_WithInvalidIndex_ReturnsNull()
    {
        // Arrange
        var obj1 = CreateTestObject(1, "corpse");
        var objects = new List<ObjectInstance> { obj1 };

        // Act - index 0 or negative
        var result = TargetParser.FindNthMatch(objects, "corpse", 0);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void FindAllMatches_WithMultipleMatches_ReturnsAll()
    {
        // Arrange
        var obj1 = CreateTestObject(1, "corpse");
        var obj2 = CreateTestObject(2, "sword");
        var obj3 = CreateTestObject(3, "corpse");
        var obj4 = CreateTestObject(4, "corpse");
        var objects = new List<ObjectInstance> { obj1, obj2, obj3, obj4 };

        // Act
        var results = TargetParser.FindAllMatches(objects, "corpse");

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Contains(results, r => r.InstanceId == 1);
        Assert.Contains(results, r => r.InstanceId == 3);
        Assert.Contains(results, r => r.InstanceId == 4);
    }

    [Fact]
    public void FindAllMatches_WithNoMatches_ReturnsEmptyList()
    {
        // Arrange
        var obj1 = CreateTestObject(1, "sword");
        var objects = new List<ObjectInstance> { obj1 };

        // Act
        var results = TargetParser.FindAllMatches(objects, "corpse");

        // Assert
        Assert.Empty(results);
    }

    private static ObjectInstance CreateTestObject(int instanceId, string name)
    {
        var def = new ObjectDefinition(
            Id: instanceId,
            Name: name,
            ShortDescription: $"a {name}",
            LongDescription: $"A {name} is here.",
            Description: $"It's a {name}.",
            Type: "container",
            WearSlots: new List<string>(),
            Flags: new List<string>(),
            Details: null,
            Values: new List<int> { 0, 0, 0, 0 },
            Weight: 10,
            Cost: 100
        );
        return new ObjectInstance(instanceId, def);
    }
}
