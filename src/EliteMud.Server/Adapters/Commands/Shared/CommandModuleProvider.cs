using EliteMud.Server.Adapters.Commands.Look;
using EliteMud.Server.Adapters.Commands.Move;
using EliteMud.Server.Adapters.Commands.NoOp;
using EliteMud.Server.Adapters.Commands.Quit;
using EliteMud.Server.Adapters.Commands.ResetZone;
using EliteMud.Server.Adapters.Commands.Say;
using EliteMud.Server.Adapters.Commands.Who;

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
            new WhoCommandModule(),
            new ResetZoneCommandModule(),
            new SayCommandModule(),
            new MoveCommandModule()
        };
    }
}
