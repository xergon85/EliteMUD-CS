using EliteMud.Application.Commands.Flee;
using EliteMud.Application.Commands.ImportLegacy;
using EliteMud.Application.Commands.Score;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Commands.Who;
using EliteMud.Application.Session;
using EliteMud.Application.Skills;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Legacy.Import;
using EliteMud.Server.Adapters.Commands.Skills;
using EliteMud.Server.Adapters.Commands.Who;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

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
            .AddSkillExecutors()
            
            // Infrastructure
            .AddSingleton<CommandCatalog>()
            .AddSingleton<PromptCatalog>()
            .AddSingleton<ConnectionRegistry>()
            
            // Business logic helpers (not command handlers)
            .AddSingleton<LegacyContentImporter>()
            .AddSingleton<ImportLegacyHandler>()
            .AddSingleton<FleeHandler>()
            .AddSingleton<ScoreHandler>()

            // Auto-discover all command handlers with [Command] attribute
            .AddCommandHandlersViaReflection()
            
            // Special interface registration
            .AddSingleton<IConnectionDirectory>(provider =>
                provider.GetRequiredService<WhoCommandHandler>())

            // Command routing
            .AddSingleton<CommandRouter>(provider =>
            {
                var handlers = provider.GetServices<ICommandHandler>();
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

            // Wrap in SkillCommandHandler and register as ICommandHandler
            services.AddSingleton<ICommandHandler>(provider =>
            {
                var executor = (ISkillExecutor)provider.GetRequiredService(type);
                var worldState = provider.GetRequiredService<IWorldState>();
                var actService = provider.GetRequiredService<ActMessageService>();
                var connectionRegistry = provider.GetRequiredService<ConnectionRegistry>();

                return new SkillCommandHandler(executor, worldState, actService, connectionRegistry);
            });
        }

        return services;
    }

    /// <summary>
    /// Auto-discover all command handlers decorated with [Command] attribute.
    /// This eliminates the need for manual registration or CommandModule factories.
    /// </summary>
    private static IServiceCollection AddCommandHandlersViaReflection(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        var handlerTypes = assembly.GetTypes()
            .Where(t =>
                t is { IsAbstract: false, IsInterface: false, IsClass: true } &&
                typeof(ICommandHandler).IsAssignableFrom(t) &&
                t.GetCustomAttribute<CommandAttribute>() != null);

        foreach (var type in handlerTypes)
        {
            // Register as concrete type (for dependencies like FleeCommandHandler -> LookCommandHandler)
            services.AddSingleton(type);
            
            // Also register as ICommandHandler (for CommandRouter)
            services.AddSingleton<ICommandHandler>(provider => (ICommandHandler)provider.GetRequiredService(type));
        }

        return services;
    }
}
