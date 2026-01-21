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

public sealed class MobInstance
{
    private readonly Dictionary<EquipmentSlot, ObjectInstance> _equipment = new();

    public MobInstance(int instanceId, MobDefinition definition)
    {
        InstanceId = instanceId;
        Definition = definition;
        // Initialize HP based on level (legacy formula is more complex, but this is a reasonable start)
        HitPoints = definition.Level * 10;
    }

    public int InstanceId { get; }
    
    public MobDefinition Definition { get; }
    
    // Combat state
    public int? FightingConnectionId { get; set; } // The player connection ID this mob is fighting
    public int? FightingMobInstanceId { get; set; } // The mob instance ID this mob is fighting (for mob-vs-mob combat)
    public byte Position { get; set; } = 8; // POS_STANDING
    public int HitPoints { get; set; } // Current HP (mobs don't have MaxHP property, use Definition.MaxHitPoints)

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

public sealed record ObjectInstance(int InstanceId, ObjectDefinition Definition);

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

    IReadOnlyList<ObjectDefinition> SearchObjects(string query);

    bool ResetZone(int zoneId);

    bool ResetZoneForRoom(int roomId, out int zoneId);
}
