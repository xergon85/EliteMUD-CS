using System.Reflection;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Skills;
using EliteMud.Server.Adapters.Commands.Consider;
using EliteMud.Server.Adapters.Commands.Drop;
using EliteMud.Server.Adapters.Commands.Equipment;
using EliteMud.Server.Adapters.Commands.Examine;
using EliteMud.Server.Adapters.Commands.Flee;
using EliteMud.Server.Adapters.Commands.Get;
using EliteMud.Server.Adapters.Commands.Hold;
using EliteMud.Server.Adapters.Commands.Inventory;
using EliteMud.Server.Adapters.Commands.Kill;
using EliteMud.Server.Adapters.Commands.Load;
using EliteMud.Server.Adapters.Commands.Look;
using EliteMud.Server.Adapters.Commands.Move;
using EliteMud.Server.Adapters.Commands.NoOp;
using EliteMud.Server.Adapters.Commands.Quit;
using EliteMud.Server.Adapters.Commands.Remove;
using EliteMud.Server.Adapters.Commands.ResetZone;
using EliteMud.Server.Adapters.Commands.Rest;
using EliteMud.Server.Adapters.Commands.Save;
using EliteMud.Server.Adapters.Commands.Say;
using EliteMud.Server.Adapters.Commands.Score;
using EliteMud.Server.Adapters.Commands.Search;
using EliteMud.Server.Adapters.Commands.SetLevel;
using EliteMud.Server.Adapters.Commands.SetSkill;
using EliteMud.Server.Adapters.Commands.Sit;
using EliteMud.Server.Adapters.Commands.Skills;
using EliteMud.Server.Adapters.Commands.Sleep;
using EliteMud.Server.Adapters.Commands.Stand;
using EliteMud.Server.Adapters.Commands.Wake;
using EliteMud.Server.Adapters.Commands.Wear;
using EliteMud.Server.Adapters.Commands.Who;
using EliteMud.Server.Adapters.Commands.Wield;
using EliteMud.Server.Adapters.Commands.Wimpy;

namespace EliteMud.Server.Adapters.Commands.Shared;

internal sealed class CommandModuleProvider : ICommandModuleProvider
{
    public IReadOnlyList<ICommandModule> GetModules()
    {
        var staticModules = new List<ICommandModule>
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
            new LoadCommandModule(),
            new SearchCommandModule(),
            new WhoCommandModule(),
            new ScoreCommandModule(),
            new SaveCommandModule(),
            new ResetZoneCommandModule(),
            new SayCommandModule(),
            new MoveCommandModule(),
            new KillCommandModule(),
            // new KickCommandModule(), // REMOVED - now auto-registered via ISkillExecutor
            new FleeCommandModule(),
            new WimpyCommandModule(),
            new SleepCommandModule(),
            new RestCommandModule(),
            new SitCommandModule(),
            new WakeCommandModule(),
            new StandCommandModule(),
            new ConsiderCommandModule(),
            new SetSkillCommandModule(),
            new SetLevelCommandModule(),
            new SkillsCommandModule()
        };
        
        // Auto-discover skill executor modules
        var skillModules = GetSkillExecutorModules();
        
        return staticModules.Concat(skillModules).ToList();
    }
    
    /// <summary>
    /// Auto-discover all ISkillExecutor implementations and create modules for them.
    /// This makes adding new skills trivial - just implement ISkillExecutor.
    /// 
    /// Note: We read CommandKind from a static property or by convention.
    /// Executors should have a static CommandKind that matches their SkillType.
    /// </summary>
    private static IEnumerable<ICommandModule> GetSkillExecutorModules()
    {
        var executorTypes = typeof(ISkillExecutor).Assembly
            .GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(ISkillExecutor).IsAssignableFrom(t));
        
        foreach (var type in executorTypes)
        {
            // Get CommandKind by convention from type name
            // KickSkillExecutor -> CommandKind.Kick
            // BashSkillExecutor -> CommandKind.Bash
            var commandKind = GetCommandKindFromTypeName(type.Name);
            
            if (commandKind.HasValue)
            {
                yield return new SkillCommandModule(commandKind.Value, type);
            }
        }
    }
    
    /// <summary>
    /// Extract CommandKind from executor type name by convention.
    /// KickSkillExecutor -> Kick, BashSkillExecutor -> Bash, etc.
    /// </summary>
    private static CommandKind? GetCommandKindFromTypeName(string typeName)
    {
        // Remove "SkillExecutor" suffix to get skill name
        if (!typeName.EndsWith("SkillExecutor"))
            return null;
        
        var skillName = typeName.Substring(0, typeName.Length - "SkillExecutor".Length);
        
        // Try to parse as CommandKind enum
        if (Enum.TryParse<CommandKind>(skillName, ignoreCase: true, out var commandKind))
        {
            return commandKind;
        }
        
        return null;
    }
}
