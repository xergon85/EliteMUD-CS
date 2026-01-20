using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Get;

public sealed record GetResult(bool Success, string Message, ObjectDefinition? Object = null);

public sealed class GetHandler
{
    private readonly IWorldState _worldState;

    public GetHandler(IWorldState worldState)
    {
        _worldState = worldState;
    }

    public GetResult Handle(PlayerState player, string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return new GetResult(false, "Get what?");
        }

        var room = _worldState.World.GetRoom(player.RoomId);
        var objects = _worldState.GetObjectsInRoom(room.Id);

        // Find matching object
        foreach (var obj in objects)
        {
            if (MatchesTarget(obj.Definition, target))
            {
                // Try to take the object
                if (_worldState.TakeObject(player, obj.InstanceId))
                {
                    return new GetResult(true, string.Empty, obj.Definition);
                }
                else
                {
                    return new GetResult(false, "You can't take that.");
                }
            }
        }

        return new GetResult(false, "You don't see that here.");
    }

    private static bool MatchesTarget(ObjectDefinition obj, string target)
    {
        var targetLower = target.ToLowerInvariant();
        
        // Check if target matches any keyword in the object name
        var keywords = obj.Name?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        return keywords.Any(k => k.ToLowerInvariant().StartsWith(targetLower));
    }
}
