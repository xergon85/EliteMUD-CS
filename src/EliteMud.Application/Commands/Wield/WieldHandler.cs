using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Wield;

public sealed class WieldHandler
{
    private readonly IWorldState _worldState;

    public WieldHandler(IWorldState worldState)
    {
        _worldState = worldState;
    }

    public CommandResult Handle(PlayerState player, string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return CommandResult.Fail("Wield what?");
        }

        var inventory = _worldState.GetPlayerInventory(player);

        // Find matching object in inventory
        foreach (var obj in inventory)
        {
            if (MatchesTarget(obj.Definition, target))
            {
                // Check if object can be wielded
                if (!obj.Definition.WearSlots.Contains("Wield") && 
                    !obj.Definition.WearSlots.Contains("WieldTwoHanded") &&
                    !obj.Definition.WearSlots.Contains("BothHands"))
                {
                    return CommandResult.Fail($"{obj.Definition.ShortDescription} cannot be wielded.");
                }

                // Determine wield slot (prefer Wield over two-handed)
                var slot = obj.Definition.WearSlots.Contains("Wield") 
                    ? EquipmentSlot.Wield 
                    : EquipmentSlot.BothHands;

                // Try to equip the object
                if (_worldState.EquipObject(player, obj.InstanceId, slot))
                {
                    return CommandResult.Ok($"You wield {obj.Definition.ShortDescription}.");
                }
                else
                {
                    return CommandResult.Fail("You are already wielding something.");
                }
            }
        }

        return CommandResult.Fail("You don't have that.");
    }

    private static bool MatchesTarget(ObjectDefinition obj, string target)
    {
        var targetLower = target.ToLowerInvariant();
        var keywords = obj.Name?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        return keywords.Any(k => k.ToLowerInvariant().StartsWith(targetLower));
    }
}
