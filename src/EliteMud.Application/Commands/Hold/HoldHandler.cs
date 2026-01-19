using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Hold;

public sealed class HoldHandler
{
    private readonly IWorldState _worldState;

    public HoldHandler(IWorldState worldState)
    {
        _worldState = worldState;
    }

    public CommandResult Handle(PlayerState player, string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return CommandResult.Fail("Hold what?");
        }

        var inventory = _worldState.GetPlayerInventory(player);

        // Find matching object in inventory
        foreach (var obj in inventory)
        {
            if (MatchesTarget(obj.Definition, target))
            {
                // Check if object can be held
                if (!obj.Definition.WearSlots.Contains("Hold") && !obj.Definition.WearSlots.Contains("Light"))
                {
                    return CommandResult.Fail($"{obj.Definition.ShortDescription} cannot be held.");
                }

                // Determine hold slot (prefer Hold over Light for shields, Light for light sources)
                var slot = obj.Definition.WearSlots.Contains("Light") 
                    ? EquipmentSlot.Light 
                    : EquipmentSlot.Hold;

                // Try to equip the object
                if (_worldState.EquipObject(player, obj.InstanceId, slot))
                {
                    return CommandResult.Ok($"You hold {obj.Definition.ShortDescription}.");
                }
                else
                {
                    var slotName = slot == EquipmentSlot.Light ? "light source" : "held item";
                    return CommandResult.Fail($"You are already holding a {slotName}.");
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
