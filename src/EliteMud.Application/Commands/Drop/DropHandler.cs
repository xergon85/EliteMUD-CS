using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Drop;

public sealed class DropHandler
{
    private readonly IWorldState _worldState;

    public DropHandler(IWorldState worldState)
    {
        _worldState = worldState;
    }

    public CommandResult Handle(PlayerState player, string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return CommandResult.Fail("Drop what?");
        }

        var inventory = _worldState.GetPlayerInventory(player);

        // Find matching object in inventory
        foreach (var obj in inventory)
        {
            if (MatchesTarget(obj.Definition, target))
            {
                // Try to drop the object
                if (_worldState.DropObject(player, obj.InstanceId))
                {
                    return CommandResult.Ok($"You drop {obj.Definition.ShortDescription}.");
                }
                else
                {
                    return CommandResult.Fail("You can't drop that.");
                }
            }
        }

        return CommandResult.Fail("You don't have that.");
    }

    private static bool MatchesTarget(ObjectDefinition obj, string target)
    {
        var targetLower = target.ToLowerInvariant();
        
        // Check if target matches any keyword in the object name
        var keywords = obj.Name?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        return keywords.Any(k => k.ToLowerInvariant().StartsWith(targetLower));
    }
}
