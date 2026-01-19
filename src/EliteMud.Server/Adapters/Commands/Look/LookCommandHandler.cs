using EliteMud.Application.Commands.Look;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Scripting;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Look;

internal sealed class LookCommandHandler : ICommandHandler
{
    private readonly IWorldState _worldState;
    private readonly IScriptEngine _scriptEngine;
    private readonly LookHandler _lookHandler;

    public LookCommandHandler(IWorldState worldState, IScriptEngine scriptEngine)
    {
        _worldState = worldState;
        _scriptEngine = scriptEngine;
        _lookHandler = new LookHandler(worldState);
    }

    public CommandKind Kind => CommandKind.Look;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var view = _lookHandler.Handle(context.Player);
        await context.Session.SendLineAsync(view.Name, cancellationToken);
        await context.Session.SendLineAsync(view.Description, cancellationToken);
        foreach (var line in view.MobLines)
        {
            await context.Session.SendLineAsync(line, cancellationToken);
        }

        foreach (var line in view.ObjectLines)
        {
            await context.Session.SendLineAsync(line, cancellationToken);
        }

        await context.Session.SendLineAsync(view.ExitLine, cancellationToken);
        await ExecuteHookAsync(context, ScriptHook.OnLook, null, cancellationToken);
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
