using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Skills;
using EliteMud.Application.World;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Skills;

/// <summary>
/// Generic command module - ONE implementation for ALL skills.
/// Auto-created for each ISkillExecutor during registration.
/// </summary>
internal sealed class SkillCommandModule : ICommandModule
{
    private readonly CommandKind _kind;
    private readonly Type _executorType;
    
    public CommandKind Kind => _kind;
    
    public SkillCommandModule(CommandKind kind, Type executorType)
    {
        _kind = kind;
        _executorType = executorType;
    }
    
    public ICommandHandler CreateHandler(IServiceProvider serviceProvider)
    {
        var executor = (ISkillExecutor)serviceProvider.GetRequiredService(_executorType);
        var worldState = serviceProvider.GetRequiredService<IWorldState>();
        var actService = serviceProvider.GetRequiredService<ActMessageService>();
        var connectionRegistry = serviceProvider.GetRequiredService<ConnectionRegistry>();
        
        return new SkillCommandHandler(executor, worldState, actService, connectionRegistry);
    }
}
