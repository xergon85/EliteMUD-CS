using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Load;

public sealed class LoadHandler
{
    private readonly IWorldState _worldState;

    public LoadHandler(IWorldState worldState)
    {
        _worldState = worldState;
    }

    public CommandResult Handle(PlayerState player, string objectIdStr)
    {
        if (string.IsNullOrWhiteSpace(objectIdStr))
        {
            return CommandResult.Fail("Load what? Usage: load <object_id>");
        }

        if (!int.TryParse(objectIdStr, out var objectId))
        {
            return CommandResult.Fail("Invalid object ID. Usage: load <object_id>");
        }

        var obj = _worldState.LoadObjectToPlayer(player, objectId);
        if (obj is null)
        {
            return CommandResult.Fail($"Object {objectId} not found.");
        }

        return CommandResult.Ok($"Loaded {obj.Definition.ShortDescription} (ID: {objectId}).");
    }
}
