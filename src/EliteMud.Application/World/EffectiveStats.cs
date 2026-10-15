using EliteMud.Game;

namespace EliteMud.Application.World;

/// <summary>
/// The one place a character's effective stats are worked out (#80): base + affects + gear, for players and mobs
/// alike, kept within legacy's limits (stat_check, handler.c:219; #88). Ability scores stay between 0 and the race's
/// maximum for a player, 0 and 25 for a mob (legacy row 0); max hit points are at least 1, max mana and movement at
/// least 0. Armor class counts both the Armor and ArmorClass affect locations; lower is better.
/// </summary>
public sealed class EffectiveStats
{
    private const int MobAbilityMax = 25;

    private readonly RulesContent _rules;

    public EffectiveStats(RulesContent rules)
    {
        _rules = rules;
    }

    /// <summary>
    /// The effective value of a stat: strength to charisma, hitroll, damroll, armor class, max hit points, mana or
    /// movement. Locations without a base value (saving throws, magic resistance) count only affects and gear.
    /// </summary>
    public int Get(ICombatant combatant, AffectLocation stat)
    {
        var value = Base(combatant, stat) + AffectBonus(combatant, stat) + GearBonus(combatant, stat);
        return stat switch
        {
            AffectLocation.Strength or AffectLocation.Dexterity or AffectLocation.Intelligence
                or AffectLocation.Wisdom or AffectLocation.Constitution or AffectLocation.Charisma =>
                Math.Clamp(value, 0, AbilityMax(combatant, stat)),
            AffectLocation.MaxHit => Math.Max(1, value),
            AffectLocation.MaxMana or AffectLocation.MaxMovement => Math.Max(0, value),
            _ => value
        };
    }

    /// <summary>
    /// A player's exceptional strength (18/xx), at most 100 (legacy stat_check); it only counts at 18
    /// (<see cref="StrengthApply"/>). Mobs have none.
    /// </summary>
    public int StrengthAdd(ICombatant combatant) =>
        combatant is PlayerState player ? Math.Clamp((int)player.StrengthAdd, 0, 100) : 0;

    /// <summary>
    /// Legacy get_thaco() (fight.c:1075): a mob's THAC0 is 20, a player's comes from their class (20 if it's
    /// unknown). Lower hits more often; the to-hit bonus comes off it in the roll.
    /// </summary>
    public int Thac0(ICombatant combatant) =>
        combatant is PlayerState player && _rules.Classes.TryGetValue(player.CharacterClass, out var playerClass)
            ? playerClass.Thac0(player.Level)
            : 20;

    /// <summary>What spells and other affects add to a stat.</summary>
    public int AffectBonus(ICombatant combatant, AffectLocation stat) =>
        combatant.Affects.Where(affect => Counts(affect.Location, stat)).Sum(affect => affect.Modifier);

    /// <summary>What worn and wielded items add to a stat.</summary>
    public int GearBonus(ICombatant combatant, AffectLocation stat) =>
        combatant.Equipment.Values
            .SelectMany(item => item.Definition.Affects)
            .Where(affect => Counts(affect.Location, stat))
            .Sum(affect => affect.Modifier);

    private static bool Counts(AffectLocation location, AffectLocation stat) =>
        location == stat || (stat == AffectLocation.ArmorClass && location == AffectLocation.Armor);

    private static int Base(ICombatant combatant, AffectLocation stat) => combatant switch
    {
        PlayerState player => stat switch
        {
            AffectLocation.Strength => player.Strength,
            AffectLocation.Dexterity => player.Dexterity,
            AffectLocation.Intelligence => player.Intelligence,
            AffectLocation.Wisdom => player.Wisdom,
            AffectLocation.Constitution => player.Constitution,
            AffectLocation.Charisma => player.Charisma,
            AffectLocation.Hitroll => player.Hitroll,
            AffectLocation.Damroll => player.Damroll,
            AffectLocation.ArmorClass => player.ArmorClass,
            AffectLocation.MaxHit => player.MaxHitPoints,
            AffectLocation.MaxMana => player.MaxMana,
            AffectLocation.MaxMovement => player.MaxMovement,
            _ => 0
        },
        MobInstance mob => stat switch
        {
            AffectLocation.Strength => mob.Definition.Stats.Strength,
            AffectLocation.Dexterity => mob.Definition.Stats.Dexterity,
            AffectLocation.Intelligence => mob.Definition.Stats.Intelligence,
            AffectLocation.Wisdom => mob.Definition.Stats.Wisdom,
            AffectLocation.Constitution => mob.Definition.Stats.Constitution,
            AffectLocation.Charisma => mob.Definition.Stats.Charisma,
            AffectLocation.Hitroll => mob.Definition.Combat?.Hitroll ?? 0,
            AffectLocation.Damroll => mob.Definition.Combat?.Damroll ?? 0,
            AffectLocation.ArmorClass => mob.Definition.ArmorClass,
            AffectLocation.MaxHit => mob.Definition.MaxHitPoints,
            _ => 0
        },
        _ => 0
    };

    /// <summary>Legacy stat_check: a player's race maximum (25 for an unknown race), 25 for a mob.</summary>
    private int AbilityMax(ICombatant combatant, AffectLocation stat)
    {
        if (combatant is not PlayerState player || !_rules.Races.TryGetValue(player.Race, out var race))
        {
            return MobAbilityMax;
        }

        var max = race.StatMax;
        return stat switch
        {
            AffectLocation.Strength => max.Strength,
            AffectLocation.Dexterity => max.Dexterity,
            AffectLocation.Intelligence => max.Intelligence,
            AffectLocation.Wisdom => max.Wisdom,
            AffectLocation.Constitution => max.Constitution,
            _ => max.Charisma
        };
    }
}
