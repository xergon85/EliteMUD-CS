using EliteMud.Game;

namespace EliteMud.Tests.Skills;

public sealed class SkillMetadataRegistryTests
{
    [Fact]
    public void GetById_ShouldReturnKickMetadata()
    {
        // Arrange
        var registry = CreateTestRegistry();
        
        // Act
        var kick = registry.GetById(323);
        
        // Assert
        Assert.NotNull(kick);
        Assert.Equal("kick", kick.Name);
        Assert.Equal(323, kick.Id);
    }
    
    [Fact]
    public void GetBySkillType_ShouldReturnBackstabMetadata()
    {
        // Arrange
        var registry = CreateTestRegistry();
        
        // Act
        var backstab = registry.GetBySkillType(SkillType.Backstab);
        
        // Assert
        Assert.NotNull(backstab);
        Assert.Equal("backstab", backstab.Name);
        Assert.Equal(315, backstab.Id);
    }
    
    [Fact]
    public void GetByName_ShouldBeCaseInsensitive()
    {
        // Arrange
        var registry = CreateTestRegistry();
        
        // Act
        var kickLower = registry.GetByName("kick");
        var kickUpper = registry.GetByName("KICK");
        var kickMixed = registry.GetByName("Kick");
        
        // Assert
        Assert.NotNull(kickLower);
        Assert.NotNull(kickUpper);
        Assert.NotNull(kickMixed);
        Assert.Same(kickLower, kickUpper);
        Assert.Same(kickLower, kickMixed);
    }
    
    [Fact]
    public void GetByAlias_ShouldReturnBackstabFromBs()
    {
        // Arrange
        var registry = CreateTestRegistry();
        
        // Act
        var backstab = registry.GetByAlias("bs");
        
        // Assert
        Assert.NotNull(backstab);
        Assert.Equal("backstab", backstab.Name);
        Assert.Equal(315, backstab.Id);
    }
    
    [Fact]
    public void GetByNameOrAlias_ShouldCheckBothNameAndAlias()
    {
        // Arrange
        var registry = CreateTestRegistry();
        
        // Act
        var kickByName = registry.GetByNameOrAlias("kick");
        var backstabByName = registry.GetByNameOrAlias("backstab");
        var backstabByAlias = registry.GetByNameOrAlias("bs");
        
        // Assert
        Assert.NotNull(kickByName);
        Assert.Equal("kick", kickByName.Name);
        
        Assert.NotNull(backstabByName);
        Assert.Equal("backstab", backstabByName.Name);
        
        Assert.NotNull(backstabByAlias);
        Assert.Equal("backstab", backstabByAlias.Name);
        Assert.Same(backstabByName, backstabByAlias);
    }
    
    [Fact]
    public void TryGet_ShouldReturnTrueForValidSkill()
    {
        // Arrange
        var registry = CreateTestRegistry();
        
        // Act
        var found = registry.TryGet("kick", out var metadata);
        
        // Assert
        Assert.True(found);
        Assert.NotNull(metadata);
        Assert.Equal("kick", metadata.Name);
    }
    
    [Fact]
    public void TryGet_ShouldReturnFalseForInvalidSkill()
    {
        // Arrange
        var registry = CreateTestRegistry();
        
        // Act
        var found = registry.TryGet("invalid_skill", out var metadata);
        
        // Assert
        Assert.False(found);
        Assert.Null(metadata);
    }
    
    [Fact]
    public void HasSkill_ShouldReturnTrueForExistingSkills()
    {
        // Arrange
        var registry = CreateTestRegistry();
        
        // Act & Assert
        Assert.True(registry.HasSkill(323)); // Kick
        Assert.True(registry.HasSkill(SkillType.Bash));
        Assert.True(registry.HasSkill(SkillType.Backstab));
    }
    
    [Fact]
    public void HasSkill_ShouldReturnFalseForNonExistentSkills()
    {
        // Arrange
        var registry = CreateTestRegistry();
        
        // Act & Assert
        Assert.False(registry.HasSkill(999));
    }
    
    [Fact]
    public void GetAll_ShouldReturnAllSkills()
    {
        // Arrange
        var registry = CreateTestRegistry();
        
        // Act
        var allSkills = registry.GetAll().ToList();
        
        // Assert
        Assert.Equal(8, allSkills.Count);
        Assert.Contains(allSkills, s => s.Name == "kick");
        Assert.Contains(allSkills, s => s.Name == "bash");
        Assert.Contains(allSkills, s => s.Name == "backstab");
        Assert.Contains(allSkills, s => s.Name == "rescue");
        Assert.Contains(allSkills, s => s.Name == "track");
        Assert.Contains(allSkills, s => s.Name == "dodge");
        Assert.Contains(allSkills, s => s.Name == "parry");
        Assert.Contains(allSkills, s => s.Name == "tumble");
    }
    
    [Fact]
    public void Count_ShouldReturnCorrectNumber()
    {
        // Arrange
        var registry = CreateTestRegistry();
        
        // Act
        var count = registry.Count;
        
        // Assert
        Assert.Equal(8, count);
    }
    
    [Fact]
    public void GetById_ShouldReturnNullForNonExistentSkill()
    {
        // Arrange
        var registry = CreateTestRegistry();
        
        // Act
        var skill = registry.GetById(999);
        
        // Assert
        Assert.Null(skill);
    }
    
    [Fact]
    public void GetByName_ShouldReturnNullForNonExistentSkill()
    {
        // Arrange
        var registry = CreateTestRegistry();
        
        // Act
        var skill = registry.GetByName("nonexistent");
        
        // Assert
        Assert.Null(skill);
    }
    
    private static SkillMetadataRegistry CreateTestRegistry()
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
