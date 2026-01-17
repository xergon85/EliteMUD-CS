namespace EliteMud.Server.Adapters.Commands.Shared;

internal interface ICommandModuleProvider
{
    IReadOnlyList<ICommandModule> GetModules();
}
