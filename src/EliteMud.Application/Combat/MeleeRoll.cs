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

    public MeleeRoll(CombatCalculator calculator, IWorldState worldState)
    {
        _calculator = calculator;
        _worldState = worldState;
    }

    /// <summary>
    /// The to-hit roll, for now only between players (#48). Out of movement, an attack misses; one that lands
    /// costs a movement point.
    /// </summary>
    public bool Hits(PlayerState attacker, PlayerState victim)
    {
        if (attacker.Movement < 1)
        {
            return false;
        }

        var hits = _calculator.AttackHits(
            attacker,
            _worldState.GetTotalEffectiveHitroll(attacker),
            _worldState.GetTotalEffectiveStrength(attacker),
            attacker.StrengthAdd,
            _worldState.GetTotalEffectiveIntelligence(attacker),
            _worldState.GetTotalEffectiveWisdom(attacker),
            _worldState.GetTotalEffectiveArmorClass(victim),
            _worldState.GetTotalEffectiveDexterity(victim));
        if (hits)
        {
            attacker.Movement = (short)Math.Max(0, attacker.Movement - 1);
        }

        return hits;
    }

    /// <summary>
    /// The damage of a melee hit that lands: strength bonus and damroll, then the wielded weapon's dice and effects
    /// (bless, evil, flame) and, for a mob, its attack dice too; bare-handed, a mob's attack dice or a player's 0–2.
    /// Then more against a victim who isn't on their feet, and at least 1 (legacy MAX(1, dam)). Backstab multiplies
    /// this; other hits go on to the victim's dodge or parry.
    /// </summary>
    public int Damage(ICombatant attacker, ICombatant victim)
    {
        var damage = StrengthDamage(attacker) + Damroll(attacker);

        var weapon = Wielded(attacker);
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

    /// <summary>Legacy str_app[STRENGTH_APPLY_INDEX(ch)].todam; mobs have no 18/xx bonus.</summary>
    private int StrengthDamage(ICombatant combatant) => combatant switch
    {
        PlayerState player => StrengthApply.ToDamage(_worldState.GetTotalEffectiveStrength(player), player.StrengthAdd),
        MobInstance mob => StrengthApply.ToDamage(WorldStateExtensions.GetMobEffectiveStrength(mob)),
        _ => 0
    };

    private int Damroll(ICombatant combatant) => combatant switch
    {
        PlayerState player => _worldState.GetTotalEffectiveDamroll(player),
        MobInstance mob => WorldStateExtensions.GetMobEffectiveDamroll(mob),
        _ => 0
    };

    private ObjectInstance? Wielded(ICombatant combatant) => combatant switch
    {
        PlayerState player when player.EquipmentSlotToObjectId.TryGetValue((int)EquipmentSlot.Wield, out var id) =>
            _worldState.GetObjectInstance(id),
        MobInstance mob when mob.Equipment.TryGetValue(EquipmentSlot.Wield, out var weapon) => weapon,
        _ => null
    };
}
