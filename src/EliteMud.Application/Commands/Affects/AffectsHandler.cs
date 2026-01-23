using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using System.Text;

namespace EliteMud.Application.Commands.Affects;

/// <summary>
/// Handler for the 'affects' command that displays active buffs/debuffs on the player.
/// Shows affect type, modifiers, and remaining duration.
/// </summary>
public sealed class AffectsHandler
{
    public CommandResult Handle(ICombatant combatant)
    {
        var affects = combatant.Affects;

        if (affects.Count == 0)
        {
            return CommandResult.Ok("You are not affected by any spells.");
        }

        var output = new StringBuilder();
        output.AppendLine("#GAffects:#N");
        output.AppendLine();

        foreach (var affect in affects)
        {
            // Format: "Spell: Armor"
            var spellName = GetAffectName(affect.Type);
            output.AppendLine($"#CSpell:#N {spellName}");

            // Format: "  Modifies: Armor Class by -20"
            if (affect.Modifier != 0)
            {
                var locationName = GetLocationName(affect.Location);
                var modifierText = affect.Modifier > 0 ? $"+{affect.Modifier}" : $"{affect.Modifier}";
                output.AppendLine($"  #YModifies:#N {locationName} by {modifierText}");
            }

            // Format: "  Duration: 24 hours"
            var hoursText = affect.DurationHours == 1 ? "hour" : "hours";
            output.AppendLine($"  #BDuration:#N {affect.DurationHours} {hoursText}");
            
            output.AppendLine();
        }

        return CommandResult.Ok(output.ToString().TrimEnd());
    }

    /// <summary>
    /// Get friendly name for affect type.
    /// </summary>
    private static string GetAffectName(AffectType type)
    {
        return type switch
        {
            AffectType.Armor => "Armor",
            AffectType.Bless => "Bless",
            AffectType.Curse => "Curse",
            AffectType.Poison => "Poison",
            AffectType.DetectInvisibility => "Detect Invisibility",
            AffectType.DetectMagic => "Detect Magic",
            AffectType.DetectPoison => "Detect Poison",
            AffectType.Sanctuary => "Sanctuary",
            _ => type.ToString()
        };
    }

    /// <summary>
    /// Get friendly name for affect location.
    /// </summary>
    private static string GetLocationName(AffectLocation location)
    {
        return location switch
        {
            AffectLocation.None => "None",
            AffectLocation.Strength => "Strength",
            AffectLocation.Dexterity => "Dexterity",
            AffectLocation.Intelligence => "Intelligence",
            AffectLocation.Wisdom => "Wisdom",
            AffectLocation.Constitution => "Constitution",
            AffectLocation.Charisma => "Charisma",
            AffectLocation.MaxHit => "Max Hit Points",
            AffectLocation.MaxMana => "Max Mana",
            AffectLocation.MaxMovement => "Max Movement",
            AffectLocation.ArmorClass => "Armor Class",
            AffectLocation.Hitroll => "Hit Roll",
            AffectLocation.Damroll => "Damage Roll",
            AffectLocation.SavingPhysical => "Saving Throw vs Physical",
            AffectLocation.SavingMental => "Saving Throw vs Mental",
            AffectLocation.SavingMagic => "Saving Throw vs Magic",
            AffectLocation.SavingPoison => "Saving Throw vs Poison",
            AffectLocation.MagicResistance => "Magic Resistance",
            _ => location.ToString()
        };
    }
}
