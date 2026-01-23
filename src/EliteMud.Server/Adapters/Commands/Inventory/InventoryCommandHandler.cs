using EliteMud.Application.Commands.Inventory;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Inventory;

[Command("inventory", Aliases = new[] { "inv", "i" })]
internal sealed class InventoryCommandHandler : ICommandHandler
{
    private readonly InventoryHandler _inventoryHandler;

    public InventoryCommandHandler(IWorldState worldState)
    {
        _inventoryHandler = new InventoryHandler(worldState);
    }
    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _inventoryHandler.Handle(context.Player);
        foreach (var line in result.Items)
        {
            await context.Session.SendLineAsync(line, cancellationToken);
        }
        return CommandOutcome.Continue;
    }
}
