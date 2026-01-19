using EliteMud.Server.Adapters.Commands.Drop;
using EliteMud.Server.Adapters.Commands.Equipment;
using EliteMud.Server.Adapters.Commands.Examine;
using EliteMud.Server.Adapters.Commands.Get;
using EliteMud.Server.Adapters.Commands.Hold;
using EliteMud.Server.Adapters.Commands.Inventory;
using EliteMud.Server.Adapters.Commands.Look;
using EliteMud.Server.Adapters.Commands.Move;
using EliteMud.Server.Adapters.Commands.NoOp;
using EliteMud.Server.Adapters.Commands.Quit;
using EliteMud.Server.Adapters.Commands.Remove;
using EliteMud.Server.Adapters.Commands.ResetZone;
using EliteMud.Server.Adapters.Commands.Say;
using EliteMud.Server.Adapters.Commands.Wear;
using EliteMud.Server.Adapters.Commands.Who;
using EliteMud.Server.Adapters.Commands.Wield;

namespace EliteMud.Server.Adapters.Commands.Shared;

internal sealed class CommandModuleProvider : ICommandModuleProvider
{
    public IReadOnlyList<ICommandModule> GetModules()
    {
        return new ICommandModule[]
        {
            new NoOpCommandModule(),
            new QuitCommandModule(),
            new LookCommandModule(),
            new ExamineCommandModule(),
            new GetCommandModule(),
            new DropCommandModule(),
            new InventoryCommandModule(),
            new EquipmentCommandModule(),
            new WearCommandModule(),
            new RemoveCommandModule(),
            new WieldCommandModule(),
            new HoldCommandModule(),
            new WhoCommandModule(),
            new ResetZoneCommandModule(),
            new SayCommandModule(),
            new MoveCommandModule()
        };
    }
}
