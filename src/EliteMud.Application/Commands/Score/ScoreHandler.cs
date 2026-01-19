using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using System.Text;

namespace EliteMud.Application.Commands.Score;

public sealed class ScoreHandler
{
    public CommandResult Handle(PlayerState player)
    {
        var output = new StringBuilder();
        
        // Line 1: Name, title, level
        var title = player.Title ?? "the newbie";
        output.AppendLine($"You are #B{player.Name}#N#C {title}#N (#Rlevel {player.Level}#N).");
        
        // Line 2: Age and race
        // TODO: Calculate actual age from Birth field (not yet implemented)
        var age = 17; // Default starting age - will be replaced with actual calculation
        output.AppendLine($"You are a {age} year old {player.Race}.");
        
        // TODO: Birthday check - requires Birth field
        // if ((age(ch).month == 0) && (age(ch).day == 0))
        //   output.AppendLine("  #bIt's your birthday today.#N");
        
        // TODO: Deity worship - requires Deity field (not yet implemented)
        // if (player.Deity != null)
        //   output.AppendLine($"You are the devout worshipper of {player.Deity}.");
        
        // Line 3: HP, Mana, Movement
        output.AppendLine($"You have #R{player.HitPoints}#N(#G{player.MaxHitPoints}#N) hp, #C{player.Mana}#N(#B{player.MaxMana}#N) mana and #Y{player.Movement}#N(#G{player.MaxMovement}#N) movement points.");
        
        // Line 4: Alignment
        var alignDesc = GetAlignmentDescription(player.Alignment);
        output.AppendLine($"You are {alignDesc}.");
        
        // Line 5: Experience and gold
        output.AppendLine($"You have scored #G{player.Experience}#N exp, and have #B{player.Gold}#N gold coins.");
        
        // Line 6: XP needed for next level
        // TODO: Calculate exp_needed based on level table (for now, placeholder)
        var expNeeded = player.Level * 1000; // Simplified calculation
        var expRemaining = expNeeded - player.Experience;
        if (player.Level < 100) // Assume 100 is max level
        {
            output.AppendLine($"You need #R{expRemaining}#N exp to reach your next level.");
        }
        
        // Line 7: Time played
        // TODO: Calculate from LastLogon and TimePlayed fields (not yet implemented)
        var daysPlayed = 0;
        var hoursPlayed = 0;
        output.AppendLine($"You have been playing for {daysPlayed} days and {hoursPlayed} hours.");
        
        // TODO: Quest points - requires quest system (not yet implemented)
        // if (player.QuestPoints > 0)
        //   output.AppendLine($"You have #m{player.QuestPoints}#N quest points.");
        
        // Line 8: Carrying weight
        // TODO: Calculate actual weight from inventory objects (requires WorldState access)
        var itemCount = player.InventoryObjectIds.Count;
        var totalWeight = 0; // Placeholder
        if (itemCount > 0)
        {
            var itemPlural = itemCount > 1 ? "s" : "";
            output.AppendLine($"You are carrying {itemCount} item{itemPlural} with the total weight of {totalWeight} pounds.");
        }
        else
        {
            output.AppendLine("You are not carrying anything.");
        }
        
        // Line 9: Inventory and equipped counts
        var inventoryCount = player.InventoryObjectIds.Count;
        var equippedCount = player.EquipmentSlotToObjectId.Count;
        
        if (inventoryCount > 0)
        {
            var inventoryVerb = inventoryCount > 1 ? "are" : "is";
            var inventoryPlural = inventoryCount > 1 ? "s" : "";
            output.Append($"There {inventoryVerb} {inventoryCount} item{inventoryPlural} in your inventory and ");
        }
        else
        {
            output.Append("You have nothing in your inventory and ");
        }
        
        if (equippedCount > 0)
        {
            var equippedPlural = equippedCount > 1 ? "s" : "";
            output.AppendLine($"{equippedCount} item{equippedPlural} equipped.");
        }
        else
        {
            output.AppendLine("no items equipped.");
        }
        
        // Line 10+: Position/status
        // TODO: Use actual Position field when implemented
        // For now, default to standing
        output.AppendLine("#BYou are standing.#N");
        
        // TODO: Conditions (hunger/thirst/drunk) - requires Conditions system
        // if (player.Drunk > 10)
        //   output.AppendLine("#RYou are intoxicated.#N");
        // if (player.Hunger == 0)
        //   output.AppendLine("#YYou are hungry.#N");
        // if (player.Thirst == 0)
        //   output.AppendLine("#YYou are thirsty.#N");
        
        // TODO: Spell effects/affects - requires Affects system
        // if (IS_AFFECTED(player, AFF_INVISIBLE))
        //   output.AppendLine("#BYou are invisible.#N");
        // if (IS_AFFECTED(player, AFF_SANCTUARY))
        //   output.AppendLine("#GYou are protected by Sanctuary.#N");
        // etc...
        
        // TODO: PKOK flag - requires player flags system
        // if (player.IsPkok)
        //   output.AppendLine("#rYou are a player killer!#N (PKOK)");
        
        return CommandResult.Ok(output.ToString());
    }
    
    private static string GetAlignmentDescription(int alignment)
    {
        // Match legacy alignment descriptions from act.informative.c:1096-1115
        return alignment switch
        {
            <= -900 => "#rso evil your horns are beginning to show#N",
            <= -500 => "#Rvery evil he he#N",
            <= -350 => "#Revil#N",
            <= -150 => "#Bneutral with an #Revil #Btendency#N",
            < 150 => "#Bneutral#N",
            < 350 => "#Bneutral with a touch of #Ggoodness#N",
            < 500 => "#wgood#N",
            < 900 => "#wso very much good#N",
            _ => "#wso good that you have developed a halo#N"
        };
    }
}
