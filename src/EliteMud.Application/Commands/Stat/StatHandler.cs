using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Progression;
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
    private readonly RulesContent _rules;
    private readonly EffectiveStats _stats;

    public StatHandler(RulesContent rules, EffectiveStats stats)
    {
        _rules = rules;
        _stats = stats;
    }

    public CommandResult Handle(PlayerState player)
    {
        var output = new StringBuilder();

        // Line 1: Level in red, name and title in cyan
        var title = PlayerProgression.DisplayTitle(player, _rules);
        var raceClass = $"{_rules.RaceName(player.Race).ToLower()} {_rules.ClassName(player.CharacterClass).ToLower()}";
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
        var strSpellMod = _stats.AffectBonus(player, AffectLocation.Strength);
        var strEquipMod = _stats.GearBonus(player, AffectLocation.Strength);
        
        var intSpellMod = _stats.AffectBonus(player, AffectLocation.Intelligence);
        var intEquipMod = _stats.GearBonus(player, AffectLocation.Intelligence);
        
        var wisSpellMod = _stats.AffectBonus(player, AffectLocation.Wisdom);
        var wisEquipMod = _stats.GearBonus(player, AffectLocation.Wisdom);
        
        var dexSpellMod = _stats.AffectBonus(player, AffectLocation.Dexterity);
        var dexEquipMod = _stats.GearBonus(player, AffectLocation.Dexterity);
        
        var conSpellMod = _stats.AffectBonus(player, AffectLocation.Constitution);
        var conEquipMod = _stats.GearBonus(player, AffectLocation.Constitution);
        
        var chaSpellMod = _stats.AffectBonus(player, AffectLocation.Charisma);
        var chaEquipMod = _stats.GearBonus(player, AffectLocation.Charisma);

        var strDisplay = FormatStatWithModifier(player.Strength, _stats.Get(player, AffectLocation.Strength), strEquipMod, strSpellMod);
        var intDisplay = FormatStatWithModifier(player.Intelligence, _stats.Get(player, AffectLocation.Intelligence), intEquipMod, intSpellMod);
        var wisDisplay = FormatStatWithModifier(player.Wisdom, _stats.Get(player, AffectLocation.Wisdom), wisEquipMod, wisSpellMod);
        var dexDisplay = FormatStatWithModifier(player.Dexterity, _stats.Get(player, AffectLocation.Dexterity), dexEquipMod, dexSpellMod);
        var conDisplay = FormatStatWithModifier(player.Constitution, _stats.Get(player, AffectLocation.Constitution), conEquipMod, conSpellMod);
        var chaDisplay = FormatStatWithModifier(player.Charisma, _stats.Get(player, AffectLocation.Charisma), chaEquipMod, chaSpellMod);

        output.AppendLine($"Str: {strDisplay}  Int: {intDisplay}  Wis: {wisDisplay}  Dex: {dexDisplay}  Con: {conDisplay}  Cha: {chaDisplay}");

        // Line 4: AC, Hitroll, Damroll, THAC0 - all values in red
        var baseAC = player.ArmorClass;
        var acSpellMod = _stats.AffectBonus(player, AffectLocation.ArmorClass); // Armor and ArmorClass both
        var acEquipMod = _stats.GearBonus(player, AffectLocation.ArmorClass);
        var acTotalMod = acSpellMod + acEquipMod;
        var effectiveAC = _stats.Get(player, AffectLocation.ArmorClass);
        
        var baseHitroll = player.Hitroll;
        var hitrollSpellMod = _stats.AffectBonus(player, AffectLocation.Hitroll);
        var hitrollEquipMod = _stats.GearBonus(player, AffectLocation.Hitroll);
        var hitrollTotalMod = hitrollSpellMod + hitrollEquipMod;
        var effectiveHitroll = _stats.Get(player, AffectLocation.Hitroll);
        
        var baseDamroll = player.Damroll;
        var damrollSpellMod = _stats.AffectBonus(player, AffectLocation.Damroll);
        var damrollEquipMod = _stats.GearBonus(player, AffectLocation.Damroll);
        var damrollTotalMod = damrollSpellMod + damrollEquipMod;
        var effectiveDamroll = _stats.Get(player, AffectLocation.Damroll);

        // Legacy do_stat shows get_thaco(): the class's THAC0, the same one combat uses
        var thac0 = _stats.Thac0(player);

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
        var savingPhysical = _stats.Get(player, AffectLocation.SavingPhysical);
        var savingMental = _stats.Get(player, AffectLocation.SavingMental);
        var savingMagic = _stats.Get(player, AffectLocation.SavingMagic);
        var savingPoison = _stats.Get(player, AffectLocation.SavingPoison);
        var magicResist = _stats.Get(player, AffectLocation.MagicResistance);

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
        if (player.Race == "troll")
        {
            output.AppendLine($"Magic: (innate)    #Cregeneration#N");
        }
        
        if (player.Race is "elf" or "drow" or "dwarf")
        {
            output.AppendLine($"Magic: (innate)    #Cinfravision#N");
        }

        return CommandResult.Ok(output.ToString());
    }

    /// <summary>
    /// Format a stat value with optional modifier display: the effective value (kept within the race's limits),
    /// then the base and what changed it.
    /// Examples: "[20]" or "[22 (20+2)]" or "[18 (20-2)]" or "[23 (20+3 Eq:+2 Spell:+1)]"
    /// Legacy format: stat values in cyan (#C)
    /// </summary>
    private static string FormatStatWithModifier(sbyte baseValue, int effectiveValue, int equipModifier, int spellModifier)
    {
        var totalModifier = equipModifier + spellModifier;
        if (totalModifier == 0 && effectiveValue == baseValue)
        {
            return $"[#C{baseValue}#N]";
        }

        
        // If both equipment and spell modifiers exist, show breakdown
        if (equipModifier != 0 && spellModifier != 0)
        {
            return $"[#C{effectiveValue}#N (#C{baseValue}#N Eq:{equipModifier:+0;-#} Spell:{spellModifier:+0;-#})]";
        }
        
        // Otherwise just show simple modifier
        return $"[#C{effectiveValue}#N (#C{baseValue}#N{totalModifier:+0;-#})]";
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
