using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Flee;

internal sealed class FleeCommandHandler : ICommandHandler
{
    private readonly IWorldState _worldState;
    private readonly Func<IEnumerable<ConnectionContext>> _connections;

    public FleeCommandHandler(
        IWorldState worldState,
        Func<IEnumerable<ConnectionContext>> connections)
    {
        _worldState = worldState;
        _connections = connections;
    }

    public CommandKind Kind => CommandKind.Flee;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var player = context.Player;

        // Check if in bad position (legacy: act.offensive.c:388)
        if (player.Position < CombatService.POS_FIGHTING)
        {
            await context.Session.SendLineAsync(
                "You are in pretty bad shape, unable to flee!",
                cancellationToken);
            return CommandOutcome.Continue;
        }

        // Get current room for stopping combat later
        var currentRoomId = player.RoomId;

        // Try to flee in 6 random directions (legacy: act.offensive.c:431)
        var random = new Random();
        var directions = new[] {
            Direction.North, Direction.East, Direction.South,
            Direction.West, Direction.Up, Direction.Down
        };

        for (int i = 0; i < 6; i++)
        {
            var attemptDirection = directions[random.Next(directions.Length)];

            // Check if can move in that direction
            if (!_worldState.World.TryMove(player.RoomId, attemptDirection, out var targetRoomId))
            {
                continue;
            }

            // Broadcast flee attempt to room (legacy: act.offensive.c:443)
            await BroadcastToRoomExceptAsync(
                context,
                $"{player.Name} panics, and attempts to flee.",
                cancellationToken);

            // Calculate experience loss if fighting (legacy: act.offensive.c:425-427)
            int experienceLoss = 0;
            if (player.FightingConnectionId != null)
            {
                // Get victim to calculate loss
                var victimId = player.FightingConnectionId.Value;
                if (victimId > 0)
                {
                    // Fighting a player
                    var victimContext = _connections()
                        .FirstOrDefault(c => c.Id == victimId);
                    if (victimContext != null)
                    {
                        var victim = victimContext.Player;
                        int damageDone = victim.MaxHitPoints - victim.HitPoints;
                        experienceLoss = damageDone * victim.Level;
                    }
                }
                else
                {
                    // Fighting a mob
                    var mobInstanceId = -victimId;
                    var mob = _worldState.GetMobsInRoom(currentRoomId)
                        .FirstOrDefault(m => m.InstanceId == mobInstanceId);
                    if (mob != null)
                    {
                        int mobMaxHp = Math.Max(mob.HitPoints, mob.Definition.Level * 10);
                        int damageDone = mobMaxHp - mob.HitPoints;
                        experienceLoss = damageDone * mob.Definition.Level;
                    }
                }
            }

            // Actually move to the new room
            player.RoomId = targetRoomId;

            // Stop fighting
            CombatService.StopFighting(player);

            // Apply experience loss (legacy: act.offensive.c:453)
            if (experienceLoss > 0)
            {
                player.Experience -= experienceLoss;
                if (player.Experience < 0) player.Experience = 0;
            }

            // Send success message (legacy: act.offensive.c:454)
            await context.Session.SendLineAsync("You flee head over heels.", cancellationToken);

            // Stop all mobs/players that were fighting us in the old room (legacy: act.offensive.c:462-466)
            var mobsInOldRoom = _worldState.GetMobsInRoom(currentRoomId);
            foreach (var mob in mobsInOldRoom)
            {
                if (mob.FightingConnectionId == context.Id)
                {
                    mob.FightingConnectionId = null;
                    mob.Position = CombatService.POS_STANDING;
                }
            }

            var playersInOldRoom = _connections()
                .Where(c => c.Player.RoomId == currentRoomId && c.Player.FightingConnectionId == context.Id);
            foreach (var otherPlayer in playersInOldRoom)
            {
                CombatService.StopFighting(otherPlayer.Player);
            }

            // Show new room by looking
            await ShowRoomAsync(context, cancellationToken);

            return CommandOutcome.Continue;
        }

        // No valid exits found (legacy: act.offensive.c:421)
        await context.Session.SendLineAsync("PANIC!  You couldn't escape!", cancellationToken);
        return CommandOutcome.Continue;
    }

    private async ValueTask BroadcastToRoomExceptAsync(
        ConnectionContext speaker,
        string message,
        CancellationToken cancellationToken)
    {
        foreach (var connection in _connections())
        {
            if (connection.Id == speaker.Id)
            {
                continue;
            }

            if (connection.Player.RoomId != speaker.Player.RoomId)
            {
                continue;
            }

            await connection.Session.SendLineAsync(message, cancellationToken);
        }
    }

    private async ValueTask ShowRoomAsync(
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var room = _worldState.World.GetRoom(context.Player.RoomId);
        if (room == null)
        {
            return;
        }

        // Send room name
        await context.Session.SendLineAsync(room.Name, cancellationToken);

        // Send room description
        await context.Session.SendLineAsync(room.Description, cancellationToken);

        // Send exits
        var exits = room.Exits.Select(e => e.Direction.ToString()).ToList();
        if (exits.Count > 0)
        {
            await context.Session.SendLineAsync($"Obvious exits: {string.Join(", ", exits)}", cancellationToken);
        }
    }
}
