using EliteMud.Application.Commands.Flee;
using EliteMud.Application.Commands.ImportLegacy;
using EliteMud.Application.Commands.Score;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Commands.Who;
using EliteMud.Application.Session;
using EliteMud.Application.Skills;
using EliteMud.Game;
using EliteMud.Legacy.Import;
using EliteMud.Scripting;
using EliteMud.Server.Adapters.Commands.ImportLegacy;
using EliteMud.Server.Adapters.Commands.Look;
using EliteMud.Server.Adapters.Commands.Move;
using EliteMud.Server.Adapters.Commands.ResetZone;
using EliteMud.Server.Adapters.Commands.Say;
using EliteMud.Server.Adapters.Commands.Who;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server.Adapters.Commands.Shared;

internal static class CommandServiceCollectionExtensions
{
    public static IServiceCollection AddCommandHandlers(this IServiceCollection services)
    {
        return services
            // Skills infrastructure
            .AddSingleton<SkillRegistry>()
            .AddSingleton<CombatCalculator>(provider =>
            {
                var skillRegistry = provider.GetRequiredService<SkillRegistry>();
                var dodgeSkill = skillRegistry.GetPassiveSkill(SkillType.Dodge);
                return new CombatCalculator(dodgeSkill);
            })

            // Auto-register all skill executors (makes them available as commands)
            .AddSkillExecutors()
            .AddSingleton<CommandCatalog>()
            .AddSingleton<PromptCatalog>()
            .AddSingleton<LegacyContentImporter>()
            .AddSingleton<ImportLegacyHandler>()
            .AddSingleton<ImportLegacyCommandHandler>()
            .AddSingleton<LookCommandHandler>()
            .AddSingleton<MoveCommandHandler>()
            .AddSingleton<FleeHandler>()
            .AddSingleton<SayCommandHandler>(provider => new SayCommandHandler(
                provider.GetRequiredService<IScriptEngine>(),
                provider.GetRequiredService<ConnectionRegistry>().GetConnections
            ))
            .AddSingleton<ResetZoneCommandHandler>()
            .AddSingleton<ScoreHandler>()
            .AddSingleton<WhoCommandHandler>(provider =>
                new WhoCommandHandler(provider.GetRequiredService<ConnectionRegistry>().GetConnections
                ))
            .AddSingleton<IConnectionDirectory>(provider => provider.GetRequiredService<WhoCommandHandler>())
            .AddSingleton<ConnectionRegistry>()
            .AddSingleton<ICommandModuleProvider, CommandModuleProvider>()
            .AddSingleton<CommandHandlerRegistry>()
            .AddSingleton<CommandRouter>(provider =>
            {
                var modules = provider.GetRequiredService<ICommandModuleProvider>().GetModules();
                var handlerRegistry = provider.GetRequiredService<CommandHandlerRegistry>();
                var handlers = handlerRegistry.BuildHandlers(modules, provider);
                return new CommandRouter(handlers);
            });
    }

    /// <summary>
    /// Auto-discover and register all ISkillExecutor implementations.
    /// This makes skill executors automatically available as commands.
    /// </summary>
    private static IServiceCollection AddSkillExecutors(this IServiceCollection services)
    {
        var executorTypes = typeof(ISkillExecutor).Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(ISkillExecutor).IsAssignableFrom(t));

        foreach (var type in executorTypes)
        {
            // Register the executor itself
            services.AddSingleton(type);
        }

        return services;
    }
}
