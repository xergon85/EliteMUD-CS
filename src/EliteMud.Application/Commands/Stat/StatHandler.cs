using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;
using System.Text;

namespace EliteMud.Application.Commands.Stat;

/// <summary>
/// Handler for the 'stat' command that displays detailed character statistics.
/// Shows base stats, modifiers from affects, AC, hitroll, damroll, saves, clan info, etc.
/// 
/// Legacy format from screenshot:
/// Level 76 - Xergon the Troll Knight -
/// 221 year old male troll ranger player
/// Str: [20]  Int: [9]  Wis: [9]  Dex: [16]  Con: [18]  Cha: [12]
/// AC[-177/10  Mod: -26/10] Hitroll[22  Mod: 1] Damroll[15  Mod: 8] THAC0[-5]
/// Saves[Physical: 5 / Mental: 0 / Magic: 0 / Poison: 0] Magic Resistance[15]
/// Clan[116] ClanLevel[9] Hometown[Midgaard] Pracs[91]
/// Worships[None]
/// Magic: (from item) sneak
/// Magic: (innate)    regeneration
/// Magic: (innate)    infravision
/// </summary>
public sealed class StatHandler
{
    private readonly IWorldState _worldState;

    public StatHandler(IWorldState worldState)
    {
        _worldState = worldState;
    }

    public CommandResult Handle(PlayerState player)
    {
        var output = new StringBuilder();

        // Line 1: Level in red, name and title in cyan
        var title = player.Title ?? "the newbie";
        var raceClass = $"{player.Race.ToLower()} {player.CharacterClass.ToLower()}";
        output.AppendLine($"Level #R{player.Level}#N - #C{player.Name} {title}#N -");

        // Line 2: Age, sex, race, class, "player" - all green
        // TODO: Calculate actual age from Birth/CreatedAt field
        var age = 17; // Placeholder
        var sex = player.Sex switch
        {
            1 => "male",
            2 => "female",
            _ => "neutral"
        };
        output.AppendLine($"#G{age} year old {sex} {raceClass} player#N");

        // Line 3: Stats - values in cyan
        var strMod = GetStatModifier(player, AffectLocation.Strength);
        var intMod = GetStatModifier(player, AffectLocation.Intelligence);
        var wisMod = GetStatModifier(player, AffectLocation.Wisdom);
        var dexMod = GetStatModifier(player, AffectLocation.Dexterity);
        var conMod = GetStatModifier(player, AffectLocation.Constitution);
        var chaMod = GetStatModifier(player, AffectLocation.Charisma);

        var strDisplay = FormatStatWithModifier(player.Strength, strMod);
        var intDisplay = FormatStatWithModifier(player.Intelligence, intMod);
        var wisDisplay = FormatStatWithModifier(player.Wisdom, wisMod);
        var dexDisplay = FormatStatWithModifier(player.Dexterity, dexMod);
        var conDisplay = FormatStatWithModifier(player.Constitution, conMod);
        var chaDisplay = FormatStatWithModifier(player.Charisma, chaMod);

        output.AppendLine($"Str: {strDisplay}  Int: {intDisplay}  Wis: {wisDisplay}  Dex: {dexDisplay}  Con: {conDisplay}  Cha: {chaDisplay}");

        // Line 4: AC, Hitroll, Damroll, THAC0 - all values in red
        var baseAC = player.ArmorClass;
        var acSpellMod = GetStatModifier(player, AffectLocation.ArmorClass);
        var acEquipMod = _worldState.GetEquipmentBonus(player, AffectLocation.ArmorClass);
        var acTotalMod = acSpellMod + acEquipMod;
        var effectiveAC = _worldState.GetTotalEffectiveArmorClass(player);
        
        var baseHitroll = player.Hitroll;
        var hitrollSpellMod = GetStatModifier(player, AffectLocation.Hitroll);
        var hitrollEquipMod = _worldState.GetEquipmentBonus(player, AffectLocation.Hitroll);
        var hitrollTotalMod = hitrollSpellMod + hitrollEquipMod;
        var effectiveHitroll = _worldState.GetTotalEffectiveHitroll(player);
        
        var baseDamroll = player.Damroll;
        var damrollSpellMod = GetStatModifier(player, AffectLocation.Damroll);
        var damrollEquipMod = _worldState.GetEquipmentBonus(player, AffectLocation.Damroll);
        var damrollTotalMod = damrollSpellMod + damrollEquipMod;
        var effectiveDamroll = _worldState.GetTotalEffectiveDamroll(player);

        // THAC0 calculation (legacy DikuMUD formula)
        var thac0 = CalculateTHAC0(player.Level, effectiveHitroll);

        // Format displays with breakdown of equipment and spell modifiers
        string acDisplay;
        if (acTotalMod != 0)
        {
            if (acEquipMod != 0 && acSpellMod != 0)
            {
                // Show breakdown: Mod: -26/10 (Eq: -15, Spell: -11)
                acDisplay = $"[#R{effectiveAC}/{baseAC / 10}#N  Mod: #w{acTotalMod}/10#N (#wEq: {acEquipMod}/10, Spell: {acSpellMod}/10#N)]";
            }
            else if (acEquipMod != 0)
            {
                // Only equipment bonus
                acDisplay = $"[#R{effectiveAC}/{baseAC / 10}#N  Mod: #w{acTotalMod}/10#N (#wEq#N)]";
            }
            else
            {
                // Only spell bonus
                acDisplay = $"[#R{effectiveAC}/{baseAC / 10}#N  Mod: #w{acTotalMod}/10#N (#wSpell#N)]";
            }
        }
        else
        {
            acDisplay = $"[#R{effectiveAC}/{baseAC / 10}#N]";
        }

        string hitrollDisplay;
        if (hitrollTotalMod != 0)
        {
            if (hitrollEquipMod != 0 && hitrollSpellMod != 0)
            {
                hitrollDisplay = $"[#R{effectiveHitroll}#N  Mod: #w{hitrollTotalMod}#N (#wEq: {hitrollEquipMod}, Spell: {hitrollSpellMod}#N)]";
            }
            else if (hitrollEquipMod != 0)
            {
                hitrollDisplay = $"[#R{effectiveHitroll}#N  Mod: #w{hitrollTotalMod}#N (#wEq#N)]";
            }
            else
            {
                hitrollDisplay = $"[#R{effectiveHitroll}#N  Mod: #w{hitrollTotalMod}#N (#wSpell#N)]";
            }
        }
        else
        {
            hitrollDisplay = $"[#R{effectiveHitroll}#N]";
        }

        string damrollDisplay;
        if (damrollTotalMod != 0)
        {
            if (damrollEquipMod != 0 && damrollSpellMod != 0)
            {
                damrollDisplay = $"[#R{effectiveDamroll}#N  Mod: #w{damrollTotalMod}#N (#wEq: {damrollEquipMod}, Spell: {damrollSpellMod}#N)]";
            }
            else if (damrollEquipMod != 0)
            {
                damrollDisplay = $"[#R{effectiveDamroll}#N  Mod: #w{damrollTotalMod}#N (#wEq#N)]";
            }
            else
            {
                damrollDisplay = $"[#R{effectiveDamroll}#N  Mod: #w{damrollTotalMod}#N (#wSpell#N)]";
            }
        }
        else
        {
            damrollDisplay = $"[#R{effectiveDamroll}#N]";
        }

        output.AppendLine($"AC{acDisplay} Hitroll{hitrollDisplay} Damroll{damrollDisplay} THAC0[#R{thac0}#N]");

        // Line 5: Saving throws and magic resistance - all values in red
        // TODO: Implement saving throw fields in PlayerState
        var savingPhysical = GetStatModifier(player, AffectLocation.SavingPhysical);
        var savingMental = GetStatModifier(player, AffectLocation.SavingMental);
        var savingMagic = GetStatModifier(player, AffectLocation.SavingMagic);
        var savingPoison = GetStatModifier(player, AffectLocation.SavingPoison);
        var magicResist = GetStatModifier(player, AffectLocation.MagicResistance);

        output.AppendLine($"Saves[Physical: #R{savingPhysical}#N / Mental: #R{savingMental}#N / Magic: #R{savingMagic}#N / Poison: #R{savingPoison}#N] Magic Resistance[#R{magicResist}#N]");

        // Line 6: Clan, ClanLevel, Hometown, Pracs - all values in blue
        // TODO: Implement Clan, Hometown, Practices fields
        output.AppendLine($"Clan[#BNone#N] ClanLevel[#B0#N] Hometown[#BMidgaard#N] Pracs[#B0#N]");

        // Line 7: Worships (deity) - value in bold blue
        // TODO: Implement Deity field
        output.AppendLine($"Worships[#bNone#N]");

        // Lines 8+: Magic abilities - spell names in cyan
        // Show active affects
        if (player.Affects.Count > 0)
        {
            foreach (var affect in player.Affects)
            {
                var affectName = GetAffectName(affect.Type);
                
                // Determine source label based on affect source
                string sourceLabel;
                if (affect.Source == "item")
                {
                    sourceLabel = "from item";
                }
                else if (affect.Source == "innate" || affect.Source == "racial")
                {
                    sourceLabel = "innate";
                }
                else
                {
                    // Spell affects show duration in hours
                    var hoursText = affect.DurationHours == 1 ? "hour" : "hours";
                    sourceLabel = $"{affect.DurationHours} {hoursText}";
                }
                
                output.AppendLine($"Magic: ({sourceLabel})    #C{affectName.ToLower()}#N");
            }
        }

        // TODO: Show innate racial abilities (regeneration, infravision, etc.)
        // These would be permanent affects with source="innate" or separate racial ability system
        // For now, add some placeholder innate abilities based on race
        if (player.Race.Equals("Troll", StringComparison.OrdinalIgnoreCase))
        {
            output.AppendLine($"Magic: (innate)    #Cregeneration#N");
        }
        
        if (player.Race.Equals("Elf", StringComparison.OrdinalIgnoreCase) ||
            player.Race.Equals("Drow", StringComparison.OrdinalIgnoreCase) ||
            player.Race.Equals("Dwarf", StringComparison.OrdinalIgnoreCase))
        {
            output.AppendLine($"Magic: (innate)    #Cinfravision#N");
        }

        return CommandResult.Ok(output.ToString());
    }

    /// <summary>
    /// Get total modifier from all affects for a specific location.
    /// </summary>
    private static int GetStatModifier(PlayerState player, AffectLocation location)
    {
        return player.Affects
            .Where(a => a.Location == location)
            .Sum(a => a.Modifier);
    }

    /// <summary>
    /// Format a stat value with optional modifier display.
    /// Examples: "[20]" or "[20+2]" or "[20-3]"
    /// Legacy format: stat values in cyan (#C)
    /// </summary>
    private static string FormatStatWithModifier(sbyte baseValue, int modifier)
    {
        if (modifier == 0)
        {
            return $"[#C{baseValue}#N]";
        }

        var effectiveValue = baseValue + modifier;
        var modSign = modifier > 0 ? "+" : "";
        return $"[#C{effectiveValue}#N (#C{baseValue}#N{modSign}{modifier})]";
    }

    /// <summary>
    /// Calculate THAC0 (To Hit Armor Class 0).
    /// Legacy DikuMUD formula: 20 - level - hitroll
    /// Lower is better (easier to hit).
    /// </summary>
    private static int CalculateTHAC0(byte level, sbyte hitroll)
    {
        // Legacy formula from DikuMUD
        return 20 - level - hitroll;
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
}
