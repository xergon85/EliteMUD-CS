using EliteMud.Application.Commands.Move;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Scripting;
using EliteMud.Server.Adapters.Commands.Look;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Move;

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

    public CommandKind Kind => CommandKind.Move;

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

        await ExecuteHookAsync(context, ScriptHook.OnEnterRoom, null, cancellationToken);
        await _lookHandler.HandleAsync(new CommandRequest(CommandKind.Look, null, null), context, cancellationToken);
        return CommandOutcome.Continue;
    }

    private async ValueTask ExecuteHookAsync(
        ConnectionContext context,
        ScriptHook hook,
        string? text,
        CancellationToken cancellationToken)
    {
        var room = _worldState.World.GetRoom(context.Player.RoomId);
        var scriptContext = new ScriptContext(context.Player, room, text);
        await _scriptEngine.ExecuteAsync(hook, scriptContext, cancellationToken);
        foreach (var output in scriptContext.Outputs)
        {
            await context.Session.SendLineAsync(output, cancellationToken);
        }
    }
}
