using EliteMud.Application.Commands.Move;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Scripting;
using EliteMud.Server.Adapters.Commands.Look;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Move;

[Command("move", Aliases = new[] { "north", "n", "east", "e", "south", "s", "west", "w", "up", "u", "down", "d", "go" })]
internal sealed class MoveCommandHandler : ICommandHandler
{
    private readonly IWorldState _worldState;
    private readonly IScriptEngine _scriptEngine;
    private readonly LookCommandHandler _lookHandler;
    private readonly MoveHandler _moveHandler;

    public MoveCommandHandler(
        IWorldState worldState,
        IScriptEngine scriptEngine,
        LookCommandHandler lookHandler)
    {
        _worldState = worldState;
        _scriptEngine = scriptEngine;
        _lookHandler = lookHandler;
        _moveHandler = new MoveHandler(worldState);
    }
    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        if (!command.Direction.HasValue)
        {
            return CommandOutcome.Unknown;
        }

        var result = _moveHandler.Handle(context.Player, command.Direction.Value);
        if (!result.Moved)
        {
            await context.Session.SendLineAsync(result.Message ?? "You cannot go that way.", cancellationToken);
            return CommandOutcome.Continue;
        }

        var room = _worldState.World.GetRoom(context.Player.RoomId);
        await context.ExecuteScriptHookAsync(_scriptEngine, ScriptHook.OnEnterRoom, room, null, cancellationToken);
        await _lookHandler.HandleAsync(new CommandRequest("look", null, null), context, cancellationToken);
        return CommandOutcome.Continue;
    }
}
