namespace EliteMud.Game;

public sealed class ObjectDetails
{
    public ObjectLight? Light { get; init; }
    public ObjectSpellContainer? SpellContainer { get; init; }
    public ObjectWandStaff? Charges { get; init; }
    public ObjectWeapon? Weapon { get; init; }
    public ObjectMissile? Missile { get; init; }
    public ObjectArmor? Armor { get; init; }
    public ObjectTrap? Trap { get; init; }
    public ObjectContainer? Container { get; init; }
    public ObjectDrink? Drink { get; init; }
    public ObjectNote? Note { get; init; }
    public ObjectKey? Key { get; init; }
    public ObjectFood? Food { get; init; }
    public ObjectMoney? Money { get; init; }
    public ObjectPortal? Portal { get; init; }
}

public sealed record ObjectLight(int Color, int Type, int Hours);

public sealed record ObjectSpellContainer(int Level, IReadOnlyList<int> SpellIds);

public sealed record ObjectWandStaff(int SpellId, int Level, int Charges, int ChargesRemaining);

public sealed record ObjectWeapon(int DiceCount, int DiceSides, int DamageType, int HitPoints);

public sealed record ObjectMissile(int Damage, int DamageType);

public sealed record ObjectArmor(int ArmorClass, int HitPoints);

public sealed record ObjectTrap(int SpellId, int HitPoints);

public sealed record ObjectContainer(
    int Capacity,
    IReadOnlyList<string> Flags,
    int KeyId,
    int CorpseType,
    int CorpseBlood,
    int CorpseLevel);

public sealed record ObjectDrink(int Capacity, int Amount, int Liquid, bool Poisoned);

public sealed record ObjectNote(int Language);

public sealed record ObjectKey(int KeyType, int Timer, int TimerSet);

public sealed record ObjectFood(int Filling, bool Poisoned);

public sealed record ObjectMoney(int Amount);

public sealed record ObjectPortal(
    int Destination,
    IReadOnlyList<string> Flags,
    int LockItem,
    int MinLevel,
    int MaxLevel,
    int Duration);
