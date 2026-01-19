using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using System.Text;

namespace EliteMud.Application.Commands.Score;

public sealed class ScoreHandler
{
    public CommandResult Handle(PlayerState player)
    {
        var output = new StringBuilder();
        
        output.AppendLine($"You are {player.Name}{(player.Title != null ? " " + player.Title : "")}");
        output.AppendLine($"Level {player.Level} {player.Race} {player.CharacterClass}");
        output.AppendLine();
        
        // Stats
        output.AppendLine("Attributes:");
        output.AppendLine($"  Str: {FormatStat(player.Strength, player.StrengthAdd)}  Int: {player.Intelligence}  Wis: {player.Wisdom}");
        output.AppendLine($"  Dex: {player.Dexterity}  Con: {player.Constitution}  Cha: {player.Charisma}");
        output.AppendLine();
        
        // Vitals
        output.AppendLine("Vitals:");
        output.AppendLine($"  HP:   {player.HitPoints}/{player.MaxHitPoints}");
        output.AppendLine($"  Mana: {player.Mana}/{player.MaxMana}");
        output.AppendLine($"  Move: {player.Movement}/{player.MaxMovement}");
        output.AppendLine();
        
        // Combat stats
        output.AppendLine("Combat:");
        output.AppendLine($"  AC:      {player.ArmorClass}");
        output.AppendLine($"  Hitroll: {FormatBonus(player.Hitroll)}");
        output.AppendLine($"  Damroll: {FormatBonus(player.Damroll)}");
        output.AppendLine($"  Align:   {FormatAlignment(player.Alignment)}");
        output.AppendLine();
        
        // Resources
        output.AppendLine("Resources:");
        output.AppendLine($"  Gold:  {player.Gold}");
        output.AppendLine($"  Bank:  {player.BankGold}");
        output.AppendLine($"  Exp:   {player.Experience}");
        
        return CommandResult.Ok(output.ToString());
    }
    
    private static string FormatStat(sbyte str, sbyte strAdd)
    {
        if (str == 18 && strAdd > 0)
        {
            if (strAdd == 100)
                return "18/00";
            return $"18/{strAdd:D2}";
        }
        return str.ToString();
    }
    
    private static string FormatBonus(sbyte value)
    {
        if (value >= 0)
            return $"+{value}";
        return value.ToString();
    }
    
    private static string FormatAlignment(int alignment)
    {
        return alignment switch
        {
            < -350 => "Evil",
            < -100 => "Somewhat Evil",
            < 100 => "Neutral",
            < 350 => "Somewhat Good",
            _ => "Good"
        };
    }
}
