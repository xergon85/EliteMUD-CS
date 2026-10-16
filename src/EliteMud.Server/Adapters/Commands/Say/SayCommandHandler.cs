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
    private readonly ActMessenger _act;
    private readonly SayHandler _sayHandler;

    public SayCommandHandler(IScriptEngine scriptEngine, ActMessenger act)
    {
        _scriptEngine = scriptEngine;
        _act = act;
        _sayHandler = new SayHandler();
    }
    
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
        _act.BroadcastToRoom(context.Player, result.BroadcastMessage);
        return CommandOutcome.Continue;
    }
}
