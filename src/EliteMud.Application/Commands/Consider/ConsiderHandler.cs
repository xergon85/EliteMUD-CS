using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Consider;

/// <summary>
/// Result of considering a target for combat.
/// </summary>
public sealed record ConsiderResult(bool Success, string Message, MobInstance? Target = null);

/// <summary>
/// Handles the 'consider' command - estimate combat difficulty against a target.
/// Legacy: do_consider() in act.informative.c:2320-2411
/// </summary>
public sealed class ConsiderHandler
{
    private readonly IWorldState _worldState;

    public ConsiderHandler(IWorldState worldState)
    {
        _worldState = worldState;
    }

    /// <summary>
    /// Consider a target for combat and return difficulty assessment.
    /// Legacy: do_consider() in act.informative.c:2320-2411
    /// </summary>
    public ConsiderResult Handle(PlayerState player, string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return new ConsiderResult(false, "Consider killing who?");
        }

        // Find target mob in room
        var room = _worldState.World.GetRoom(player.RoomId);
        var mobs = _worldState.GetMobsInRoom(room.Id);
        
        MobInstance? target = null;
        foreach (var mob in mobs)
        {
            if (mob.Definition.Name.Contains(targetName, StringComparison.OrdinalIgnoreCase))
            {
                target = mob;
                break;
            }
        }

        if (target == null)
        {
            return new ConsiderResult(false, "Consider killing who?");
        }

        // Check if target is already dead
        if (target.HitPoints <= 0)
        {
            return new ConsiderResult(true, "I think it's dead already.", target);
        }

        // Build consideration message with three comparisons:
        // 1. Armor Class comparison
        // 2. Hit Points comparison
        // 3. Overall combat rating comparison
        
        var message = BuildConsiderationMessage(player, target);
        return new ConsiderResult(true, message, target);
    }

    /// <summary>
    /// Build the full consideration message with AC, HP, and combat rating comparisons.
    /// Legacy: do_consider() lines 2347-2410
    /// </summary>
    private string BuildConsiderationMessage(PlayerState player, MobInstance target)
    {
        var lines = new List<string>();

        // 1. AC Comparison (lower AC is better, so we compare victim - player)
        // Legacy: diff = GET_AC(victim) - GET_AC(ch)
        // Note: Mobs don't have AC property, estimate from level (10 AC per level, max 100)
        int mobAC = Math.Min(100, target.Definition.Level * 10);
        int acDiff = mobAC - player.ArmorClass;
        lines.Add(GetArmorMessage(acDiff));

        // 2. HP Comparison (percentage difference)
        // Legacy: diff = (int) (100 - GET_HIT(victim)*100/GET_HIT(ch))
        // Note: Mobs don't have MaxHP, estimate from level (level * 10)
        int hpDiff = 100 - (target.HitPoints * 100 / Math.Max(1, (int)player.HitPoints));
        lines.Add(GetHealthMessage(hpDiff));

        // 3. Combat Rating Comparison
        // Legacy: diff = (combat_rating(victim) - combat_rating(ch))
        int ratingDiff = CalculateCombatRating(target) - CalculateCombatRating(player);
        lines.Add(GetDifficultyMessage(ratingDiff));

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Calculate combat rating for a character/mob.
    /// Legacy: combat_rating() in act.informative.c:2301-2317
    /// </summary>
    private int CalculateCombatRating(PlayerState player)
    {
        // For players, use level (multi-class logic not implemented yet)
        // Legacy: rating = GET_LEVEL(ch)
        int rating = player.Level;

        // Adjust by current HP percentage
        // Legacy: rating = (int)(rating * (float)GET_HIT(ch)/(float)GET_MAX_HIT(ch))
        rating = (int)(rating * (float)player.HitPoints / (float)Math.Max(1, (int)player.MaxHitPoints));

        return rating;
    }

    /// <summary>
    /// Calculate combat rating for a mob.
    /// Legacy: combat_rating() in act.informative.c:2301-2317
    /// </summary>
    private int CalculateCombatRating(MobInstance mob)
    {
        // For NPCs, use level
        // Legacy: if (IS_NPC(ch)) rating = GET_LEVEL(ch)
        int rating = mob.Definition.Level;

        // Adjust by current HP percentage
        // Note: Mobs don't have MaxHP property, estimate from level (level * 10)
        int estimatedMaxHP = mob.Definition.Level * 10;
        rating = (int)(rating * (float)mob.HitPoints / (float)Math.Max(1, estimatedMaxHP));

        return rating;
    }

    /// <summary>
    /// Get armor comparison message.
    /// Legacy: lines 2347-2361
    /// </summary>
    private string GetArmorMessage(int acDiff)
    {
        return acDiff switch
        {
            <= -140 => "Your victim is massively better protected than you.",
            <= -80 => "Your victim is well better armored than you!",
            <= -20 => "Your victim is better armored than you.",
            <= 30 => "Your victim is about evenly armored with you.",
            <= 90 => "Your victim lacks some of your protection.",
            <= 150 => "Your victim lacks much of your protection.",
            _ => "Your victim is grossly under armored compared to you."
        };
    }

    /// <summary>
    /// Get health comparison message.
    /// Legacy: lines 2364-2378
    /// </summary>
    private string GetHealthMessage(int hpDiff)
    {
        return hpDiff switch
        {
            <= -49 => "Your victim is massively healthier than you!",
            <= -29 => "Your victim is considerably healthier than you.",
            <= -9 => "Your victim is healthier than you.",
            <= 10 => "Your victim is about the same health as you.",
            <= 30 => "Your victim is not as healthy as you.",
            <= 50 => "Your victim lacks your vigor.",
            _ => "Your victim is puny in comparison."
        };
    }

    /// <summary>
    /// Get overall difficulty message based on combat rating difference.
    /// Legacy: lines 2381-2410
    /// </summary>
    private string GetDifficultyMessage(int ratingDiff)
    {
        return ratingDiff switch
        {
            <= -10 => "Now where did that chicken go?",
            <= -5 => "You could do it with a needle!",
            <= -2 => "Easy.",
            <= -1 => "Fairly easy.",
            0 => "The perfect match!",
            <= 1 => "You would need some luck!",
            <= 2 => "You would need a lot of luck!",
            <= 3 => "You would need a lot of luck and great equipment!",
            <= 5 => "Do you feel lucky, punk?",
            <= 10 => "Are you mad!?",
            <= 15 => "You ARE mad!",
            <= 20 => "Why not pretend you are dead instead?",
            <= 30 => "Your brain will be a nice decoration on the walls!",
            _ => "You are a very dumb player for even considering."
        };
    }
}
