using EliteMud.Application.Commands.Shared;
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
    public CommandResult Handle(PlayerState player)
    {
        var output = new StringBuilder();

        // Line 1: Level, name, title, race, class
        var title = player.Title ?? "the newbie";
        var raceClass = $"{player.Race.ToLower()} {player.CharacterClass.ToLower()}";
        output.AppendLine($"#RLevel {player.Level}#N - #B{player.Name}#N #Cthe {title}#N -");

        // Line 2: Age, sex, race, class, "player"
        // TODO: Calculate actual age from Birth/CreatedAt field
        var age = 17; // Placeholder
        var sex = player.Sex switch
        {
            1 => "male",
            2 => "female",
            _ => "neutral"
        };
        output.AppendLine($"{age} year old {sex} {raceClass} player");

        // Line 3: Stats with modifiers from affects
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

        // Line 4: AC, Hitroll, Damroll, THAC0
        var baseAC = player.ArmorClass;
        var acMod = GetStatModifier(player, AffectLocation.ArmorClass);
        var effectiveAC = player.GetEffectiveArmorClass();
        
        var baseHitroll = player.Hitroll;
        var hitrollMod = GetStatModifier(player, AffectLocation.Hitroll);
        var effectiveHitroll = player.GetEffectiveHitroll();
        
        var baseDamroll = player.Damroll;
        var damrollMod = GetStatModifier(player, AffectLocation.Damroll);
        var effectiveDamroll = player.GetEffectiveDamroll();

        // THAC0 calculation (legacy DikuMUD formula)
        var thac0 = CalculateTHAC0(player.Level, effectiveHitroll);

        var acDisplay = acMod != 0 ? $"[#G{effectiveAC}#N/#Y{baseAC / 10}#N  Mod: #C{acMod}#N/#Y10#N]" : $"[#G{effectiveAC}#N/#Y{baseAC / 10}#N]";
        var hitrollDisplay = hitrollMod != 0 ? $"[#G{effectiveHitroll}#N  Mod: #C{hitrollMod}#N]" : $"[#G{effectiveHitroll}#N]";
        var damrollDisplay = damrollMod != 0 ? $"[#G{effectiveDamroll}#N  Mod: #C{damrollMod}#N]" : $"[#G{effectiveDamroll}#N]";

        output.AppendLine($"AC{acDisplay} Hitroll{hitrollDisplay} Damroll{damrollDisplay} THAC0[#R{thac0}#N]");

        // Line 5: Saving throws and magic resistance
        // TODO: Implement saving throw fields in PlayerState
        var savingPhysical = GetStatModifier(player, AffectLocation.SavingPhysical);
        var savingMental = GetStatModifier(player, AffectLocation.SavingMental);
        var savingMagic = GetStatModifier(player, AffectLocation.SavingMagic);
        var savingPoison = GetStatModifier(player, AffectLocation.SavingPoison);
        var magicResist = GetStatModifier(player, AffectLocation.MagicResistance);

        output.AppendLine($"Saves[Physical: #Y{savingPhysical}#N / Mental: #Y{savingMental}#N / Magic: #Y{savingMagic}#N / Poison: #Y{savingPoison}#N] Magic Resistance[#C{magicResist}#N]");

        // Line 6: Clan, ClanLevel, Hometown, Pracs
        // TODO: Implement Clan, Hometown, Practices fields
        output.AppendLine($"Clan[#mNone#N] ClanLevel[#m0#N] Hometown[#GUnknown#N] Pracs[#Y0#N]");

        // Line 7: Worships (deity)
        // TODO: Implement Deity field
        output.AppendLine($"Worships[#bNone#N]");

        // Lines 8+: Magic abilities (from items and innate)
        // Show active affects
        if (player.Affects.Count > 0)
        {
            foreach (var affect in player.Affects)
            {
                var affectName = GetAffectName(affect.Type);
                var source = affect.Source ?? "unknown";
                var sourceLabel = source == "item" ? "from item" : "innate";
                output.AppendLine($"Magic: #C({sourceLabel})#N    #G{affectName.ToLower()}#N");
            }
        }

        // TODO: Show innate racial abilities (regeneration, infravision, etc.)
        // For now, add some placeholder innate abilities based on race
        if (player.Race.Equals("Troll", StringComparison.OrdinalIgnoreCase))
        {
            output.AppendLine($"Magic: #C(innate)#N    #Gregeneration#N");
        }
        
        if (player.Race.Equals("Elf", StringComparison.OrdinalIgnoreCase) ||
            player.Race.Equals("Drow", StringComparison.OrdinalIgnoreCase) ||
            player.Race.Equals("Dwarf", StringComparison.OrdinalIgnoreCase))
        {
            output.AppendLine($"Magic: #C(innate)#N    #Ginfravision#N");
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
    /// </summary>
    private static string FormatStatWithModifier(sbyte baseValue, int modifier)
    {
        if (modifier == 0)
        {
            return $"[#Y{baseValue}#N]";
        }

        var effectiveValue = baseValue + modifier;
        var modSign = modifier > 0 ? "+" : "";
        return $"[#G{effectiveValue}#N (#Y{baseValue}#N#C{modSign}{modifier}#N)]";
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
