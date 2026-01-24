using EliteMud.Application.Skills;
using EliteMud.Game;
using EliteMud.Scripting;
using EliteMud.Application.World;

namespace EliteMud.Tests.Combat;

/// <summary>
/// Tests for alignment shift calculation when killing combatants.
/// Legacy: change_alignment() in fight.c:445-460
/// 
/// Formula: "If you kill a monster with alignment A, you move 1/16th of the way
/// to having alignment -A."
/// shift = (-victim_alignment - killer_alignment) >> 4
/// </summary>
public class AlignmentShiftTests
{
    private readonly CombatCalculator _combatCalculator;

    public AlignmentShiftTests()
    {
        // Create skill registry from JSON
        var registry = CreateSkillRegistry();
        
        // Create formula evaluator for Lua formulas
        var formulaEvaluator = new EliteMud.Scripting.FormulaEvaluator();
        
        // Create CombatCalculator with dodge and parry skills for tests
        var dodgeSkill = new Application.Skills.DodgeSkill(registry, formulaEvaluator);
        var parrySkill = new Application.Skills.ParrySkill(registry, formulaEvaluator);
        _combatCalculator = new CombatCalculator(dodgeSkill, parrySkill);
    }

    [Fact]
    public void CalculateAlignmentShift_NeutralKillsEvil_ShiftsTowardGood()
    {
        // Arrange
        var player = CreatePlayer(level: 10, alignment: 0);
        var evilMob = CreateMob(level: 10, alignment: -500);
        
        // Act
        int shift = _combatCalculator.CalculateAlignmentShift(player, evilMob, isPvP: false);
        
        // Assert
        // Target: -(-500) = 500
        // Current: 0
        // Difference: 500 - 0 = 500
        // Shift: 500 >> 4 = 31
        Assert.Equal(31, shift);
    }
    
    [Fact]
    public void CalculateAlignmentShift_NeutralKillsVeryEvil_LargerGoodShift()
    {
        // Arrange
        var player = CreatePlayer(level: 10, alignment: 0);
        var veryEvilMob = CreateMob(level: 10, alignment: -1000);
        
        // Act
        int shift = _combatCalculator.CalculateAlignmentShift(player, veryEvilMob, isPvP: false);
        
        // Assert
        // Target: -(-1000) = 1000
        // Current: 0
        // Difference: 1000 - 0 = 1000
        // Shift: 1000 >> 4 = 62
        Assert.Equal(62, shift);
    }

    [Fact]
    public void CalculateAlignmentShift_NeutralKillsGood_ShiftsTowardEvil()
    {
        // Arrange
        var player = CreatePlayer(level: 10, alignment: 0);
        var goodMob = CreateMob(level: 10, alignment: 500);
        
        // Act
        int shift = _combatCalculator.CalculateAlignmentShift(player, goodMob, isPvP: false);
        
        // Assert
        // Target: -(500) = -500
        // Current: 0
        // Difference: -500 - 0 = -500
        // Shift: -500 / 16 = -31 (rounds toward zero)
        Assert.Equal(-31, shift);
    }
    
    [Fact]
    public void CalculateAlignmentShift_NeutralKillsNeutral_NoShift()
    {
        // Arrange
        var player = CreatePlayer(level: 10, alignment: 0);
        var neutralMob = CreateMob(level: 10, alignment: 0);
        
        // Act
        int shift = _combatCalculator.CalculateAlignmentShift(player, neutralMob, isPvP: false);
        
        // Assert
        // Target: -(0) = 0
        // Current: 0
        // Difference: 0 - 0 = 0
        // Shift: 0 >> 4 = 0
        Assert.Equal(0, shift);
    }

    [Fact]
    public void CalculateAlignmentShift_GoodKillsEvil_AlreadyAtTarget()
    {
        // Arrange
        var goodPlayer = CreatePlayer(level: 10, alignment: 500);
        var evilMob = CreateMob(level: 10, alignment: -500);
        
        // Act
        int shift = _combatCalculator.CalculateAlignmentShift(goodPlayer, evilMob, isPvP: false);
        
        // Assert
        // Target: -(-500) = 500
        // Current: 500
        // Difference: 500 - 500 = 0
        // Shift: 0 >> 4 = 0 (already at target, no shift)
        Assert.Equal(0, shift);
    }
    
    [Fact]
    public void CalculateAlignmentShift_EvilKillsGood_AlreadyAtTarget()
    {
        // Arrange
        var evilPlayer = CreatePlayer(level: 10, alignment: -500);
        var goodMob = CreateMob(level: 10, alignment: 500);
        
        // Act
        int shift = _combatCalculator.CalculateAlignmentShift(evilPlayer, goodMob, isPvP: false);
        
        // Assert
        // Target: -(500) = -500
        // Current: -500
        // Difference: -500 - (-500) = 0
        // Shift: 0 >> 4 = 0 (already at target, no shift)
        Assert.Equal(0, shift);
    }
    
    [Fact]
    public void CalculateAlignmentShift_VeryGoodKillsVeryEvil_SmallShift()
    {
        // Arrange
        var veryGoodPlayer = CreatePlayer(level: 10, alignment: 700);
        var veryEvilMob = CreateMob(level: 10, alignment: -1000);
        
        // Act
        int shift = _combatCalculator.CalculateAlignmentShift(veryGoodPlayer, veryEvilMob, isPvP: false);
        
        // Assert
        // Target: -(-1000) = 1000
        // Current: 700
        // Difference: 1000 - 700 = 300
        // Shift: 300 >> 4 = 18 (small because already close to target)
        Assert.Equal(18, shift);
    }

    [Fact]
    public void CalculateAlignmentShift_LevelDoesNotMatter()
    {
        // Arrange - Same alignment, different levels
        var player = CreatePlayer(level: 10, alignment: 0);
        var lowLevelMob = CreateMob(level: 1, alignment: -500);
        var highLevelMob = CreateMob(level: 100, alignment: -500);
        
        // Act
        int shift1 = _combatCalculator.CalculateAlignmentShift(player, lowLevelMob, isPvP: false);
        int shift2 = _combatCalculator.CalculateAlignmentShift(player, highLevelMob, isPvP: false);
        
        // Assert - Level doesn't affect alignment shift in legacy
        Assert.Equal(shift1, shift2);
        Assert.Equal(31, shift1); // Both should be 500 >> 4 = 31
    }

    [Fact]
    public void CalculateAlignmentShift_PvPUsesSameFormula()
    {
        // Arrange
        var killer = CreatePlayer(level: 10, alignment: 0);
        var victim = CreatePlayer(level: 10, alignment: -500);
        
        // Act
        int pvpShift = _combatCalculator.CalculateAlignmentShift(killer, victim, isPvP: true);
        int pveShift = _combatCalculator.CalculateAlignmentShift(killer, CreateMob(level: 10, alignment: -500), isPvP: false);
        
        // Assert - Legacy uses same formula for PvP and PvE
        Assert.Equal(pvpShift, pveShift);
        Assert.Equal(31, pvpShift); // 500 >> 4 = 31
    }
    
    [Fact]
    public void CalculateAlignmentShift_NegativeShiftRoundsCorrectly()
    {
        // Arrange
        var player = CreatePlayer(level: 10, alignment: 0);
        var goodMob = CreateMob(level: 10, alignment: 1000);
        
        // Act
        int shift = _combatCalculator.CalculateAlignmentShift(player, goodMob, isPvP: false);
        
        // Assert
        // Target: -(1000) = -1000
        // Current: 0
        // Difference: -1000 - 0 = -1000
        // Shift: -1000 / 16 = -62 (rounds toward zero)
        Assert.Equal(-62, shift);
    }

    [Fact]
    public void CalculateAlignmentShift_SmallDifference_RoundsToZero()
    {
        // Arrange
        var player = CreatePlayer(level: 10, alignment: 0);
        var slightlyEvilMob = CreateMob(level: 10, alignment: -15);
        
        // Act
        int shift = _combatCalculator.CalculateAlignmentShift(player, slightlyEvilMob, isPvP: false);
        
        // Assert
        // Target: -(-15) = 15
        // Current: 0
        // Difference: 15 - 0 = 15
        // Shift: 15 >> 4 = 0 (15/16 rounds down to 0)
        Assert.Equal(0, shift);
    }
    
    [Fact]
    public void CalculateAlignmentShift_ConvergentBehavior_SlowsNearTarget()
    {
        // Arrange - Player getting closer to target
        var player1 = CreatePlayer(level: 10, alignment: 0);
        var player2 = CreatePlayer(level: 10, alignment: 300);
        var player3 = CreatePlayer(level: 10, alignment: 450);
        var evilMob = CreateMob(level: 10, alignment: -500);
        
        // Act - All kill same evil mob (target = 500)
        int shift1 = _combatCalculator.CalculateAlignmentShift(player1, evilMob, isPvP: false);
        int shift2 = _combatCalculator.CalculateAlignmentShift(player2, evilMob, isPvP: false);
        int shift3 = _combatCalculator.CalculateAlignmentShift(player3, evilMob, isPvP: false);
        
        // Assert - Shifts decrease as you get closer to target
        Assert.Equal(31, shift1); // (500 - 0) >> 4 = 31
        Assert.Equal(12, shift2); // (500 - 300) >> 4 = 12
        Assert.Equal(3, shift3);  // (500 - 450) >> 4 = 3
        Assert.True(shift1 > shift2 && shift2 > shift3);
    }

    [Fact]
    public void ApplyAlignmentShift_IncreasesPlayerAlignment()
    {
        // Arrange
        var player = CreatePlayer(level: 10, alignment: 0);
        
        // Act
        _combatCalculator.ApplyAlignmentShift(player, 50);
        
        // Assert
        Assert.Equal(50, player.Alignment);
    }
    
    [Fact]
    public void ApplyAlignmentShift_DecreasesPlayerAlignment()
    {
        // Arrange
        var player = CreatePlayer(level: 10, alignment: 0);
        
        // Act
        _combatCalculator.ApplyAlignmentShift(player, -50);
        
        // Assert
        Assert.Equal(-50, player.Alignment);
    }

    [Fact]
    public void ApplyAlignmentShift_ClampsToMaximum1000()
    {
        // Arrange
        var player = CreatePlayer(level: 10, alignment: 950);
        
        // Act
        _combatCalculator.ApplyAlignmentShift(player, 100);
        
        // Assert
        Assert.Equal(1000, player.Alignment); // Clamped to max
    }
    
    [Fact]
    public void ApplyAlignmentShift_ClampsToMinimumNegative1000()
    {
        // Arrange
        var player = CreatePlayer(level: 10, alignment: -950);
        
        // Act
        _combatCalculator.ApplyAlignmentShift(player, -100);
        
        // Assert
        Assert.Equal(-1000, player.Alignment); // Clamped to min
    }
    
    [Fact]
    public void ApplyAlignmentShift_MultipleKills_ConvergesTowardTarget()
    {
        // Arrange
        var player = CreatePlayer(level: 10, alignment: 0);
        var evilMob = CreateMob(level: 10, alignment: -500);
        
        // Act - Kill same mob type 5 times
        for (int i = 0; i < 5; i++)
        {
            int shift = _combatCalculator.CalculateAlignmentShift(player, evilMob, isPvP: false);
            _combatCalculator.ApplyAlignmentShift(player, shift);
        }
        
        // Assert - Should converge toward 500 (opposite of -500)
        // Kill 1: 0 → 31 (shift = (500-0)>>4 = 31)
        // Kill 2: 31 → 60 (shift = (500-31)>>4 = 29)
        // Kill 3: 60 → 87 (shift = (500-60)>>4 = 27)
        // Kill 4: 87 → 112 (shift = (500-87)>>4 = 25)
        // Kill 5: 112 → 136 (shift = (500-112)>>4 = 24)
        Assert.InRange(player.Alignment, 130, 140);
    }

    private static PlayerState CreatePlayer(int level = 10, short alignment = 0)
    {
        var player = new PlayerState(
            id: 1,
            name: "TestPlayer",
            roomId: 1000,
            level: (byte)level,
            characterClass: "Warrior",
            race: "Human",
            sex: 1
        );
        player.Alignment = alignment;
        return player;
    }
    
    private static MobInstance CreateMob(int level = 10, short alignment = 0)
    {
        var definition = new MobDefinition(
            9999, // Id
            "test mob", // Name
            "a test mob", // ShortDescription
            "A test mob for testing", // LongDescription
            "Test", // Description
            level, // Level
            "Test", // Race
            "Warrior", // Class
            new List<string>(), // Flags
            new StatBlock(10, 10, 10, 10, 10, 10), // Stats
            new List<string>(), // Resistances
            new List<string>(), // Skills
            0, // ArmorClass
            100, // MaxHitPoints
            alignment, // Alignment
            new List<MobAttack>(), // Attacks
            null // Combat
        );
        
        return new MobInstance(1, definition);
    }

    private static SkillMetadataRegistry CreateSkillRegistry()
    {
        var contentRoot = FindContentRoot();
        var skillsById = EliteMud.Server.ContentLoader.LoadSkills(contentRoot);
        return new SkillMetadataRegistry(skillsById);
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
