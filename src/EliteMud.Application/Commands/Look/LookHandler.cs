using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Commands.Look;

public sealed class LookHandler
{
    private readonly IWorldState _worldState;

    public LookHandler(IWorldState worldState)
    {
        _worldState = worldState;
    }

    public RoomView Handle(PlayerState player)
    {
        var room = _worldState.World.GetRoom(player.RoomId);
        var mobs = _worldState.GetMobsInRoom(room.Id);
        var mobLines = new List<string>();
        foreach (var mob in mobs)
        {
            var line = string.IsNullOrWhiteSpace(mob.Definition.LongDescription)
                ? mob.Definition.ShortDescription
                : mob.Definition.LongDescription.TrimEnd();
            if (!string.IsNullOrWhiteSpace(line))
            {
                mobLines.Add(line);
            }
        }

        return new RoomView(room.Name, room.Description, mobLines, BuildExitLine(room));
    }

    private static string BuildExitLine(RoomDefinition room)
    {
        if (room.Exits.Count == 0)
        {
            return "Exits: none.";
        }

        var names = new List<string>(room.Exits.Count);
        foreach (var exit in room.Exits)
        {
            names.Add(exit.Direction.ToString().ToLowerInvariant());
        }

        return $"Exits: {string.Join(", ", names)}.";
    }
}
