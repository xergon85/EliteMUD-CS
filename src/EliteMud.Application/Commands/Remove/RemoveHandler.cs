using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Remove;

public sealed class RemoveHandler
{
    private readonly IWorldState _worldState;

    public RemoveHandler(IWorldState worldState)
    {
        _worldState = worldState;
    }

    public CommandResult Handle(PlayerState player, string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return CommandResult.Fail("Remove what?");
        }

        var equipment = _worldState.GetPlayerEquipment(player);

        // Find matching object in equipment
        foreach (var (slot, obj) in equipment)
        {
            if (MatchesTarget(obj.Definition, target))
            {
                // Try to unequip the object
                if (_worldState.UnequipObject(player, slot))
                {
                    return CommandResult.Ok($"You remove {obj.Definition.ShortDescription}.");
                }
                else
                {
                    return CommandResult.Fail("You can't remove that.");
                }
            }
        }

        return CommandResult.Fail("You're not wearing that.");
    }

    private static bool MatchesTarget(ObjectDefinition obj, string target)
    {
        var targetLower = target.ToLowerInvariant();
        var keywords = obj.Name?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        return keywords.Any(k => k.ToLowerInvariant().StartsWith(targetLower));
    }
}
