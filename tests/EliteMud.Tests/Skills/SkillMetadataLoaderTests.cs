using EliteMud.Game;
using EliteMud.Server;

namespace EliteMud.Tests.Skills;

public sealed class SkillMetadataLoaderTests
{
    [Fact]
    public void LoadSkills_ShouldLoadAllDefinedSkills()
    {
        // Arrange
        var contentRoot = FindContentRoot();
        
        // Act
        var skills = ContentLoader.LoadSkills(contentRoot);
        
        // Assert
        Assert.NotNull(skills);
        Assert.NotEmpty(skills);
        Assert.Equal(7, skills.Count); // kick, bash, backstab, rescue, dodge, parry, tumble
    }
    
    [Fact]
    public void LoadSkills_ShouldLoadKickMetadata()
    {
        // Arrange
        var contentRoot = FindContentRoot();
        
        // Act
        var skills = ContentLoader.LoadSkills(contentRoot);
        
        // Assert
        Assert.True(skills.ContainsKey(323)); // Kick ID
        var kick = skills[323];
        
        Assert.Equal(323, kick.Id);
        Assert.Equal("kick", kick.Name);
        Assert.Empty(kick.Aliases);
        Assert.Equal("Active", kick.Type);
        Assert.Equal("Combat", kick.Category);
        Assert.Equal(1, kick.MinimumLevel);
        Assert.Equal(3, kick.WaitStateRounds);
        Assert.Equal(60, kick.SkillgainCooldown);
        
        // Check mechanics
        Assert.NotNull(kick.Mechanics);
        Assert.Equal("return math.max(1, level / 2)", kick.Mechanics.DamageFormula);
        Assert.Equal("return ((10 - victimAC/10) * 2) + random(1,101) <= skillPercent", kick.Mechanics.HitFormula);
    }
    
    [Fact]
    public void LoadSkills_ShouldLoadBackstabMetadataWithAlias()
    {
        // Arrange
        var contentRoot = FindContentRoot();
        
        // Act
        var skills = ContentLoader.LoadSkills(contentRoot);
        
        // Assert
        Assert.True(skills.ContainsKey(315)); // Backstab ID
        var backstab = skills[315];
        
        Assert.Equal(315, backstab.Id);
        Assert.Equal("backstab", backstab.Name);
        Assert.Contains("bs", backstab.Aliases);
        Assert.Equal("Active", backstab.Type);
        Assert.Equal("Stealth", backstab.Category);
        
        // Check mechanics
        Assert.NotNull(backstab.Mechanics);
        Assert.Equal("return math.min(math.floor(level / 10) + 1, 5)", backstab.Mechanics.DamageMultiplierFormula);
    }
    
    [Fact]
    public void LoadSkills_ShouldLoadClassRestrictions()
    {
        // Arrange
        var contentRoot = FindContentRoot();
        
        // Act
        var skills = ContentLoader.LoadSkills(contentRoot);
        
        // Assert
        var kick = skills[323];
        Assert.NotNull(kick.ClassRestrictions);
        
        // Warriors can learn kick at level 1
        Assert.True(kick.ClassRestrictions.ContainsKey("Warrior"));
        var warriorRestriction = kick.ClassRestrictions["Warrior"];
        Assert.Equal(1, warriorRestriction.MinLevel);
        Assert.Equal(95, warriorRestriction.MaxProficiency);
        Assert.Equal(10, warriorRestriction.Difficulty);
        
        // Magic Users cannot learn kick
        Assert.True(kick.ClassRestrictions.ContainsKey("MagicUser"));
        var magicUserRestriction = kick.ClassRestrictions["MagicUser"];
        Assert.Null(magicUserRestriction.MinLevel);
        Assert.Equal(0, magicUserRestriction.MaxProficiency);
    }
    
    [Fact]
    public void LoadSkills_ShouldLoadDodgeAsPassiveSkill()
    {
        // Arrange
        var contentRoot = FindContentRoot();
        
        // Act
        var skills = ContentLoader.LoadSkills(contentRoot);
        
        // Assert
        Assert.True(skills.ContainsKey(360)); // Dodge ID
        var dodge = skills[360];
        
        Assert.Equal("Passive", dodge.Type);
        Assert.Equal("Defensive", dodge.Category);
        Assert.Equal(0, dodge.WaitStateRounds); // Passive skills have no wait state
        
        // Check passive mechanics
        Assert.NotNull(dodge.Mechanics);
        Assert.Equal("return (random(1,250) + damage) < skillPercent", dodge.Mechanics.ActivationFormula);
        Assert.Equal("return math.max(0, damage - (level * 2))", dodge.Mechanics.EffectFormula);
    }
    
    [Fact]
    public void LoadSkills_ShouldLoadRequirementsAndEffects()
    {
        // Arrange
        var contentRoot = FindContentRoot();
        
        // Act
        var skills = ContentLoader.LoadSkills(contentRoot);
        
        // Assert
        var bash = skills[324];
        Assert.NotNull(bash.Mechanics);
        
        // Check requirements
        Assert.NotNull(bash.Mechanics.Requirements);
        var positionReq = bash.Mechanics.Requirements.FirstOrDefault(r => r.Type == "position");
        Assert.NotNull(positionReq);
        Assert.Equal("Fighting", positionReq.Value);
        Assert.Equal("You can't bash while sitting down!", positionReq.Message);
        
        // Check effects
        Assert.NotNull(bash.Mechanics.Effects);
        var knockdownEffect = bash.Mechanics.Effects.FirstOrDefault(e => e.Type == "onHit" && e.Target == "victim");
        Assert.NotNull(knockdownEffect);
        Assert.Equal("setPosition", knockdownEffect.Effect);
        Assert.Equal("Sitting", knockdownEffect.Value);
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
