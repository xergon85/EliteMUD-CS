using EliteMud.Application.Commands.Say;
using EliteMud.Application.Commands.Shared;
using EliteMud.Game;
using EliteMud.Scripting;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Say;

[Command("say")]
internal sealed class SayCommandHandler : ICommandHandler
{
    private readonly IScriptEngine _scriptEngine;
    private readonly Func<IEnumerable<ConnectionContext>> _connections;
    private readonly SayHandler _sayHandler;

    public SayCommandHandler(IScriptEngine scriptEngine, Func<IEnumerable<ConnectionContext>> connections)
    {
        _scriptEngine = scriptEngine;
        _connections = connections;
        _sayHandler = new SayHandler();
    }

    public CommandKind Kind => CommandKind.Say;

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _sayHandler.Handle(context.Player, command.Argument ?? string.Empty);
        await context.Session.SendLineAsync(result.Message, cancellationToken);
        if (!result.Success || string.IsNullOrWhiteSpace(result.BroadcastMessage))
        {
            return CommandOutcome.Continue;
        }

        // Create dummy room for script context (Say doesn't need full room details)
        var dummyRoom = new RoomDefinition(
            context.Player.RoomId,
            string.Empty,
            string.Empty,
            Array.Empty<ExitDefinition>());
        
        await context.ExecuteScriptHookAsync(_scriptEngine, ScriptHook.OnSay, dummyRoom, command.Argument, cancellationToken);
        await BroadcastRoomAsync(context, result.BroadcastMessage, cancellationToken);
        return CommandOutcome.Continue;
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
