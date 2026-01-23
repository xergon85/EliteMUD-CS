using EliteMud.Application.Commands.Flee;
using EliteMud.Application.Commands.ImportLegacy;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Commands.Who;
using EliteMud.Application.Commands.Score;
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
            // Skills
            .AddSingleton<SkillRegistry>()
            .AddSingleton<CombatCalculator>(provider =>
            {
                var skillRegistry = provider.GetRequiredService<SkillRegistry>();
                var dodgeSkill = skillRegistry.GetPassiveSkill(SkillType.Dodge);
                return new CombatCalculator(dodgeSkill);
            })
            .AddSingleton<KickSkillExecutor>()
            
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
}
