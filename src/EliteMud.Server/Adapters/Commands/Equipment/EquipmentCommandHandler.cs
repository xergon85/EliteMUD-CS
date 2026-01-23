using EliteMud.Application.Commands.Equipment;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Equipment;

[Command("equipment", Aliases = new[] { "eq" })]
internal sealed class EquipmentCommandHandler : ICommandHandler
{
    private readonly EquipmentHandler _equipmentHandler;

    public EquipmentCommandHandler(IWorldState worldState)
    {
        _equipmentHandler = new EquipmentHandler(worldState);
    }
    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var result = _equipmentHandler.Handle(context.Player);
        foreach (var line in result.Lines)
        {
            await context.Session.SendLineAsync(line, cancellationToken);
        }
        return CommandOutcome.Continue;
    }
}
