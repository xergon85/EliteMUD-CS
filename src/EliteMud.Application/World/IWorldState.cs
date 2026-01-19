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
    }

    public int InstanceId { get; }
    
    public MobDefinition Definition { get; }

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

    bool ResetZone(int zoneId);

    bool ResetZoneForRoom(int roomId, out int zoneId);
}
