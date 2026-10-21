using EliteMud.Game;

namespace EliteMud.Application.World;

public sealed class MobInstance : ICombatant
{
    private readonly Dictionary<EquipmentSlot, ObjectInstance> _equipment = new();
    private readonly List<Affect> _affects = new(); // Mobs can have affects too!
    private readonly List<long> _memory = new(); // Player IDs this mob remembers (for MOB_MEMORY)
    private readonly List<ObjectInstance> _inventory = new();

    public MobInstance(int instanceId, MobDefinition definition)
    {
        InstanceId = instanceId;
        Definition = definition;
        // Initialize HP from mob definition's MaxHitPoints
        HitPoints = (short)Math.Min(definition.MaxHitPoints, short.MaxValue);

        // Starts at the content value and grows as the mob damages and kills (legacy GET_EXP on an NPC)
        Experience = definition.Experience;
        
        // Initialize hometown to current spawn location (will be set by zone reset)
        Hometown = null;
    }

    public int InstanceId { get; }
    
    public MobDefinition Definition { get; }

    /// <summary>
    /// The room the mob is in (legacy ch->in_room), or <see cref="Nowhere"/> before it enters the world and after it
    /// leaves. Only <see cref="WorldPopulation"/> sets it, as it moves the mob between rooms.
    /// </summary>
    public int RoomId { get; internal set; } = Nowhere;

    /// <summary>Legacy NOWHERE: not in any room.</summary>
    public const int Nowhere = -1;
    
    // ===== Combat state =====
    public ICombatant? Fighting { get; set; } // Who this mob is fighting: a player or another mob
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
    public int Experience { get; set; }
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
    
    
    

    public IReadOnlyDictionary<EquipmentSlot, ObjectInstance> Equipment => _equipment;
    
    /// <summary>
    /// What this mob carries (from scavenging and GiveMob zone resets).
    /// Legacy: ch->carrying (linked list)
    /// </summary>
    public IReadOnlyList<ObjectInstance> Inventory => _inventory;
    
    /// <summary>
    /// Add an object to this mob's inventory.
    /// </summary>
    public void AddToInventory(ObjectInstance item)
    {
        _inventory.Add(item);
    }
    
    /// <summary>
    /// Remove an object from this mob's inventory.
    /// Returns true if object was found and removed.
    /// </summary>
    public bool RemoveFromInventory(ObjectInstance item)
    {
        return _inventory.Remove(item);
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

public interface IWorldState
{
    WorldDefinition World { get; }

    /// <summary>The characters in the world, players and mobs, and the rooms they're in.</summary>
    WorldPopulation Population { get; }

    IReadOnlyList<ObjectInstance> GetObjectsInRoom(int roomId);

    /// <summary>
    /// Advance every portal's open timer by one mud hour.
    /// Returns the portals that closed, with the room they're in.
    /// </summary>
    IReadOnlyList<(int RoomId, ObjectInstance Portal)> TickPortals();

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
    /// Get the runtime closed/locked state of an exit.
    /// Doors always have one. A closed exit that isn't a door (a barrier) has one too, since legacy keeps
    /// EX_CLOSED/EX_LOCKED on any exit. Returns null for exits that can't be closed.
    /// Commands that act on doors (open, close, lock, unlock, bash) check <see cref="ExitDefinition.IsDoor"/> first.
    /// </summary>
    DoorState? GetDoorState(int roomId, Direction direction);

    /// <summary>
    /// True if the exit is closed: a closed door, or a closed exit that isn't a door (a barrier).
    /// Legacy blocks movement on EX_CLOSED alone.
    /// </summary>
    bool IsExitClosed(int roomId, Direction direction);
    
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
