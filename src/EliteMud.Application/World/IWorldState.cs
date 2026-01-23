using EliteMud.Game;

namespace EliteMud.Application.World;

public enum EquipmentSlot
{
    Light = 0,
    FingerRight = 1,
    FingerLeft = 2,
    Neck1 = 3,
    Neck2 = 4,
    Body = 5,
    Head = 6,
    Legs = 7,
    Feet = 8,
    Hands = 9,
    Arms = 10,
    Shield = 11,
    About = 12,
    Waist = 13,
    WristRight = 14,
    WristLeft = 15,
    Wield = 16,
    Hold = 17,
    Wield2 = 18,
    BothHands = 19
}

public sealed class MobInstance : ICombatant
{
    private readonly Dictionary<EquipmentSlot, ObjectInstance> _equipment = new();
    private readonly List<Affect> _affects = new(); // Mobs can have affects too!

    public MobInstance(int instanceId, MobDefinition definition)
    {
        InstanceId = instanceId;
        Definition = definition;
        // Initialize HP from mob definition's MaxHitPoints
        HitPoints = (short)Math.Min(definition.MaxHitPoints, short.MaxValue);
    }

    public int InstanceId { get; }
    
    public MobDefinition Definition { get; }
    
    // Combat state
    public int? FightingConnectionId { get; set; } // The player connection ID this mob is fighting
    public int? FightingMobInstanceId { get; set; } // The mob instance ID this mob is fighting (for mob-vs-mob combat)
    public Position Position { get; set; } = Position.Standing;
    
    // ICombatant implementation
    public string Name => Definition.ShortDescription;
    public short HitPoints { get; set; } // Current HP
    public short MaxHitPoints => (short)Math.Min(Definition.MaxHitPoints, short.MaxValue);
    public short ArmorClass => (short)Math.Clamp(Definition.ArmorClass, short.MinValue, short.MaxValue);
    public byte Level => (byte)Math.Min(Definition.Level, byte.MaxValue);
    
    /// <summary>
    /// Get skill proficiency for this mob.
    /// TODO: Parse Definition.Skills list and return actual proficiency.
    /// For now, mobs don't have skills - returns 0.
    /// </summary>
    public byte GetSkill(SkillType skillType)
    {
        // TODO: Parse Definition.Skills string list to SkillType and proficiency
        return 0;
    }
    
    /// <summary>
    /// Check if mob has a skill.
    /// TODO: Parse Definition.Skills list.
    /// For now, mobs don't have skills - returns false.
    /// </summary>
    public bool HasSkill(SkillType skillType)
    {
        return false;
    }
    
    // ===== Affects (Buffs/Debuffs) =====
    
    /// <summary>
    /// Get all active affects on this mob.
    /// </summary>
    public IReadOnlyList<Affect> Affects => _affects;
    
    /// <summary>
    /// Add an affect to the mob.
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
        // Mobs don't have base Hitroll in current implementation, so we use Definition.ArmorClass as placeholder
        // TODO: Add Hitroll to MobDefinition
        int effectiveHitroll = 0; // Default base hitroll for mobs
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
        // Mobs don't have base Damroll in current implementation
        // TODO: Add Damroll to MobDefinition
        int effectiveDamroll = 0; // Default base damroll for mobs
        foreach (var affect in _affects.Where(a => a.Location == AffectLocation.Damroll))
        {
            effectiveDamroll += affect.Modifier;
        }
        return (sbyte)Math.Clamp(effectiveDamroll, sbyte.MinValue, sbyte.MaxValue);
    }

    public IReadOnlyDictionary<EquipmentSlot, ObjectInstance> Equipment => _equipment;

    public bool Equip(ObjectInstance obj, EquipmentSlot slot)
    {
        if (_equipment.ContainsKey(slot))
        {
            return false; // Slot already occupied
        }

        _equipment[slot] = obj;
        return true;
    }

    public ObjectInstance? Unequip(EquipmentSlot slot)
    {
        if (_equipment.Remove(slot, out var obj))
        {
            return obj;
        }

        return null;
    }
}

/// <summary>
/// Runtime instance of an object in the world.
/// Legacy: struct obj_data in structs.h
/// </summary>
public sealed class ObjectInstance
{
    private readonly List<ObjectInstance> _contents = new();

    public ObjectInstance(int instanceId, ObjectDefinition definition)
    {
        InstanceId = instanceId;
        Definition = definition;
    }

    public int InstanceId { get; }
    public ObjectDefinition Definition { get; }

    /// <summary>
    /// Items contained within this object (for containers like corpses, bags, etc.)
    /// Legacy: obj_data->contains
    /// </summary>
    public IReadOnlyList<ObjectInstance> Contents => _contents;

    /// <summary>
    /// Add an item to this container.
    /// Legacy: obj_to_obj()
    /// </summary>
    public void AddItem(ObjectInstance item)
    {
        _contents.Add(item);
    }

    /// <summary>
    /// Remove an item from this container.
    /// </summary>
    public bool RemoveItem(ObjectInstance item)
    {
        return _contents.Remove(item);
    }

    /// <summary>
    /// Remove an item from this container by instance ID.
    /// </summary>
    public bool RemoveItemById(int instanceId)
    {
        var item = _contents.FirstOrDefault(i => i.InstanceId == instanceId);
        if (item is null)
        {
            return false;
        }
        return _contents.Remove(item);
    }
}

public interface IWorldState
{
    WorldDefinition World { get; }

    IReadOnlyList<MobInstance> GetMobsInRoom(int roomId);

    IReadOnlyList<ObjectInstance> GetObjectsInRoom(int roomId);

    IReadOnlyList<ObjectInstance> GetPlayerInventory(PlayerState player);

    IReadOnlyDictionary<EquipmentSlot, ObjectInstance> GetPlayerEquipment(PlayerState player);

    ObjectInstance? GetObjectInstance(int instanceId);

    bool TakeObject(PlayerState player, int objectInstanceId);

    bool DropObject(PlayerState player, int objectInstanceId);

    bool EquipObject(PlayerState player, int objectInstanceId, EquipmentSlot slot);

    bool UnequipObject(PlayerState player, EquipmentSlot slot);

    ObjectInstance? LoadObjectToPlayer(PlayerState player, int objectDefinitionId);

    /// <summary>
    /// Creates an object instance from a definition ID without adding it to any location.
    /// Used for loading player inventory and equipment from database.
    /// </summary>
    ObjectInstance? CreateObjectInstance(int objectDefinitionId);

    IReadOnlyList<ObjectDefinition> SearchObjects(string query);

    bool ResetZone(int zoneId);

    bool ResetZoneForRoom(int roomId, out int zoneId);

    /// <summary>
    /// Create a corpse from a dead player and place it in the specified room.
    /// Transfers all inventory and equipment to the corpse.
    /// Legacy: make_corpse() in fight.c:310-393
    /// </summary>
    ObjectInstance CreatePlayerCorpse(PlayerState player, int roomId);

    /// <summary>
    /// Create a corpse from a dead mob and place it in the specified room.
    /// Transfers all equipment to the corpse.
    /// Legacy: make_corpse() in fight.c:310-393
    /// </summary>
    ObjectInstance CreateMobCorpse(MobInstance mob, int roomId);

    /// <summary>
    /// Remove a mob from the world completely.
    /// Legacy: extract_char() in handler.c
    /// </summary>
    bool RemoveMob(int mobInstanceId, int roomId);
}

/// <summary>
/// Extension methods for calculating effective stats including equipment bonuses.
/// </summary>
public static class WorldStateExtensions
{
    /// <summary>
    /// Get total equipment bonus for a specific affect location.
    /// Sums all modifiers from equipped items affecting that location.
    /// </summary>
    public static int GetEquipmentBonus(this IWorldState worldState, PlayerState player, AffectLocation location)
    {
        var equipment = worldState.GetPlayerEquipment(player);
        int total = 0;
        
        foreach (var (slot, obj) in equipment)
        {
            foreach (var affect in obj.Definition.Affects)
            {
                if (affect.Location == location)
                {
                    total += affect.Modifier;
                }
            }
        }
        
        return total;
    }

    /// <summary>
    /// Get effective armor class including spell affects AND equipment bonuses.
    /// Lower is better (negative AC is good).
    /// Use this instead of player.GetEffectiveArmorClass() to include equipment.
    /// </summary>
    public static short GetTotalEffectiveArmorClass(this IWorldState worldState, PlayerState player)
    {
        // Start with base AC
        short effectiveAC = player.ArmorClass;
        
        // Add spell affect modifiers
        foreach (var affect in player.Affects.Where(a => a.Location == AffectLocation.ArmorClass))
        {
            effectiveAC += (short)affect.Modifier;
        }
        
        // Add equipment bonuses
        effectiveAC += (short)worldState.GetEquipmentBonus(player, AffectLocation.ArmorClass);
        
        return effectiveAC;
    }
    
    /// <summary>
    /// Get effective hitroll including spell affects AND equipment bonuses.
    /// Higher is better (bonus to hit).
    /// Use this instead of player.GetEffectiveHitroll() to include equipment.
    /// </summary>
    public static sbyte GetTotalEffectiveHitroll(this IWorldState worldState, PlayerState player)
    {
        int effectiveHitroll = player.Hitroll;
        
        // Add spell affect modifiers
        foreach (var affect in player.Affects.Where(a => a.Location == AffectLocation.Hitroll))
        {
            effectiveHitroll += affect.Modifier;
        }
        
        // Add equipment bonuses
        effectiveHitroll += worldState.GetEquipmentBonus(player, AffectLocation.Hitroll);
        
        return (sbyte)Math.Clamp(effectiveHitroll, sbyte.MinValue, sbyte.MaxValue);
    }
    
    /// <summary>
    /// Get effective damroll including spell affects AND equipment bonuses.
    /// Higher is better (bonus to damage).
    /// Use this instead of player.GetEffectiveDamroll() to include equipment.
    /// </summary>
    public static sbyte GetTotalEffectiveDamroll(this IWorldState worldState, PlayerState player)
    {
        int effectiveDamroll = player.Damroll;
        
        // Add spell affect modifiers
        foreach (var affect in player.Affects.Where(a => a.Location == AffectLocation.Damroll))
        {
            effectiveDamroll += affect.Modifier;
        }
        
        // Add equipment bonuses
        effectiveDamroll += worldState.GetEquipmentBonus(player, AffectLocation.Damroll);
        
        return (sbyte)Math.Clamp(effectiveDamroll, sbyte.MinValue, sbyte.MaxValue);
    }
}
