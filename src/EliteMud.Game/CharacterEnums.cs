namespace EliteMud.Game;

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
    
    // === MULTI-ATTACK SKILLS ===
    // Legacy: Used in perform_violence() loop for extra attacks per round
    SecondAttack = 326,   // Legacy: SKILL_2ATTACK = 326, difficulty 120
    ThirdAttack = 327,    // Legacy: SKILL_3ATTACK = 327, difficulty 140
    FourthAttack = 328,   // Legacy: SKILL_4ATTACK = 328, difficulty 160
    DualWield = 329,      // Legacy: SKILL_DUAL = 329, difficulty 160 (requires offhand weapon)
    
    // === PASSIVE DEFENSIVE SKILLS ===
    Dodge = 360,
    Parry = 361,
    Tumble = 362,
    
    // === UTILITY SKILLS ===
    Track = 330,      // Pathfinding skill - shows direction to target
    
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
    MaxMana = 12,          // Max Mana bonus
    MaxHit = 13,           // Max HP bonus
    MaxMovement = 14,      // Max Movement bonus
    
    // === COMBAT STATS ===
    Armor = 15,            // Flat AC bonus (no slot multiplier from items)
    ArmorClass = 17,       // AC modifier (negative is better, gets slot multiplier from Details)
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

/// <summary>
/// Types of saving throws.
/// Legacy: SAVING_PARA (paralysis/physical), SAVING_ROD, SAVING_PETRI, SAVING_BREATH, SAVING_SPELL
/// EliteMUD simplified to 4 types matching AffectLocation values.
/// </summary>
public enum SavingThrowType
{
    Physical = 0,  // Physical attacks, paralysis, petrification
    Mental = 1,    // Mind-affecting spells, charm, sleep
    Magic = 2,     // General magic spells
    Poison = 3     // Poison and disease
}
