using EliteMud.Application.Skills;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Combat;

/// <summary>
/// Legacy hit() up to its damage() call (fight.c:1380–1490), for players and mobs alike: whether a melee attack
/// lands, and how much it does before the victim's dodge or parry (<see cref="CombatCalculator.DefendAgainstMelee"/>).
/// Every stat is the effective one: base, spells and gear (#78).
/// </summary>
public sealed class MeleeRoll
{
    private readonly CombatCalculator _calculator;
    private readonly IWorldState _worldState;
    private readonly EffectiveStats _stats;

    public MeleeRoll(CombatCalculator calculator, IWorldState worldState, EffectiveStats stats)
    {
        _stats = stats;
        _calculator = calculator;
        _worldState = worldState;
    }

    /// <summary>
    /// Whether the attacker is too winded to attack (legacy hit(), fight.c:1162): out of movement, the attack doesn't
    /// happen at all, not even as a miss, and one time in four they pant for breath. Null when they can attack;
    /// otherwise the messages to show, often none. Only players have movement.
    /// </summary>
    public IReadOnlyList<SkillMessage>? Winded(ICombatant attacker)
    {
        if (attacker is not PlayerState { Movement: < 1 })
        {
            return null;
        }

        return _calculator.RollDice(1, 4) == 1 // legacy !number(0, 3)
            ? new[]
            {
                new SkillMessage(SkillMessageTarget.Actor, "You pant for breath..."),
                new SkillMessage(SkillMessageTarget.Others, "$n pants for breath.")
            }
            : Array.Empty<SkillMessage>();
    }

    /// <summary>
    /// The to-hit roll (legacy hit(), fight.c:1380–1420), for any attacker and victim (#48). A player swinging a
    /// weapon spends weight / 8 movement on it, hit or miss.
    /// </summary>
    /// <param name="offHand">The dual-wield attack, which swings the held weapon</param>
    public bool Hits(ICombatant attacker, ICombatant victim, bool offHand = false)
    {
        if (attacker is PlayerState player && Weapon(player, offHand) is { } weapon)
        {
            player.Movement = (short)Math.Max(0, player.Movement - weapon.Definition.Weight / 8);
        }

        return _calculator.AttackHits(
            _stats.Thac0(attacker),
            ToHit(attacker),
            _stats.Get(victim, AffectLocation.ArmorClass),
            _stats.Get(victim, AffectLocation.Dexterity),
            victimAwake: victim.Position > Position.Sleeping,
            victimOutOfMovement: victim is PlayerState { Movement: < 1 });
    }

    /// <summary>
    /// The damage of a melee hit that lands: strength bonus and damroll, then the wielded weapon's dice and effects
    /// (bless, evil, flame) and, for a mob, its attack dice too; bare-handed, a mob's attack dice or a player's 0–2.
    /// Then more against a victim who isn't on their feet, and at least 1 (legacy MAX(1, dam)). Backstab multiplies
    /// this; other hits go on to the victim's dodge or parry.
    /// </summary>
    /// <param name="offHand">The dual-wield attack, which swings the held weapon</param>
    public int Damage(ICombatant attacker, ICombatant victim, bool offHand = false)
    {
        var damage = StrengthDamage(attacker) + _stats.Get(attacker, AffectLocation.Damroll);

        var weapon = Weapon(attacker, offHand);
        var weaponDetails = weapon?.Definition.Details?.Weapon;
        if (weapon != null && weaponDetails != null)
        {
            damage += _calculator.RollDice(weaponDetails.DiceCount, weaponDetails.DiceSides);
            damage += _calculator.ApplyWeaponEffects(weapon.Definition.Flags, victim);
            if (attacker is MobInstance armedMob && armedMob.Definition.Attacks.Count > 0)
            {
                damage += MobAttack(armedMob);
            }
        }
        else if (attacker is MobInstance mob)
        {
            // A mob without attack data (legacy always has it) hits for its level + 1d4
            damage += mob.Definition.Attacks.Count > 0
                ? MobAttack(mob)
                : mob.Definition.Level + _calculator.RollDice(1, 4);
        }
        else
        {
            damage += _calculator.RollDice(1, 3) - 1; // legacy number(0, 2)
        }

        return Math.Max(1, _calculator.CalculateDamageWithPositionMultiplier(damage, victim.Position));
    }

    /// <summary>A mob's natural attack dice, with or without a weapon (legacy hit()).</summary>
    private int MobAttack(MobInstance mob)
    {
        var attack = mob.Definition.Attacks[0];
        return _calculator.RollDice(attack.DamageDiceCount, attack.DamageDiceSides) + attack.DamageBonus;
    }

    /// <summary>Legacy: str_app tohit + hitroll + (int − 13) / 3 + (wis − 13) / 3.</summary>
    private int ToHit(ICombatant combatant) =>
        StrengthApply.ToHit(_stats.Get(combatant, AffectLocation.Strength), _stats.StrengthAdd(combatant))
        + _stats.Get(combatant, AffectLocation.Hitroll)
        + (_stats.Get(combatant, AffectLocation.Intelligence) - 13) / 3
        + (_stats.Get(combatant, AffectLocation.Wisdom) - 13) / 3;

    /// <summary>Legacy str_app[STRENGTH_APPLY_INDEX(ch)].todam.</summary>
    private int StrengthDamage(ICombatant combatant) =>
        StrengthApply.ToDamage(_stats.Get(combatant, AffectLocation.Strength), _stats.StrengthAdd(combatant));

    /// <summary>
    /// The weapon an attack swings (legacy hit(), fight.c:1244): for the dual-wield attack the held item if it's a
    /// weapon, otherwise the wielded one, in one hand or both. Null bare-handed, or when the item isn't a weapon.
    /// </summary>
    private ObjectInstance? Weapon(ICombatant attacker, bool offHand)
    {
        if (offHand && Equipped(attacker, EquipmentSlot.Hold) is { } held && IsWeapon(held))
        {
            return held;
        }

        var wielded = Equipped(attacker, EquipmentSlot.Wield) ?? Equipped(attacker, EquipmentSlot.BothHands);
        return wielded != null && IsWeapon(wielded) ? wielded : null;
    }

    /// <summary>Legacy ITEM_WEAPON or ITEM_FIREWEAPON.</summary>
    private static bool IsWeapon(ObjectInstance item) => item.Definition.Type is "Weapon" or "FireWeapon";

    private ObjectInstance? Equipped(ICombatant combatant, EquipmentSlot slot) => combatant switch
    {
        PlayerState player when player.EquipmentSlotToObjectId.TryGetValue((int)slot, out var id) =>
            _worldState.GetObjectInstance(id),
        MobInstance mob when mob.Equipment.TryGetValue(slot, out var item) => item,
        _ => null
    };
}
