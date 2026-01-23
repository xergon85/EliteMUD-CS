using EliteMud.Game;

namespace EliteMud.Tests.Skills;

/// <summary>
/// Tests for basic skill storage, retrieval, and improvement mechanics.
/// Based on legacy EliteMUD skills system research.
/// </summary>
public class SkillSystemTests
{
    [Fact]
    public void GetSkill_DefaultValue_ReturnsZero()
    {
        // Arrange
        var player = CreatePlayer();

        // Act
        var skillValue = player.GetSkill(SkillType.Kick);

        // Assert
        Assert.Equal(0, skillValue);
    }

    [Fact]
    public void SetSkill_ValidValue_StoresCorrectly()
    {
        // Arrange
        var player = CreatePlayer();

        // Act
        player.SetSkill(SkillType.Kick, 75);

        // Assert
        Assert.Equal(75, player.GetSkill(SkillType.Kick));
    }

    [Fact]
    public void SetSkill_OverMaximum_CapsAt100()
    {
        // Arrange
        var player = CreatePlayer();

        // Act
        player.SetSkill(SkillType.Kick, 150);

        // Assert
        Assert.Equal(100, player.GetSkill(SkillType.Kick));
    }

    [Fact]
    public void SetSkill_MultipleSkills_StoresIndependently()
    {
        // Arrange
        var player = CreatePlayer();

        // Act
        player.SetSkill(SkillType.Kick, 75);
        player.SetSkill(SkillType.Dodge, 50);
        player.SetSkill(SkillType.Bash, 90);

        // Assert
        Assert.Equal(75, player.GetSkill(SkillType.Kick));
        Assert.Equal(50, player.GetSkill(SkillType.Dodge));
        Assert.Equal(90, player.GetSkill(SkillType.Bash));
    }

    [Fact]
    public void TryImproveSkill_MaxedSkill_ReturnsFalse()
    {
        // Arrange
        var player = CreatePlayer();
        player.SetSkill(SkillType.Kick, 100);

        // Act
        var improved = player.TryImproveSkill(SkillType.Kick);

        // Assert
        Assert.False(improved);
        Assert.Equal(100, player.GetSkill(SkillType.Kick));
    }

    [Fact]
    public void TryImproveSkill_LowSkill_EventuallyImproves()
    {
        // Arrange
        var player = CreatePlayer();
        player.SetSkill(SkillType.Kick, 10);

        // Act - Try 1000 times (should improve at least once)
        bool everImproved = false;
        int finalSkill = 10;
        
        for (int i = 0; i < 1000; i++)
        {
            if (player.TryImproveSkill(SkillType.Kick))
            {
                everImproved = true;
            }
            finalSkill = player.GetSkill(SkillType.Kick);
        }

        // Assert
        // With 10% skill, improvement chance is 90% per attempt
        // After 1000 attempts, it's virtually guaranteed to improve
        Assert.True(everImproved);
        Assert.True(finalSkill > 10);
    }

    [Fact]
    public void TryImproveSkill_HighSkill_RarelyImproves()
    {
        // Arrange
        var player = CreatePlayer();
        player.SetSkill(SkillType.Kick, 95);

        // Act - Try 100 times
        int improvementCount = 0;
        
        for (int i = 0; i < 100; i++)
        {
            if (player.TryImproveSkill(SkillType.Kick))
            {
                improvementCount++;
            }
        }

        // Assert
        // With 95% skill, improvement chance is only 5% per attempt
        // After 100 attempts, expected improvements: ~5
        // We'll check it's less than 20 (to avoid flaky tests)
        Assert.True(improvementCount < 20);
    }

    [Fact]
    public void TryImproveSkill_IncrementsByOne()
    {
        // Arrange
        var player = CreatePlayer();
        player.SetSkill(SkillType.Kick, 50);

        // Act - Force improvement by trying many times
        int originalSkill = player.GetSkill(SkillType.Kick);
        
        while (player.GetSkill(SkillType.Kick) == originalSkill)
        {
            player.TryImproveSkill(SkillType.Kick);
        }

        // Assert
        Assert.Equal(originalSkill + 1, player.GetSkill(SkillType.Kick));
    }

    [Fact]
    public void Skills_MultipleCharacters_IndependentStorage()
    {
        // Arrange
        var player1 = CreatePlayer("Player1");
        var player2 = CreatePlayer("Player2");

        // Act
        player1.SetSkill(SkillType.Kick, 75);
        player2.SetSkill(SkillType.Kick, 25);

        // Assert
        Assert.Equal(75, player1.GetSkill(SkillType.Kick));
        Assert.Equal(25, player2.GetSkill(SkillType.Kick));
    }

    private static PlayerState CreatePlayer(string name = "TestPlayer")
    {
        return new PlayerState(
            id: 1,
            name: name,
            roomId: 1000,
            level: 10,
            characterClass: "Warrior",
            race: "Human",
            sex: 1
        );
    }
}
