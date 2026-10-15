using EliteMud.Application.Commands.Remove;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Remove;

[Command("remove", Aliases = new[] { "remove", "rem" })]
internal sealed class RemoveCommandHandler : ICommandHandler
{
    private readonly RemoveHandler _removeHandler;
    private readonly ActMessenger _act;

    public RemoveCommandHandler(
        IWorldState worldState,
        ActMessenger act)
    {
        _removeHandler = new RemoveHandler(worldState);
        _act = act;
    }

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _removeHandler.Handle(context.Player, command.Argument ?? string.Empty);

        // Handle multiple objects (remove all, remove all.item)
        if (result.Objects != null && result.Objects.Count > 0)
        {
            foreach (var obj in result.Objects)
            {
                context.SendEquipMessage(_act, "You stop using $p.", "$n stops using $p.", obj);
            }
            return CommandOutcome.Continue;
        }

        // Handle single object
        if (result.Object is not null)
        {
            context.SendEquipMessage(_act, "You stop using $p.", "$n stops using $p.", result.Object);
            return CommandOutcome.Continue;
        }

        // Handle error message
        if (!string.IsNullOrEmpty(result.Message))
        {
            await context.Session.SendLineAsync(result.Message, cancellationToken);
        }

        return CommandOutcome.Continue;
    }
}
