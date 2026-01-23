using EliteMud.Game;

namespace EliteMud.Tests.Skills;

/// <summary>
/// Tests for dodge passive skill and combat damage reduction.
/// Based on legacy EliteMUD fight.c:1543-1551
/// </summary>
public class DodgeSkillTests
{
    private readonly CombatCalculator _combatCalculator;

    public DodgeSkillTests()
    {
        // Create CombatCalculator with dodge and parry skills for tests
        var dodgeSkill = new DodgeSkill();
        var parrySkill = new ParrySkill();
        _combatCalculator = new CombatCalculator(dodgeSkill, parrySkill);
    }

    [Fact]
    public void ApplyDamage_NoDodgeSkill_TakesFullDamage()
    {
        // Arrange
        var victim = CreatePlayer(level: 10);
        victim.SetSkill(SkillType.Dodge, 0); // No dodge skill

        // Act
        var result = _combatCalculator.ApplyDamage(victim, 20);

        // Assert
        Assert.Equal(20, result.Damage);
        Assert.False(result.Dodged);
        Assert.Null(result.Message);
    }

    [Fact]
    public void ApplyDamage_HighDodgeSkill_CanDodge()
    {
        // Arrange
        var victim = CreatePlayer(level: 10);
        victim.SetSkill(SkillType.Dodge, 100); // 100% dodge skill
        victim.HitPoints = 100;

        // Act - Try 100 times with low damage
        // Formula: random(1,250) + damage < skill
        // With damage=1 and skill=100, dodge should trigger frequently
        int dodgeCount = 0;
        
        for (int i = 0; i < 100; i++)
        {
            victim.HitPoints = 100; // Reset HP
            var result = _combatCalculator.ApplyDamage(victim, 1);
            if (result.Dodged)
            {
                dodgeCount++;
            }
        }

        // Assert
        // With skill=100 and damage=1, check is: random(1,250)+1 < 100
        // Success when random roll is <=98, which is ~39% of the time
        // After 100 attempts, expect ~39 dodges (allow 20-60 range for randomness)
        Assert.True(dodgeCount > 20 && dodgeCount < 60, 
            $"Expected 20-60 dodges, got {dodgeCount}");
    }

    [Fact]
    public void ApplyDamage_DodgeSucceeds_ReducesDamageByTwoTimesLevel()
    {
        // Arrange
        var victim = CreatePlayer(level: 10);
        victim.SetSkill(SkillType.Dodge, 100);
        victim.HitPoints = 100;

        // Act - Keep trying until dodge succeeds
        DamageResult? dodgeResult = null;
        for (int i = 0; i < 1000; i++)
        {
            victim.HitPoints = 100;
            var result = _combatCalculator.ApplyDamage(victim, 50);
            if (result.Dodged)
            {
                dodgeResult = result;
                break;
            }
        }

        // Assert
        Assert.NotNull(dodgeResult);
        // Damage reduction = 2 * level = 2 * 10 = 20
        // Final damage = 50 - 20 = 30
        Assert.Equal(30, dodgeResult.Damage);
        Assert.Equal("You dodge the attack!", dodgeResult.Message);
    }

    [Fact]
    public void ApplyDamage_DodgeSucceeds_ImprovesDodgeSkill()
    {
        // Arrange
        var victim = CreatePlayer(level: 10);
        victim.SetSkill(SkillType.Dodge, 50); // Start at 50%
        victim.HitPoints = 100;

        // Act - Keep trying until dodge succeeds AND improves
        int skillBefore = victim.GetSkill(SkillType.Dodge);
        bool skillImproved = false;
        
        for (int i = 0; i < 10000 && !skillImproved; i++)
        {
            victim.HitPoints = 100;
            var result = _combatCalculator.ApplyDamage(victim, 10);
            
            if (result.Dodged && victim.GetSkill(SkillType.Dodge) > skillBefore)
            {
                skillImproved = true;
            }
        }

        // Assert
        Assert.True(skillImproved);
        Assert.True(victim.GetSkill(SkillType.Dodge) >= skillBefore);
    }

    [Fact]
    public void ApplyDamage_HighDamage_HarderToDodge()
    {
        // Arrange
        var victim = CreatePlayer(level: 10);
        victim.SetSkill(SkillType.Dodge, 100);
        victim.HitPoints = 1000;

        // Act - Test with high damage (harder to dodge)
        // Formula: random(1,250) + damage < skill
        // With damage=200 and skill=100, dodge is impossible
        int dodgeCount = 0;
        
        for (int i = 0; i < 100; i++)
        {
            victim.HitPoints = 1000;
            var result = _combatCalculator.ApplyDamage(victim, 200);
            if (result.Dodged)
            {
                dodgeCount++;
            }
        }

        // Assert - Should be 0 dodges (200 damage makes check impossible)
        Assert.Equal(0, dodgeCount);
    }

    [Fact]
    public void ApplyDamage_CanReduceDamageToZero()
    {
        // Arrange
        var victim = CreatePlayer(level: 50); // High level
        victim.SetSkill(SkillType.Dodge, 100);
        victim.HitPoints = 100;

        // Act - Try with low damage that dodge can completely negate
        // Dodge reduces by 2*level = 2*50 = 100
        DamageResult? result = null;
        for (int i = 0; i < 1000; i++)
        {
            victim.HitPoints = 100;
            var r = _combatCalculator.ApplyDamage(victim, 50);
            if (r.Dodged)
            {
                result = r;
                break;
            }
        }

        // Assert
        Assert.NotNull(result);
        // 50 damage - 100 reduction = -50, capped at 0
        Assert.Equal(0, result.Damage);
        Assert.True(result.Dodged);
    }

    [Fact]
    public void ApplyDamage_DamageCapAt500()
    {
        // Arrange
        var victim = CreatePlayer(level: 1);
        victim.SetSkill(SkillType.Dodge, 0);
        victim.HitPoints = 1000;

        // Act - Try to apply 600 damage
        var result = _combatCalculator.ApplyDamage(victim, 600);

        // Assert - Should cap at 500
        Assert.Equal(500, result.Damage);
        Assert.Equal(500, 1000 - victim.HitPoints); // Verify HP changed correctly
    }

    [Fact]
    public void ApplyDamage_UpdatesPosition_WhenHPDropsBelowZero()
    {
        // Arrange - Player with low HP, no dodge skill
        var victim = CreatePlayer(level: 10);
        victim.HitPoints = 10;
        victim.Position = Position.Fighting;

        // Act - Deal lethal damage (10 - 25 = -15, which is <= -11 for Dead)
        _combatCalculator.ApplyDamage(victim, 25);

        // Assert - Should update position to Dead
        Assert.True(victim.HitPoints <= -11);
        Assert.Equal(Position.Dead, victim.Position);
    }

    private static PlayerState CreatePlayer(int level = 10)
    {
        return new PlayerState(
            id: 1,
            name: "TestPlayer",
            roomId: 1000,
            level: (byte)level,
            characterClass: "Warrior",
            race: "Human",
            sex: 1
        );
    }
}
