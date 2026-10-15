using EliteMud.Application.Commands.Drop;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Drop;

[Command("drop")]
internal sealed class DropCommandHandler : ICommandHandler
{
    private readonly DropHandler _dropHandler;
    private readonly ActMessenger _act;

    public DropCommandHandler(
        IWorldState worldState,
        ActMessenger act)
    {
        _dropHandler = new DropHandler(worldState);
        _act = act;
    }
    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _dropHandler.Handle(context.Player, command.Argument ?? string.Empty);
        
        if (!result.Success)
        {
            await context.Session.SendLineAsync(result.Message, cancellationToken);
            return CommandOutcome.Continue;
        }

        // Handle multiple objects (drop all, drop all.item)
        if (result.Objects != null && result.Objects.Count > 0)
        {
            foreach (var obj in result.Objects)
            {
                _act.ToChar(context.Player, "you drop $p.", obj: obj);

                _act.ToNotChar(context.Player, "$n drops $p.", obj: obj);
            }
            return CommandOutcome.Continue;
        }

        // Handle single object
        _act.ToChar(context.Player, "you drop $p.", obj: result.Object);

        _act.ToNotChar(context.Player, "$n drops $p.", obj: result.Object);

        return CommandOutcome.Continue;
    }
}
