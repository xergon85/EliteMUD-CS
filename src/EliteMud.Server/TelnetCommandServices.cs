using EliteMud.Application;
using EliteMud.Game;
using EliteMud.Scripting;

namespace EliteMud.Server;

internal sealed class TelnetCommandServices : IConnectionDirectory
{
    private readonly IWorldState _worldState;
    private readonly IScriptEngine _scriptEngine;
    private readonly CommandCatalog _catalog;
    private readonly Func<IEnumerable<ConnectionContext>> _connections;
    private readonly LookHandler _lookHandler;
    private readonly MoveHandler _moveHandler;
    private readonly ResetZoneHandler _resetZoneHandler;
    private readonly SayHandler _sayHandler;
    private readonly WhoHandler _whoHandler;

    public TelnetCommandServices(
        IWorldState worldState,
        IScriptEngine scriptEngine,
        CommandCatalog catalog,
        Func<IEnumerable<ConnectionContext>> connections)
    {
        _worldState = worldState;
        _scriptEngine = scriptEngine;
        _catalog = catalog;
        _connections = connections;
        _lookHandler = new LookHandler(worldState);
        _moveHandler = new MoveHandler(worldState);
        _resetZoneHandler = new ResetZoneHandler(worldState);
        _sayHandler = new SayHandler();
        _whoHandler = new WhoHandler(this);
    }

    public IReadOnlyList<string> GetPlayerNames()
    {
        return _connections()
            .Select(connection => connection.Player.Name)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async ValueTask ShowWhoAsync(ConnectionContext context, CancellationToken cancellationToken)
    {
        var result = _whoHandler.Handle();
        await context.Session.SendLineAsync("Players:", cancellationToken);
        foreach (var name in result.Names)
        {
            await context.Session.SendLineAsync($" - {name}", cancellationToken);
        }
    }

    public async ValueTask ResetZoneAsync(ConnectionContext context, string? idText,
        CancellationToken cancellationToken)
    {
        int? zoneId = null;
        if (!string.IsNullOrWhiteSpace(idText))
        {
            if (!int.TryParse(idText, out var parsedId))
            {
                await context.Session.SendLineAsync(_catalog.GetResetUsage(), cancellationToken);
                return;
            }

            zoneId = parsedId;
        }

        var result = _resetZoneHandler.Handle(context.Player, zoneId);
        await context.Session.SendLineAsync(result.Message, cancellationToken);
        if (result.Success)
        {
            await RenderRoomAsync(context, cancellationToken);
        }
    }

    public async ValueTask MoveAsync(ConnectionContext context, Direction direction,
        CancellationToken cancellationToken)
    {
        var result = _moveHandler.Handle(context.Player, direction);
        if (!result.Moved)
        {
            await context.Session.SendLineAsync(result.Message ?? "You cannot go that way.", cancellationToken);
            return;
        }

        await ExecuteHookAsync(context, ScriptHook.OnEnterRoom, null, cancellationToken);
        await RenderRoomAsync(context, cancellationToken);
    }

    public async ValueTask SayAsync(ConnectionContext context, string message, CancellationToken cancellationToken)
    {
        var result = _sayHandler.Handle(context.Player, message);
        await context.Session.SendLineAsync(result.Message, cancellationToken);
        if (!result.Success || string.IsNullOrWhiteSpace(result.BroadcastMessage))
        {
            return;
        }

        await ExecuteHookAsync(context, ScriptHook.OnSay, message, cancellationToken);
        await BroadcastRoomAsync(context, result.BroadcastMessage, cancellationToken);
    }

    public async ValueTask RenderRoomAsync(ConnectionContext context, CancellationToken cancellationToken)
    {
        var view = _lookHandler.Handle(context.Player);
        await context.Session.SendLineAsync(view.Name, cancellationToken);
        await context.Session.SendLineAsync(view.Description, cancellationToken);
        foreach (var line in view.MobLines)
        {
            await context.Session.SendLineAsync(line, cancellationToken);
        }

        await context.Session.SendLineAsync(view.ExitLine, cancellationToken);
        await ExecuteHookAsync(context, ScriptHook.OnLook, null, cancellationToken);
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

    private async ValueTask BroadcastRoomAsync(ConnectionContext speaker, string message,
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
}
