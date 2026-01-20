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
    private readonly ConnectionRegistry _connectionRegistry;

    public LookCommandHandler(IWorldState worldState, IScriptEngine scriptEngine, ConnectionRegistry connectionRegistry)
    {
        _worldState = worldState;
        _scriptEngine = scriptEngine;
        _connectionRegistry = connectionRegistry;
        _lookHandler = new LookHandler(worldState, () => _connectionRegistry.GetConnections().Select(c => c.Player));
    }

    public CommandKind Kind => CommandKind.Look;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        // If a target is specified, examine it (look <object>)
        if (!string.IsNullOrWhiteSpace(command.Argument))
        {
            var result = _lookHandler.HandleLookAt(context.Player, command.Argument);
            await context.Session.SendLineAsync(result.Message, cancellationToken);
            return CommandOutcome.Continue;
        }

        // Otherwise, show the room
        var view = _lookHandler.Handle(context.Player);
        
        // Room name with color
        await context.Session.SendLineAsync($"#C{view.Name}#N", cancellationToken);
        
        // Room description - trim leading newline/whitespace and send as-is
        // (it already contains embedded newlines, so we use SendAsync to avoid adding extra)
        var description = view.Description.TrimStart('\n', '\r').TrimStart();
        await context.Session.SendAsync(description, cancellationToken);
        
        // Ensure description ends with newline before showing objects/mobs/exits
        if (!description.EndsWith('\n'))
        {
            await context.Session.SendAsync("\r\n", cancellationToken);
        }
        
        // Objects (green color)
        foreach (var line in view.ObjectLines)
        {
            await context.Session.SendLineAsync(line, cancellationToken);
        }
        
        // NPCs (yellow color)
        foreach (var line in view.MobLines)
        {
            await context.Session.SendLineAsync(line, cancellationToken);
        }

        // Other players (cyan color)
        foreach (var line in view.PlayerLines)
        {
            await context.Session.SendLineAsync(line, cancellationToken);
        }

        // Exits line
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
