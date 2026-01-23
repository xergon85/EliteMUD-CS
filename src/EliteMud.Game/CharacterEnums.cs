namespace EliteMud.Game;

/// <summary>
/// Playable character races in EliteMUD
/// Based on legacy system - 13 mortal races
/// </summary>
public enum Race
{
    Human = 0,
    Troll = 1,
    Halfling = 2,
    Dwarf = 3,
    Gnome = 4,
    Elf = 5,
    HalfElf = 6,
    Fairy = 7,
    Minotaur = 8,
    Ratman = 9,
    Drow = 10,
    Lizardman = 11,
    Draconian = 12
}

/// <summary>
/// Character sex/gender
/// </summary>
public enum Sex
{
    Neutral = 0,  // Not used for players, only NPCs
    Male = 1,
    Female = 2
}

/// <summary>
/// Character position state
/// Based on legacy POS_* constants
/// </summary>
public enum Position
{
    Dead = 0,
    MortallyWounded = 1,
    Incapacitated = 2,
    Stunned = 3,
    Sleeping = 4,
    Resting = 5,
    Sitting = 6,
    Fighting = 7,
    Standing = 8
    // Note: Legacy also had Swimming(10), Diving(11), Hoovering(12), Flying(13)
    // Not implementing those yet
}

/// <summary>
/// Character classes in EliteMUD
/// Based on legacy system - 20+ classes including multi-class options
/// </summary>
public enum CharacterClass
{
    // Base classes (1-4)
    MagicUser = 1,
    Cleric = 2,
    Thief = 3,
    Warrior = 4,
    
    // Advanced classes (5-19)
    Psionicist = 5,
    Monk = 6,
    Bard = 7,
    Knight = 8,
    Wizard = 9,
    Druid = 10,
    Assassin = 11,
    Ranger = 12,
    Illusionist = 13,
    Paladin = 14,
    Mariner = 15,
    Cavalier = 16,
    Ninja = 19,  // Note: 17-18 unused in legacy
    
    // Multi-class (20+)
    TwoClassMulti = 20,    // Dual-class (requires 1CLASS and 2CLASS fields)
    ThreeClassMulti = 21   // Triple-class (requires 1CLASS, 2CLASS, 3CLASS fields)
}

/// <summary>
/// Display names for races
/// </summary>
public static class RaceNames
{
    public static readonly Dictionary<Race, string> Names = new()
    {
        { Race.Human, "Human" },
        { Race.Troll, "Troll" },
        { Race.Halfling, "Halfling" },
        { Race.Dwarf, "Dwarf" },
        { Race.Gnome, "Gnome" },
        { Race.Elf, "Elf" },
        { Race.HalfElf, "Half-elf" },
        { Race.Fairy, "Fairy" },
        { Race.Minotaur, "Minotaur" },
        { Race.Ratman, "Ratman" },
        { Race.Drow, "Drow" },
        { Race.Lizardman, "Lizardman" },
        { Race.Draconian, "Draconian" }
    };
}

/// <summary>
/// Display names for classes
/// </summary>
public static class ClassNames
{
    public static readonly Dictionary<CharacterClass, string> Names = new()
    {
        { CharacterClass.MagicUser, "Magic-user" },
        { CharacterClass.Cleric, "Cleric" },
        { CharacterClass.Thief, "Thief" },
        { CharacterClass.Warrior, "Warrior" },
        { CharacterClass.Psionicist, "Psionicist" },
        { CharacterClass.Monk, "Monk" },
        { CharacterClass.Bard, "Bard" },
        { CharacterClass.Knight, "Knight" },
        { CharacterClass.Wizard, "Wizard" },
        { CharacterClass.Druid, "Druid" },
        { CharacterClass.Assassin, "Assassin" },
        { CharacterClass.Ranger, "Ranger" },
        { CharacterClass.Illusionist, "Illusionist" },
        { CharacterClass.Paladin, "Paladin" },
        { CharacterClass.Mariner, "Mariner" },
        { CharacterClass.Cavalier, "Cavalier" },
        { CharacterClass.Ninja, "Ninja" },
        { CharacterClass.TwoClassMulti, "Multi-class (Dual)" },
        { CharacterClass.ThreeClassMulti, "Multi-class (Triple)" }
    };
}

/// <summary>
/// Allowed classes per race (bitmap representation from legacy system)
/// This determines which classes are available during character creation
/// </summary>
public static class AllowedClasses
{
    // Bitmap flags for each class
    private const int MagicUserFlag = 1 << 0;
    private const int ClericFlag = 1 << 1;
    private const int ThiefFlag = 1 << 2;
    private const int WarriorFlag = 1 << 3;
    private const int PsionicistFlag = 1 << 4;
    private const int MonkFlag = 1 << 5;
    private const int BardFlag = 1 << 6;
    private const int KnightFlag = 1 << 7;
    private const int WizardFlag = 1 << 8;
    private const int DruidFlag = 1 << 9;
    private const int AssassinFlag = 1 << 10;
    private const int RangerFlag = 1 << 11;
    private const int IllusionistFlag = 1 << 12;
    private const int PaladinFlag = 1 << 13;
    private const int MarinerFlag = 1 << 14;
    private const int CavalierFlag = 1 << 15;
    private const int NinjaFlag = 1 << 18;
    
    // Basic classes available to all races
    private const int BaseClasses = MagicUserFlag | ClericFlag | ThiefFlag | WarriorFlag;
    
    /// <summary>
    /// Get list of allowed classes for a given race
    /// Note: Multi-class options require additional logic and are not included here
    /// </summary>
    public static List<CharacterClass> GetAllowedClasses(Race race)
    {
        var bitmap = GetClassBitmap(race);
        var classes = new List<CharacterClass>();
        
        // Check each class flag
        if ((bitmap & MagicUserFlag) != 0) classes.Add(CharacterClass.MagicUser);
        if ((bitmap & ClericFlag) != 0) classes.Add(CharacterClass.Cleric);
        if ((bitmap & ThiefFlag) != 0) classes.Add(CharacterClass.Thief);
        if ((bitmap & WarriorFlag) != 0) classes.Add(CharacterClass.Warrior);
        if ((bitmap & PsionicistFlag) != 0) classes.Add(CharacterClass.Psionicist);
        if ((bitmap & MonkFlag) != 0) classes.Add(CharacterClass.Monk);
        if ((bitmap & BardFlag) != 0) classes.Add(CharacterClass.Bard);
        if ((bitmap & KnightFlag) != 0) classes.Add(CharacterClass.Knight);
        if ((bitmap & WizardFlag) != 0) classes.Add(CharacterClass.Wizard);
        if ((bitmap & DruidFlag) != 0) classes.Add(CharacterClass.Druid);
        if ((bitmap & AssassinFlag) != 0) classes.Add(CharacterClass.Assassin);
        if ((bitmap & RangerFlag) != 0) classes.Add(CharacterClass.Ranger);
        if ((bitmap & IllusionistFlag) != 0) classes.Add(CharacterClass.Illusionist);
        if ((bitmap & PaladinFlag) != 0) classes.Add(CharacterClass.Paladin);
        if ((bitmap & MarinerFlag) != 0) classes.Add(CharacterClass.Mariner);
        if ((bitmap & CavalierFlag) != 0) classes.Add(CharacterClass.Cavalier);
        if ((bitmap & NinjaFlag) != 0) classes.Add(CharacterClass.Ninja);
        
        return classes;
    }
    
    private static int GetClassBitmap(Race race)
    {
        // For now, all races get base classes
        // TODO: Load actual race/class restrictions from legacy data or config
        return race switch
        {
            Race.Human => 0xFFFFFF,      // All classes
            Race.Elf => BaseClasses | WizardFlag | RangerFlag | DruidFlag,
            Race.Dwarf => BaseClasses | KnightFlag | CavalierFlag,
            Race.Halfling => BaseClasses | BardFlag | DruidFlag,
            Race.Gnome => BaseClasses | IllusionistFlag,
            Race.HalfElf => BaseClasses | BardFlag | RangerFlag | DruidFlag,
            Race.Drow => BaseClasses | WizardFlag | AssassinFlag,
            _ => BaseClasses  // Default: base 4 classes for other races
        };
    }
}

/// <summary>
/// Spell enumeration.
/// Based on legacy system - spells use IDs 0-299
/// </summary>
public enum SpellType
{
    // === DAMAGE SPELLS ===
    MagicMissile = 1,      // Low damage, always hits
    BurningHands = 7,      // Fire damage cone
    LightningBolt = 26,    // High single-target lightning damage
    
    // === HEALING SPELLS ===
    CureLightWounds = 28,  // 1d8 + level/2 healing
    CureSeriousWounds = 29, // 2d8 + level healing
    
    // === BUFF SPELLS ===
    Armor = 15,            // -20 AC buff
    Bless = 16,            // +2 hitroll buff
    
    // TODO: Add remaining spells as needed (0-299 range available)
}

/// <summary>
/// Skills enumeration.
/// Based on legacy system - skills use IDs 300-399
/// </summary>
public enum SkillType
{
    // === ACTIVE COMBAT SKILLS ===
    Backstab = 315,   // Legacy: SKILL_BACKSTAB = 315
    Kick = 323,       // Legacy: SKILL_KICK = 323
    Bash = 324,       // Legacy: SKILL_BASH = 324
    Rescue = 325,     // Legacy: SKILL_RESCUE = 325
    
    // === PASSIVE DEFENSIVE SKILLS ===
    Dodge = 360,
    Parry = 361,
    Tumble = 362,
    
    // TODO: Add remaining skills as needed (300-399 range)
}

/// <summary>
/// Type of affect/buff/debuff applied to a character.
/// Uses spell IDs from legacy system where applicable.
/// </summary>
public enum AffectType
{
    // === BUFF SPELLS ===
    Armor = 15,            // Spell ID from legacy - AC bonus
    Bless = 16,            // Spell ID from legacy - hitroll bonus
    
    // === DEBUFF SPELLS ===
    Curse = 27,            // Spell ID from legacy - hitroll penalty
    Poison = 33,           // Spell ID from legacy - periodic damage
    
    // === DETECTION SPELLS ===
    DetectInvisibility = 8,
    DetectMagic = 9,
    DetectPoison = 10,
    
    // === PROTECTION SPELLS ===
    Sanctuary = 36,        // Damage reduction
    
    // TODO: Add more as needed
}

/// <summary>
/// Location (stat) that an affect modifies.
/// Based on legacy APPLY_* constants from utils.h
/// </summary>
public enum AffectLocation
{
    None = 0,
    
    // === CORE STATS ===
    Strength = 1,
    Dexterity = 2,
    Intelligence = 3,
    Wisdom = 4,
    Constitution = 5,
    Charisma = 6,
    
    // === VITALS ===
    MaxHit = 13,           // Max HP bonus
    MaxMana = 12,          // Max Mana bonus
    MaxMovement = 14,      // Max Movement bonus
    
    // === COMBAT STATS ===
    ArmorClass = 17,       // AC modifier (negative is better)
    Hitroll = 18,          // To-hit bonus
    Damroll = 19,          // Damage bonus
    
    // === SAVES ===
    SavingPhysical = 20,
    SavingMental = 21,
    SavingMagic = 22,
    SavingPoison = 23,
    
    // === OTHER ===
    MagicResistance = 24,
    
    // TODO: Add more APPLY_* locations as needed (see LegacyImportLookup.ApplyFromIndex)
}
