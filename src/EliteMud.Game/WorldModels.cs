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

public sealed record RoomDefinition(int Id, string Name, string Description, IReadOnlyList<ExitDefinition> Exits);

public sealed record ScriptDefinition(string Id, string Hook, string Body, int? RoomId);

public sealed record StatBlock(
    int Strength,
    int Dexterity,
    int Intelligence,
    int Wisdom,
    int Constitution,
    int Charisma);

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
    IReadOnlyList<string> Skills);

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
    int Cost);

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

public sealed class PlayerState
{
    private readonly List<int> _inventoryObjectIds = new();
    private readonly Dictionary<int, int> _equipmentSlotToObjectId = new(); // slot -> objectInstanceId

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
    /// For now we only track if in combat (POS_FIGHTING) vs not.
    /// Legacy: GET_POS(ch) - POS_DEAD=0, POS_MORTALLYW=1, POS_INCAP=2, POS_STUNNED=3,
    ///         POS_SLEEPING=4, POS_RESTING=5, POS_SITTING=6, POS_FIGHTING=7, POS_STANDING=8
    /// </summary>
    public byte Position { get; set; } = 8; // POS_STANDING

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
