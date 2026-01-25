using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Look;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Recall;

[Command("recall")]
internal sealed class RecallCommandHandler : ICommandHandler
{
    private readonly IWorldState _worldState;
    private readonly LookCommandHandler _lookHandler;
    private readonly ConnectionRegistry _connectionRegistry;
    
    // Legacy: mortal_start_room = 3001 (Temple of Midgaard)
    // Legacy: max_recall_level = 10 (level 10 and below can recall)
    private const int RecallRoomId = 3001;
    private const int MaxRecallLevel = 10;

    public RecallCommandHandler(
        IWorldState worldState,
        LookCommandHandler lookHandler,
        ConnectionRegistry connectionRegistry)
    {
        _worldState = worldState;
        _lookHandler = lookHandler;
        _connectionRegistry = connectionRegistry;
    }

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var player = context.Player;
        
        // Check level restriction (legacy: max_recall_level = 10)
        // Only works for non-remorted characters level 10 and below
        if (player.Level > MaxRecallLevel)
        {
            await context.Session.SendLineAsync(
                "You have reached a level where the gods no longer protect you!",
                cancellationToken);
            return CommandOutcome.Continue;
        }
        
        // Check room flags - can't recall from GODROOM or ARENA
        var currentRoom = _worldState.World.GetRoom(player.RoomId);
        if (currentRoom.Flags.HasFlag(RoomFlags.GodRoom) || 
            currentRoom.Flags.HasFlag(RoomFlags.Arena))
        {
            await context.Session.SendLineAsync(
                "You can't recall from here!",
                cancellationToken);
            return CommandOutcome.Continue;
        }
        
        // Check if player is fighting
        if (player.FightingConnectionId != null)
        {
            await context.Session.SendLineAsync(
                "You can't recall while fighting!",
                cancellationToken);
            return CommandOutcome.Continue;
        }
        
        // Messages to room before leaving
        await context.BroadcastToRoomAsync(
            _connectionRegistry,
            $"{player.Name} seeks deep into prayer.",
            cancellationToken);
        
        await context.BroadcastToRoomAsync(
            _connectionRegistry,
            $"The gods reach down and escort {player.Name} to safety!",
            cancellationToken);
        
        // Validate recall room exists (safety check)
        if (!_worldState.World.Rooms.ContainsKey(RecallRoomId))
        {
            await context.Session.SendLineAsync(
                "The gods seem to be unavailable. Please contact an immortal.",
                cancellationToken);
            return CommandOutcome.Continue;
        }
        
        // Move player to recall room
        player.RoomId = RecallRoomId;
        
        // If player was fighting, reset position to standing
        if (player.Position == Position.Fighting)
        {
            player.Position = Position.Standing;
        }
        
        // Messages to player
        await context.Session.SendLineAsync(
            "You sink deep into prayer.",
            cancellationToken);
        await context.Session.SendLineAsync(
            "The gods reach down and escort you to safety!",
            cancellationToken);
        
        // Show new room (legacy: calls do_look)
        await _lookHandler.HandleAsync(
            new CommandRequest("look", null, null),
            context,
            cancellationToken);
        
        // Message to new room
        await context.BroadcastToRoomAsync(
            _connectionRegistry,
            $"The gods drop {player.Name} in the middle of the room!",
            cancellationToken);
        
        // Warning at max level
        if (player.Level == MaxRecallLevel)
        {
            await context.Session.SendLineAsync(
                "Beware!  Soon the gods will not watch over you.  Buy recall scrolls!",
                cancellationToken);
        }
        
        return CommandOutcome.Continue;
    }
}
