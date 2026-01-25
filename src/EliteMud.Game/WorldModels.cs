namespace EliteMud.Game;

public enum Direction
{
    North,
    East,
    South,
    West,
    Up,
    Down
}

public sealed record ExitDefinition(Direction Direction, int TargetRoomId);

public sealed record RoomDefinition(
    int Id, 
    string Name, 
    string Description, 
    IReadOnlyList<ExitDefinition> Exits, 
    RoomFlags Flags = RoomFlags.None, 
    int? ZoneId = null)
{
    // Clean string properties on construction
    public string Name { get; init; } = TextCleaner.Clean(Name);
    public string Description { get; init; } = TextCleaner.Clean(Description);
}

public sealed record ScriptDefinition(string Id, string Hook, string Body, int? RoomId);

/// <summary>
/// Represents a timed effect that modifies character stats.
/// Examples: Armor spell (-20 AC), Bless (+2 hitroll), Poison (periodic damage)
/// Duration measured in MUD hours (75 seconds per hour via PULSE_REGEN).
/// </summary>
public sealed class Affect
{
    /// <summary>
    /// Type of affect (for identification and stacking rules).
    /// </summary>
    public required AffectType Type { get; init; }
    
    /// <summary>
    /// Stat/attribute being modified.
    /// </summary>
    public required AffectLocation Location { get; init; }
    
    /// <summary>
    /// Amount to modify the stat (+/-).
    /// Example: -20 for Armor (better AC), +2 for Bless (better hitroll)
    /// </summary>
    public required int Modifier { get; init; }
    
    /// <summary>
    /// Duration remaining in MUD hours (75 seconds each).
    /// Decremented each PULSE_REGEN tick (every 75 seconds).
    /// Affect expires when this reaches 0.
    /// </summary>
    public required int DurationHours { get; set; }
    
    /// <summary>
    /// Optional source of the affect for display purposes.
    /// Examples: "armor", "sword of flames", null for item-based affects
    /// </summary>
    public string? Source { get; init; }
    
    /// <summary>
    /// Message shown to the character when this affect is applied.
    /// Example: "You feel someone protecting you."
    /// </summary>
    public string? ToCharMessage { get; init; }
    
    /// <summary>
    /// Message shown to the room when this affect is applied.
    /// Example: "$n is surrounded by a white aura."
    /// </summary>
    public string? ToRoomMessage { get; init; }
    
    /// <summary>
    /// Message shown to the character when this affect wears off.
    /// Example: "You feel less protected."
    /// </summary>
    public string? WearOffMessage { get; init; }
}

public sealed record StatBlock(
    int Strength,
    int Dexterity,
    int Intelligence,
    int Wisdom,
    int Constitution,
    int Charisma);

/// <summary>
/// Represents a mob's natural attack (claws, bite, etc.)
/// Legacy: ch->mob_specials.attacks[] array
/// </summary>
public sealed record MobAttack(
    string Type,
    int DamageType,
    int Chance,
    int DamageDiceCount,
    int DamageDiceSides,
    int DamageBonus);

/// <summary>
/// Represents a mob's base combat stats (before equipment)
/// Legacy: ch->points.armor, ch->points.hitroll, ch->points.damroll
/// </summary>
public sealed record MobCombat(
    int Hitroll,
    int Damroll);

public sealed record MobDefinition(
    int Id,
    string Name,
    string ShortDescription,
    string LongDescription,
    string Description,
    int Level,
    string Race,
    string Class,
    IReadOnlyList<string> Flags,
    StatBlock Stats,
    IReadOnlyList<string> Resistances,
    IReadOnlyList<string> Skills,
    int ArmorClass,
    int MaxHitPoints,
    int Alignment,
    IReadOnlyList<MobAttack> Attacks,
    MobCombat? Combat,
    int? Hometown = null)
{
    // Clean string properties on construction
    public string Name { get; init; } = TextCleaner.Clean(Name);
    public string ShortDescription { get; init; } = TextCleaner.Clean(ShortDescription);
    public string LongDescription { get; init; } = TextCleaner.Clean(LongDescription);
    public string Description { get; init; } = TextCleaner.Clean(Description);
    public string Race { get; init; } = TextCleaner.Clean(Race);
    public string Class { get; init; } = TextCleaner.Clean(Class);
    
    /// <summary>
    /// Parses legacy string flags into MobFlags enum.
    /// Examples: "SENTINEL", "AGGRESSIVE", "MEMORY"
    /// </summary>
    public MobFlags ParsedFlags { get; init; } = ParseFlags(Flags);
    
    private static MobFlags ParseFlags(IReadOnlyList<string> flags)
    {
        var result = MobFlags.None;
        
        foreach (var flag in flags)
        {
            var normalized = flag.ToUpperInvariant().Replace("MOB_", "").Replace(" ", "");
            
            result |= normalized switch
            {
                "SENTINEL" => MobFlags.Sentinel,
                "SCAVENGER" => MobFlags.Scavenger,
                "AGGRESSIVE" => MobFlags.Aggressive,
                "STAYZONE" or "STAY_ZONE" => MobFlags.StayZone,
                "WIMPY" => MobFlags.Wimpy,
                "AGGRESSIVEEVIL" or "AGGRESSIVE_EVIL" => MobFlags.AggressiveEvil,
                "AGGRESSIVEGOOD" or "AGGRESSIVE_GOOD" => MobFlags.AggressiveGood,
                "AGGRESSIVENEUTRAL" or "AGGRESSIVE_NEUTRAL" => MobFlags.AggressiveNeutral,
                "MEMORY" => MobFlags.Memory,
                "HELPER" => MobFlags.Helper,
                _ => MobFlags.None
            };
        }
        
        return result;
    }
}

/// <summary>
/// Represents a stat modification applied by an object when equipped.
/// Examples: +2 hitroll from a sword, -10 AC from armor, +1 STR from gauntlets
/// </summary>
public sealed record ObjectAffect(
    AffectLocation Location,
    int Modifier);

public sealed record ObjectDefinition(
    int Id,
    string Name,
    string ShortDescription,
    string LongDescription,
    string Description,
    string Type,
    IReadOnlyList<string> WearSlots,
    IReadOnlyList<string> Flags,
    ObjectDetails? Details,
    IReadOnlyList<int> Values,
    int Weight,
    int Cost,
    IReadOnlyList<ObjectAffect> Affects)
{
    // Clean string properties on construction
    public string Name { get; init; } = TextCleaner.Clean(Name);
    public string ShortDescription { get; init; } = TextCleaner.Clean(ShortDescription);
    public string LongDescription { get; init; } = TextCleaner.Clean(LongDescription);
    public string Description { get; init; } = TextCleaner.Clean(Description);
    public string Type { get; init; } = TextCleaner.Clean(Type);
}

public sealed record RoomRange(int Min, int Max);

public sealed record ZoneResetDefinition(
    string Type,
    int? ObjectId,
    int? MobId,
    int? RoomId,
    int? MaxExisting,
    int? SpawnChance,
    int? EquipSlot,
    int? ContainerId,
    int? DoorDirection,
    int? DoorState,
    bool IfFlag);

public sealed record ZoneDefinition(
    int Id,
    string Name,
    RoomRange RoomRange,
    string ResetMode,
    IReadOnlyList<ZoneResetDefinition> ResetCommands);

public sealed class PlayerState : ICombatant
{
    private readonly List<int> _inventoryObjectIds = new();
    private readonly Dictionary<int, int> _equipmentSlotToObjectId = new(); // slot -> objectInstanceId
    private readonly Dictionary<SkillType, byte> _skills = new(); // skill -> proficiency (0-100)
    private readonly Dictionary<SkillType, DateTime> _lastSkillgainTime = new(); // skill -> last improvement time
    private readonly Dictionary<SpellType, byte> _spells = new(); // spell -> proficiency (0-100)
    private readonly Dictionary<SpellType, DateTime> _lastSpellgainTime = new(); // spell -> last improvement time
    private readonly List<Affect> _affects = new(); // active affects/buffs/debuffs

    public PlayerState(
        int id,
        string name,
        int roomId,
        byte level = 1,
        string characterClass = "Warrior",
        string race = "Human",
        byte sex = 0)
    {
        Id = id;
        Name = name;
        RoomId = roomId;
        Level = level;
        CharacterClass = characterClass;
        Race = race;
        Sex = sex;
        
        // Initialize with default starting values
        // These match legacy starting character defaults
        Strength = 16;
        StrengthAdd = 0;
        Intelligence = 16;
        Wisdom = 16;
        Dexterity = 16;
        Constitution = 16;
        Charisma = 16;
        
        MaxHitPoints = 20;
        HitPoints = 20;
        MaxMana = 100;
        Mana = 100;
        MaxMovement = 100;
        Movement = 100;
        
        ArmorClass = 100; // Legacy: -100 to 100 (higher is worse)
        Gold = 0;
        BankGold = 0;
        Experience = 0;
        
        Hitroll = 0;
        Damroll = 0;
        
        Alignment = 0; // Neutral
    }

    // ===== Identity =====
    public int Id { get; }
    public string Name { get; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public byte Sex { get; set; } // 0 = neutral, 1 = male, 2 = female

    // ===== Location =====
    public int RoomId { get; set; }

    // ===== Class & Level =====
    public string CharacterClass { get; set; }
    public string Race { get; set; }
    public byte Level { get; set; }
    public int Experience { get; set; }

    // ===== Core Abilities (Stats) =====
    public sbyte Strength { get; set; }
    public sbyte StrengthAdd { get; set; } // 0-100 if Strength == 18
    public sbyte Intelligence { get; set; }
    public sbyte Wisdom { get; set; }
    public sbyte Dexterity { get; set; }
    public sbyte Constitution { get; set; }
    public sbyte Charisma { get; set; }

    // ===== Vitals (Hit/Mana/Movement) =====
    public short HitPoints { get; set; }
    public short MaxHitPoints { get; set; }
    public short Mana { get; set; }
    public short MaxMana { get; set; }
    public short Movement { get; set; }
    public short MaxMovement { get; set; }

    // ===== Combat Stats =====
    public short ArmorClass { get; set; } // -100 to 100 (higher = worse)
    public sbyte Hitroll { get; set; }    // Bonus to hit
    public sbyte Damroll { get; set; }     // Bonus to damage
    public int Alignment { get; set; }     // -1000 (evil) to +1000 (good)

    // ===== Resources =====
    public int Gold { get; set; }
    public int BankGold { get; set; }

    // ===== Combat State =====
    /// <summary>
    /// The connection ID of the player this character is fighting, or null if not in combat.
    /// Legacy equivalent: ch->specials.fighting
    /// </summary>
    public int? FightingConnectionId { get; set; }
    
    /// <summary>
    /// Position of the character (standing, fighting, sleeping, etc.)
    /// Legacy: GET_POS(ch) - POS_DEAD=0, POS_MORTALLYW=1, POS_INCAP=2, POS_STUNNED=3,
    ///         POS_SLEEPING=4, POS_RESTING=5, POS_SITTING=6, POS_FIGHTING=7, POS_STANDING=8
    /// </summary>
    public Position Position { get; set; } = Position.Standing;
    
    /// <summary>
    /// Accumulator for position-based regeneration.
    /// Incremented each tick based on position (sleeping +4, resting +3, sitting +2, standing +1).
    /// Used in regen formulas and reset to 0 after each regen tick.
    /// Legacy: ch->specials.gain_count
    /// </summary>
    public int GainCount { get; set; } = 0;
    
    /// <summary>
    /// HP threshold below which the player will auto-flee (wimpy).
    /// Max allowed is MaxHitPoints / 4 (25%).
    /// Legacy: ch->specials2.wimp_level
    /// </summary>
    public short WimpyLevel { get; set; } = 0;
    
    /// <summary>
    /// Combat lag timer. Number of combat rounds the character must wait before acting.
    /// Decremented each combat tick (every 2 seconds).
    /// When > 0, the character cannot execute commands or skills.
    /// Legacy: ch->char_specials.wait_state
    /// </summary>
    public int WaitState { get; set; } = 0;
    
    // ===== Communication State =====
    
    /// <summary>
    /// Connection ID of the last player who sent this character a tell.
    /// Used by the 'reply' command to respond to the last tell sender.
    /// Legacy: GET_LAST_TELL(ch)
    /// </summary>
    public int? LastTellSender { get; set; }

    // ===== Inventory & Equipment =====
    public IReadOnlyList<int> InventoryObjectIds => _inventoryObjectIds;
    public IReadOnlyDictionary<int, int> EquipmentSlotToObjectId => _equipmentSlotToObjectId;

    public void AddToInventory(int objectInstanceId)
    {
        _inventoryObjectIds.Add(objectInstanceId);
    }

    public bool RemoveFromInventory(int objectInstanceId)
    {
        return _inventoryObjectIds.Remove(objectInstanceId);
    }

    public bool EquipToSlot(int slot, int objectInstanceId)
    {
        if (_equipmentSlotToObjectId.ContainsKey(slot))
        {
            return false; // Slot occupied
        }
        _equipmentSlotToObjectId[slot] = objectInstanceId;
        return true;
    }

    public bool UnequipFromSlot(int slot, out int objectInstanceId)
    {
        if (_equipmentSlotToObjectId.Remove(slot, out objectInstanceId))
        {
            return true;
        }
        objectInstanceId = 0;
        return false;
    }

    // ===== Skills & Spells =====
    
    /// <summary>
    /// Get skill proficiency (0-100 percentage).
    /// Legacy: GET_SKILL(ch, skill) macro
    /// </summary>
    public byte GetSkill(SkillType skillType)
    {
        if (_skills.TryGetValue(skillType, out var proficiency))
        {
            return proficiency;
        }
        return 0;
    }

    /// <summary>
    /// Set skill proficiency (0-100 percentage).
    /// Legacy: SET_SKILL(ch, skill, percent) macro
    /// </summary>
    public void SetSkill(SkillType skillType, byte proficiency)
    {
        _skills[skillType] = Math.Min((byte)100, proficiency);
    }

    /// <summary>
    /// Checks if the character has learned a specific skill (proficiency > 0).
    /// Part of ICombatant interface.
    /// </summary>
    public bool HasSkill(SkillType skillType)
    {
        return GetSkill(skillType) > 0;
    }

    /// <summary>
    /// Get all learned skills as a read-only dictionary.
    /// Used for persistence and skill display.
    /// </summary>
    public IReadOnlyDictionary<SkillType, byte> GetAllSkills()
    {
        return _skills;
    }

    /// <summary>
    /// Improve skill by 1% if improvement check passes.
    /// Legacy: improve_skill(ch, skill) - act.other.c:52-74
    /// 
    /// Improvement conditions:
    /// 1. Skill must not be maxed (100%)
    /// 2. Skillgain cooldown must have passed (60 seconds since last improvement)
    /// 3. Random roll must succeed (harder at higher proficiency)
    /// </summary>
    public bool TryImproveSkill(SkillType skillType)
    {
        var currentPercent = GetSkill(skillType);
        if (currentPercent >= 100) return false; // Already maxed
        
        // Check skillgain cooldown
        if (_lastSkillgainTime.TryGetValue(skillType, out var lastImprovement))
        {
            var timeSinceLastGain = DateTime.UtcNow - lastImprovement;
            if (timeSinceLastGain.TotalSeconds < CombatConstants.SkillgainCooldownSeconds)
            {
                return false; // Still on cooldown
            }
        }
        
        // Legacy: if (number(0, 99) > percent) { percent++; SET_SKILL(ch, skill, percent); }
        if (Random.Shared.Next(0, 100) > currentPercent)
        {
            SetSkill(skillType, (byte)(currentPercent + 1));
            _lastSkillgainTime[skillType] = DateTime.UtcNow;
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Get all skillgain timestamps as a read-only dictionary.
    /// Used for persistence and skill cooldown tracking.
    /// </summary>
    public IReadOnlyDictionary<SkillType, DateTime> GetAllSkillgainTimes()
    {
        return _lastSkillgainTime;
    }
    
    /// <summary>
    /// Set skillgain timestamp for a specific skill.
    /// Used when loading character data from database.
    /// </summary>
    public void SetSkillgainTime(SkillType skillType, DateTime timestamp)
    {
        _lastSkillgainTime[skillType] = timestamp;
    }
    
    // ===== Spell Proficiency =====
    
    /// <summary>
    /// Get spell proficiency (0-100 percentage).
    /// </summary>
    public byte GetSpell(SpellType spellType)
    {
        if (_spells.TryGetValue(spellType, out var proficiency))
        {
            return proficiency;
        }
        return 0;
    }

    /// <summary>
    /// Set spell proficiency (0-100 percentage).
    /// </summary>
    public void SetSpell(SpellType spellType, byte proficiency)
    {
        _spells[spellType] = Math.Min((byte)100, proficiency);
    }

    /// <summary>
    /// Checks if the character has learned a specific spell (proficiency > 0).
    /// </summary>
    public bool HasSpell(SpellType spellType)
    {
        return GetSpell(spellType) > 0;
    }

    /// <summary>
    /// Get all learned spells as a read-only dictionary.
    /// Used for persistence and spell display.
    /// </summary>
    public IReadOnlyDictionary<SpellType, byte> GetAllSpells()
    {
        return _spells;
    }

    /// <summary>
    /// Improve spell by 1% if improvement check passes.
    /// Same logic as skill improvement - harder at higher proficiency.
    /// </summary>
    public bool TryImproveSpell(SpellType spellType)
    {
        var currentPercent = GetSpell(spellType);
        if (currentPercent >= 100) return false; // Already maxed
        
        // Check spellgain cooldown
        if (_lastSpellgainTime.TryGetValue(spellType, out var lastImprovement))
        {
            var timeSinceLastGain = DateTime.UtcNow - lastImprovement;
            if (timeSinceLastGain.TotalSeconds < CombatConstants.SkillgainCooldownSeconds)
            {
                return false; // Still on cooldown
            }
        }
        
        // Same improvement logic as skills
        if (Random.Shared.Next(0, 100) > currentPercent)
        {
            SetSpell(spellType, (byte)(currentPercent + 1));
            _lastSpellgainTime[spellType] = DateTime.UtcNow;
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Get all spellgain timestamps as a read-only dictionary.
    /// Used for persistence and spell cooldown tracking.
    /// </summary>
    public IReadOnlyDictionary<SpellType, DateTime> GetAllSpellgainTimes()
    {
        return _lastSpellgainTime;
    }
    
    /// <summary>
    /// Set spellgain timestamp for a specific spell.
    /// Used when loading character data from database.
    /// </summary>
    public void SetSpellgainTime(SpellType spellType, DateTime timestamp)
    {
        _lastSpellgainTime[spellType] = timestamp;
    }
    
    // ===== Affects (Buffs/Debuffs) =====
    
    /// <summary>
    /// Get all active affects on this character.
    /// </summary>
    public IReadOnlyList<Affect> Affects => _affects;
    
    /// <summary>
    /// Add an affect to the character.
    /// If an affect of the same type already exists, it will be replaced (refreshed).
    /// </summary>
    public void AddAffect(Affect affect)
    {
        // Remove existing affect of same type (no stacking)
        _affects.RemoveAll(a => a.Type == affect.Type);
        _affects.Add(affect);
    }
    
    /// <summary>
    /// Remove an affect by type.
    /// Returns true if an affect was removed, false if none existed.
    /// </summary>
    public bool RemoveAffect(AffectType type)
    {
        return _affects.RemoveAll(a => a.Type == type) > 0;
    }
    
    /// <summary>
    /// Decrement all affect durations and remove expired ones.
    /// Should be called every PULSE_REGEN (75 seconds).
    /// Returns list of affects that expired.
    /// </summary>
    public List<Affect> TickAffects()
    {
        var expired = new List<Affect>();
        
        foreach (var affect in _affects.ToList()) // ToList to avoid modification during iteration
        {
            affect.DurationHours--;
            
            if (affect.DurationHours <= 0)
            {
                expired.Add(affect);
                _affects.Remove(affect);
            }
        }
        
        return expired;
    }
    
    /// <summary>
    /// Get effective armor class including all affect modifiers.
    /// Lower is better (negative AC is good).
    /// </summary>
    public short GetEffectiveArmorClass()
    {
        short effectiveAC = ArmorClass;
        foreach (var affect in _affects.Where(a => a.Location == AffectLocation.ArmorClass))
        {
            effectiveAC += (short)affect.Modifier;
        }
        return effectiveAC;
    }
    
    /// <summary>
    /// Get effective hitroll including all affect modifiers.
    /// Higher is better (bonus to hit).
    /// </summary>
    public sbyte GetEffectiveHitroll()
    {
        int effectiveHitroll = Hitroll;
        foreach (var affect in _affects.Where(a => a.Location == AffectLocation.Hitroll))
        {
            effectiveHitroll += affect.Modifier;
        }
        return (sbyte)Math.Clamp(effectiveHitroll, sbyte.MinValue, sbyte.MaxValue);
    }
    
    /// <summary>
    /// Get effective damroll including all affect modifiers.
    /// Higher is better (bonus to damage).
    /// </summary>
    public sbyte GetEffectiveDamroll()
    {
        int effectiveDamroll = Damroll;
        foreach (var affect in _affects.Where(a => a.Location == AffectLocation.Damroll))
        {
            effectiveDamroll += affect.Modifier;
        }
        return (sbyte)Math.Clamp(effectiveDamroll, sbyte.MinValue, sbyte.MaxValue);
    }
    
    /// <summary>
    /// Get effective strength including all affect modifiers.
    /// </summary>
    public sbyte GetEffectiveStrength()
    {
        int effectiveStr = Strength;
        foreach (var affect in _affects.Where(a => a.Location == AffectLocation.Strength))
        {
            effectiveStr += affect.Modifier;
        }
        return (sbyte)Math.Clamp(effectiveStr, sbyte.MinValue, sbyte.MaxValue);
    }
    
    // ===== Combat Lag (WAIT_STATE) =====
    
    /// <summary>
    /// Decrement WaitState by one tick if it's greater than zero.
    /// Called every combat tick (2 seconds) by GameTickService.
    /// Legacy: Implicit in the wait_state decrement in game loop
    /// </summary>
    public void DecrementWaitState()
    {
        if (WaitState > 0)
        {
            WaitState--;
        }
    }
    
    /// <summary>
    /// Check if the character can act (not waiting).
    /// </summary>
    public bool CanAct() => WaitState <= 0;
}

public sealed class WorldDefinition
{
    private readonly IReadOnlyDictionary<int, RoomDefinition> _rooms;

    public WorldDefinition(IReadOnlyDictionary<int, RoomDefinition> rooms)
    {
        _rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
    }

    public IReadOnlyDictionary<int, RoomDefinition> Rooms => _rooms;

    public RoomDefinition GetRoom(int id)
    {
        if (!_rooms.TryGetValue(id, out var room))
        {
            throw new KeyNotFoundException($"Room {id} not found.");
        }

        return room;
    }

    public bool TryMove(int currentRoomId, Direction direction, out int targetRoomId)
    {
        var room = GetRoom(currentRoomId);
        foreach (var exit in room.Exits)
        {
            if (exit.Direction == direction)
            {
                targetRoomId = exit.TargetRoomId;
                return true;
            }
        }

        targetRoomId = currentRoomId;
        return false;
    }
}

/// <summary>
/// Extension methods for alignment-based combat checks.
/// Legacy thresholds from utils.h:195-197
/// </summary>
public static class AlignmentExtensions
{
    /// <summary>
    /// Check if a combatant is good-aligned.
    /// Legacy: IS_GOOD(ch) = GET_ALIGNMENT(ch) >= 350
    /// </summary>
    public static bool IsGood(this ICombatant combatant) => combatant.Alignment >= 350;
    
    /// <summary>
    /// Check if a combatant is evil-aligned.
    /// Legacy: IS_EVIL(ch) = GET_ALIGNMENT(ch) <= -350
    /// </summary>
    public static bool IsEvil(this ICombatant combatant) => combatant.Alignment <= -350;
    
    /// <summary>
    /// Check if a combatant is neutral-aligned.
    /// Legacy: IS_NEUTRAL(ch) = !IS_GOOD(ch) && !IS_EVIL(ch)
    /// </summary>
    public static bool IsNeutral(this ICombatant combatant) => !combatant.IsGood() && !combatant.IsEvil();
}
