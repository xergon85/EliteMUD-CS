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
    private readonly List<long> _memory = new(); // Player IDs this mob remembers (for MOB_MEMORY)
    private readonly List<int> _inventoryObjectIds = new(); // Object instance IDs in mob inventory

    public MobInstance(int instanceId, MobDefinition definition)
    {
        InstanceId = instanceId;
        Definition = definition;
        // Initialize HP from mob definition's MaxHitPoints
        HitPoints = (short)Math.Min(definition.MaxHitPoints, short.MaxValue);
        
        // Initialize hometown to current spawn location (will be set by zone reset)
        Hometown = null;
    }

    public int InstanceId { get; }
    
    public MobDefinition Definition { get; }
    
    // ===== Combat state =====
    public int? FightingConnectionId { get; set; } // The player connection ID this mob is fighting
    public int? FightingMobInstanceId { get; set; } // The mob instance ID this mob is fighting (for mob-vs-mob combat)
    public Position Position { get; set; } = Position.Standing;
    
    // ===== Mob AI state (legacy mobact.c) =====
    
    /// <summary>
    /// Last direction this mob moved (for anti-bounce logic).
    /// -1 means no recent movement.
    /// Legacy: ch->mob_specials.last_direction
    /// </summary>
    public int LastDirection { get; set; } = -1;
    
    /// <summary>
    /// Player IDs this mob remembers (for MOB_MEMORY flag).
    /// Populated when players attack this mob.
    /// Legacy: ch->mob_specials.memory (linked list of memory_rec)
    /// </summary>
    public IReadOnlyList<long> Memory => _memory;
    
    /// <summary>
    /// Add a player to this mob's memory.
    /// Used by MOB_MEMORY flag to track attackers.
    /// </summary>
    public void RememberPlayer(long playerId)
    {
        if (!_memory.Contains(playerId))
        {
            _memory.Add(playerId);
        }
    }
    
    /// <summary>
    /// Remove a player from this mob's memory.
    /// </summary>
    public void ForgetPlayer(long playerId)
    {
        _memory.Remove(playerId);
    }
    
    /// <summary>
    /// Clear all memory of attackers.
    /// </summary>
    public void ClearMemory()
    {
        _memory.Clear();
    }
    
    /// <summary>
    /// Hometown room ID for sentinel mobs to return to.
    /// Set by zone reset when mob is spawned.
    /// Legacy: ch->player.hometown
    /// </summary>
    public int? Hometown { get; set; }
    
    /// <summary>
    /// Pathfinding queue for tracking/returning home.
    /// Mob follows this path each tick until reaching destination.
    /// Legacy: ch->trackdir (stack-based in C)
    /// </summary>
    public Queue<int>? TrackingPath { get; set; }
    
    // ICombatant implementation
    public string Name => Definition.ShortDescription;
    public short HitPoints { get; set; } // Current HP
    public short MaxHitPoints => (short)Math.Min(Definition.MaxHitPoints, short.MaxValue);
    public short ArmorClass => (short)Math.Clamp(Definition.ArmorClass, short.MinValue, short.MaxValue);
    public byte Level => (byte)Math.Min(Definition.Level, byte.MaxValue);
    public int Alignment => Definition.Alignment;
    
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
    
    /// <summary>
    /// Object instance IDs carried by this mob (for scavenger and GiveMob zone reset).
    /// Legacy: ch->carrying (linked list)
    /// </summary>
    public IReadOnlyList<int> InventoryObjectIds => _inventoryObjectIds;
    
    /// <summary>
    /// Add an object to this mob's inventory.
    /// </summary>
    public void AddToInventory(int objectInstanceId)
    {
        _inventoryObjectIds.Add(objectInstanceId);
    }
    
    /// <summary>
    /// Remove an object from this mob's inventory.
    /// Returns true if object was found and removed.
    /// </summary>
    public bool RemoveFromInventory(int objectInstanceId)
    {
        return _inventoryObjectIds.Remove(objectInstanceId);
    }

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
        
        // Initialize container state from definition flags
        if (definition.Details?.Container != null)
        {
            var container = definition.Details.Container;
            IsClosed = container.Flags.Contains("Closed", StringComparer.OrdinalIgnoreCase);
            IsLocked = container.Flags.Contains("Locked", StringComparer.OrdinalIgnoreCase);
        }
    }

    public int InstanceId { get; }
    public ObjectDefinition Definition { get; }
    
    /// <summary>
    /// Runtime state: Is this container currently closed?
    /// Only applies to containers with "Closeable" flag.
    /// Legacy: GET_OBJ_VAL(obj, 1) & CONT_CLOSED
    /// </summary>
    public bool IsClosed { get; set; }
    
    /// <summary>
    /// Runtime state: Is this container currently locked?
    /// Only applies to containers with "Closeable" flag.
    /// Requires a key (KeyId in container details) to unlock.
    /// Legacy: GET_OBJ_VAL(obj, 1) & CONT_LOCKED
    /// </summary>
    public bool IsLocked { get; set; }

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

    /// <summary>
    /// Get all items accessible to the player (including items inside containers).
    /// This recursively includes container contents for targeting purposes.
    /// Use this when searching for items/containers that might be nested.
    /// </summary>
    IReadOnlyList<ObjectInstance> GetAllPlayerItems(PlayerState player);

    IReadOnlyDictionary<EquipmentSlot, ObjectInstance> GetPlayerEquipment(PlayerState player);

    ObjectInstance? GetObjectInstance(int instanceId);

    bool TakeObject(PlayerState player, int objectInstanceId);

    bool DropObject(PlayerState player, int objectInstanceId);

    /// <summary>
    /// Transfer object from room to mob inventory.
    /// Used by scavenger AI.
    /// </summary>
    bool TakeObjectForMob(MobInstance mob, int objectInstanceId, int roomId);

    /// <summary>
    /// Drop object from mob inventory to room.
    /// Used when mob dies (loot drop).
    /// </summary>
    bool DropObjectForMob(MobInstance mob, int objectInstanceId, int roomId);

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

    /// <summary>
    /// Move a mob from one room to another.
    /// Returns true if successful, false if mob not found or invalid rooms.
    /// Legacy: char_from_room() + char_to_room() in handler.c
    /// </summary>
    bool MoveMob(int mobInstanceId, int fromRoomId, int toRoomId);

    /// <summary>
    /// Get a mob instance by its instance ID and room ID.
    /// Returns null if not found.
    /// </summary>
    MobInstance? GetMobInstance(int mobInstanceId, int roomId);
    
    /// <summary>
    /// Get the runtime state of a door.
    /// Returns null if the exit doesn't exist or isn't a door.
    /// </summary>
    DoorState? GetDoorState(int roomId, Direction direction);
    
    /// <summary>
    /// Set door state (open/close, lock/unlock) for both sides of the door.
    /// Doors are bidirectional - changing one side changes both.
    /// </summary>
    void SetDoorState(int roomId, Direction direction, bool isClosed, bool isLocked);
    
    /// <summary>
    /// Break a door (bash command), removing closed/locked states and setting broken flag.
    /// Legacy: Removes EX_CLOSED and EX_LOCKED, sets EX_BROKEN.
    /// Doors are bidirectional - breaking one side breaks both.
    /// </summary>
    void BreakDoor(int roomId, Direction direction);
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
    /// Includes both Armor (flat) and ArmorClass (with slot multiplier) locations.
    /// Use this instead of player.GetEffectiveArmorClass() to include equipment.
    /// </summary>
    public static short GetTotalEffectiveArmorClass(this IWorldState worldState, PlayerState player)
    {
        // Start with base AC
        short effectiveAC = player.ArmorClass;
        
        // Add spell affect modifiers (both Armor and ArmorClass)
        foreach (var affect in player.Affects.Where(a => a.Location == AffectLocation.Armor || a.Location == AffectLocation.ArmorClass))
        {
            effectiveAC += (short)affect.Modifier;
        }
        
        // Add equipment bonuses (both Armor and ArmorClass)
        effectiveAC += (short)worldState.GetEquipmentBonus(player, AffectLocation.Armor);
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
    
    /// <summary>
    /// Get effective max HP including spell affects AND equipment bonuses.
    /// Use this to display max HP with equipment bonuses.
    /// </summary>
    public static short GetTotalEffectiveMaxHitPoints(this IWorldState worldState, PlayerState player)
    {
        int effectiveMaxHP = player.MaxHitPoints;
        
        // Add spell affect modifiers
        foreach (var affect in player.Affects.Where(a => a.Location == AffectLocation.MaxHit))
        {
            effectiveMaxHP += affect.Modifier;
        }
        
        // Add equipment bonuses
        effectiveMaxHP += worldState.GetEquipmentBonus(player, AffectLocation.MaxHit);
        
        return (short)Math.Max(effectiveMaxHP, 1); // Min 1 HP
    }
    
    /// <summary>
    /// Get effective max Mana including spell affects AND equipment bonuses.
    /// Use this to display max Mana with equipment bonuses.
    /// </summary>
    public static short GetTotalEffectiveMaxMana(this IWorldState worldState, PlayerState player)
    {
        int effectiveMaxMana = player.MaxMana;
        
        // Add spell affect modifiers
        foreach (var affect in player.Affects.Where(a => a.Location == AffectLocation.MaxMana))
        {
            effectiveMaxMana += affect.Modifier;
        }
        
        // Add equipment bonuses
        effectiveMaxMana += worldState.GetEquipmentBonus(player, AffectLocation.MaxMana);
        
        return (short)Math.Max(effectiveMaxMana, 0);
    }
    
    /// <summary>
    /// Get effective max Movement including spell affects AND equipment bonuses.
    /// Use this to display max Movement with equipment bonuses.
    /// </summary>
    public static short GetTotalEffectiveMaxMovement(this IWorldState worldState, PlayerState player)
    {
        int effectiveMaxMove = player.MaxMovement;
        
        // Add spell affect modifiers
        foreach (var affect in player.Affects.Where(a => a.Location == AffectLocation.MaxMovement))
        {
            effectiveMaxMove += affect.Modifier;
        }
        
        // Add equipment bonuses
        effectiveMaxMove += worldState.GetEquipmentBonus(player, AffectLocation.MaxMovement);
        
        return (short)Math.Max(effectiveMaxMove, 0);
    }
    
    // ===== Effective Attribute Stats (for combat calculations) =====
    
    /// <summary>
    /// Get effective Strength including spell affects AND equipment bonuses.
    /// Used in combat calculations (damage, to-hit).
    /// Legacy: EliteMUD applies str_app tables to total stats (base + equipment + spell).
    /// </summary>
    public static sbyte GetTotalEffectiveStrength(this IWorldState worldState, PlayerState player)
    {
        int effectiveStr = player.Strength;
        
        // Add spell affect modifiers
        foreach (var affect in player.Affects.Where(a => a.Location == AffectLocation.Strength))
        {
            effectiveStr += affect.Modifier;
        }
        
        // Add equipment bonuses
        effectiveStr += worldState.GetEquipmentBonus(player, AffectLocation.Strength);
        
        // Clamp to valid attribute range (3-25, matching legacy)
        return (sbyte)Math.Clamp(effectiveStr, 3, 25);
    }
    
    /// <summary>
    /// Get effective Dexterity including spell affects AND equipment bonuses.
    /// Used in combat calculations (AC, dodge).
    /// Legacy: EliteMUD applies dex_app tables to total stats (base + equipment + spell).
    /// </summary>
    public static sbyte GetTotalEffectiveDexterity(this IWorldState worldState, PlayerState player)
    {
        int effectiveDex = player.Dexterity;
        
        // Add spell affect modifiers
        foreach (var affect in player.Affects.Where(a => a.Location == AffectLocation.Dexterity))
        {
            effectiveDex += affect.Modifier;
        }
        
        // Add equipment bonuses
        effectiveDex += worldState.GetEquipmentBonus(player, AffectLocation.Dexterity);
        
        // Clamp to valid attribute range (3-25, matching legacy)
        return (sbyte)Math.Clamp(effectiveDex, 3, 25);
    }
    
    /// <summary>
    /// Get effective Intelligence including spell affects AND equipment bonuses.
    /// Used in combat calculations (to-hit bonus for smart characters).
    /// Legacy: EliteMUD applies int_app tables to total stats (base + equipment + spell).
    /// </summary>
    public static sbyte GetTotalEffectiveIntelligence(this IWorldState worldState, PlayerState player)
    {
        int effectiveInt = player.Intelligence;
        
        // Add spell affect modifiers
        foreach (var affect in player.Affects.Where(a => a.Location == AffectLocation.Intelligence))
        {
            effectiveInt += affect.Modifier;
        }
        
        // Add equipment bonuses
        effectiveInt += worldState.GetEquipmentBonus(player, AffectLocation.Intelligence);
        
        // Clamp to valid attribute range (3-25, matching legacy)
        return (sbyte)Math.Clamp(effectiveInt, 3, 25);
    }
    
    /// <summary>
    /// Get effective Wisdom including spell affects AND equipment bonuses.
    /// Used in combat calculations (to-hit bonus for wise characters).
    /// Legacy: EliteMUD applies wis_app tables to total stats (base + equipment + spell).
    /// </summary>
    public static sbyte GetTotalEffectiveWisdom(this IWorldState worldState, PlayerState player)
    {
        int effectiveWis = player.Wisdom;
        
        // Add spell affect modifiers
        foreach (var affect in player.Affects.Where(a => a.Location == AffectLocation.Wisdom))
        {
            effectiveWis += affect.Modifier;
        }
        
        // Add equipment bonuses
        effectiveWis += worldState.GetEquipmentBonus(player, AffectLocation.Wisdom);
        
        // Clamp to valid attribute range (3-25, matching legacy)
        return (sbyte)Math.Clamp(effectiveWis, 3, 25);
    }
    
    /// <summary>
    /// Get effective Constitution including spell affects AND equipment bonuses.
    /// Currently used for display only (future: HP regen, poison resistance).
    /// Legacy: EliteMUD applies con_app tables to total stats (base + equipment + spell).
    /// </summary>
    public static sbyte GetTotalEffectiveConstitution(this IWorldState worldState, PlayerState player)
    {
        int effectiveCon = player.Constitution;
        
        // Add spell affect modifiers
        foreach (var affect in player.Affects.Where(a => a.Location == AffectLocation.Constitution))
        {
            effectiveCon += affect.Modifier;
        }
        
        // Add equipment bonuses
        effectiveCon += worldState.GetEquipmentBonus(player, AffectLocation.Constitution);
        
        // Clamp to valid attribute range (3-25, matching legacy)
        return (sbyte)Math.Clamp(effectiveCon, 3, 25);
    }
    
    /// <summary>
    /// Get effective Charisma including spell affects AND equipment bonuses.
    /// Currently used for display only (future: shop prices, NPC reactions).
    /// Legacy: EliteMUD applies cha_app tables to total stats (base + equipment + spell).
    /// </summary>
    public static sbyte GetTotalEffectiveCharisma(this IWorldState worldState, PlayerState player)
    {
        int effectiveCha = player.Charisma;
        
        // Add spell affect modifiers
        foreach (var affect in player.Affects.Where(a => a.Location == AffectLocation.Charisma))
        {
            effectiveCha += affect.Modifier;
        }
        
        // Add equipment bonuses
        effectiveCha += worldState.GetEquipmentBonus(player, AffectLocation.Charisma);
        
        // Clamp to valid attribute range (3-25, matching legacy)
        return (sbyte)Math.Clamp(effectiveCha, 3, 25);
    }
    
    // ===== Mob Equipment Bonuses =====
    
    /// <summary>
    /// Get total equipment bonus for a mob's specific affect location.
    /// Sums all modifiers from equipped items affecting that location.
    /// </summary>
    public static int GetMobEquipmentBonus(MobInstance mob, AffectLocation location)
    {
        int total = 0;
        
        foreach (var (slot, obj) in mob.Equipment)
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
    /// Get effective armor class for a mob including equipment bonuses.
    /// Lower is better (negative AC is good).
    /// Includes both Armor (flat) and ArmorClass (with slot multiplier) locations.
    /// </summary>
    public static short GetMobEffectiveArmorClass(MobInstance mob)
    {
        // Start with base AC from mob definition
        short effectiveAC = (short)mob.Definition.ArmorClass;
        
        // Add spell affect modifiers (both Armor and ArmorClass)
        foreach (var affect in mob.Affects.Where(a => a.Location == AffectLocation.Armor || a.Location == AffectLocation.ArmorClass))
        {
            effectiveAC += (short)affect.Modifier;
        }
        
        // Add equipment bonuses (both Armor and ArmorClass)
        effectiveAC += (short)GetMobEquipmentBonus(mob, AffectLocation.Armor);
        effectiveAC += (short)GetMobEquipmentBonus(mob, AffectLocation.ArmorClass);
        
        return effectiveAC;
    }
    
    /// <summary>
    /// Get effective hitroll for a mob including equipment bonuses.
    /// Combines base hitroll from Combat stats with equipment and affects.
    /// </summary>
    public static int GetMobEffectiveHitroll(MobInstance mob)
    {
        int effectiveHitroll = mob.Definition.Combat?.Hitroll ?? 0;
        
        // Add spell affects
        foreach (var affect in mob.Affects.Where(a => a.Location == AffectLocation.Hitroll))
        {
            effectiveHitroll += affect.Modifier;
        }
        
        // Add equipment bonuses
        effectiveHitroll += GetMobEquipmentBonus(mob, AffectLocation.Hitroll);
        
        return effectiveHitroll;
    }
    
    /// <summary>
    /// Get effective damroll for a mob including equipment bonuses.
    /// Combines base damroll from Combat stats with equipment and affects.
    /// </summary>
    public static int GetMobEffectiveDamroll(MobInstance mob)
    {
        int effectiveDamroll = mob.Definition.Combat?.Damroll ?? 0;
        
        // Add spell affects
        foreach (var affect in mob.Affects.Where(a => a.Location == AffectLocation.Damroll))
        {
            effectiveDamroll += affect.Modifier;
        }
        
        // Add equipment bonuses
        effectiveDamroll += GetMobEquipmentBonus(mob, AffectLocation.Damroll);
        
        return effectiveDamroll;
    }
}
