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
                // Legacy behavior: Check object Type to determine equipment slot
                // Light objects (Type="Light") go to WEAR_LIGHT slot and only need Take flag
                // Other objects go to HOLD slot and need Hold flag
                EquipmentSlot slot;
                
                if (obj.Definition.Type == "Light")
                {
                    // Light objects only need Take flag (legacy: wear_bitvectors[WEAR_LIGHT] = ITEM_TAKE)
                    if (!obj.Definition.WearSlots.Contains("Take"))
                    {
                        return CommandResult.Fail($"{obj.Definition.ShortDescription} cannot be held.");
                    }
                    slot = EquipmentSlot.Light;
                }
                else
                {
                    // Non-light objects need Hold flag
                    if (!obj.Definition.WearSlots.Contains("Hold"))
                    {
                        return CommandResult.Fail($"{obj.Definition.ShortDescription} cannot be held.");
                    }
                    slot = EquipmentSlot.Hold;
                }

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
