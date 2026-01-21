using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Session;
using EliteMud.Application.World;
using EliteMud.Data;
using EliteMud.Data.Repositories;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server;

/// <summary>
/// Background service that handles periodic game ticks.
/// Runs combat every 2 seconds (PULSE_VIOLENCE).
/// Runs regeneration every 75 seconds (MUD hour).
/// Runs auto-save every 5 minutes.
/// </summary>
internal sealed class GameTickService
{
    private readonly ConnectionRegistry _connectionRegistry;
    private readonly IServiceProvider _serviceProvider;
    private readonly IWorldState _worldState;
    private readonly ActMessageService _actService;
    
    private readonly TimeSpan _combatInterval = TimeSpan.FromSeconds(2); // PULSE_VIOLENCE
    private readonly TimeSpan _regenInterval = TimeSpan.FromSeconds(75); // MUD hour (matches legacy)
    private readonly TimeSpan _autoSaveInterval = TimeSpan.FromMinutes(5); // Auto-save every 5 minutes
    
    private int _tickCount;
    private DateTime _lastRegen = DateTime.UtcNow;
    private DateTime _lastAutoSave = DateTime.UtcNow;

    public GameTickService(
        ConnectionRegistry connectionRegistry,
        IServiceProvider serviceProvider,
        IWorldState worldState,
        ActMessageService actService)
    {
        _connectionRegistry = connectionRegistry;
        _serviceProvider = serviceProvider;
        _worldState = worldState;
        _actService = actService;
    }

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine($"[GameTick] Service started. Combat: {_combatInterval.TotalSeconds}s, Regen: {_regenInterval.TotalSeconds}s, Auto-save: {_autoSaveInterval.TotalMinutes}min");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_combatInterval, stoppingToken);
            
            try
            {
                _tickCount++;
                
                // Combat runs every tick (2 seconds)
                await ProcessCombatRoundAsync(stoppingToken);
                
                // Regeneration runs every 75 seconds
                if (DateTime.UtcNow - _lastRegen >= _regenInterval)
                {
                    ProcessRegeneration();
                    _lastRegen = DateTime.UtcNow;
                }
                
                // Auto-save runs every 5 minutes
                if (DateTime.UtcNow - _lastAutoSave >= _autoSaveInterval)
                {
                    await ProcessAutoSaveAsync(stoppingToken);
                    _lastAutoSave = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameTick] Error during tick: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Process one round of combat for all fighting characters.
    /// Legacy: perform_violence()
    /// </summary>
    private async Task ProcessCombatRoundAsync(CancellationToken cancellationToken)
    {
        var connections = _connectionRegistry.GetConnections().ToList();
        
        // Find all players in combat
        var fightingPlayers = connections.Where(c => c.Player.FightingConnectionId != null).ToList();
        
        if (fightingPlayers.Count == 0)
        {
            return; // No combat happening
        }

        foreach (var attacker in fightingPlayers)
        {
            try
            {
                // Skip if attacker is dead or incapacitated
                if (attacker.Player.Position < CombatService.POS_STUNNED)
                {
                    continue;
                }

                var targetConnectionId = attacker.Player.FightingConnectionId;
                if (targetConnectionId == null)
                {
                    continue; // No longer fighting
                }

                // Check if fighting a mob (negative ID) or player (positive ID)
                if (targetConnectionId.Value < 0)
                {
                    // Fighting a mob
                    var mobInstanceId = -targetConnectionId.Value;
                    await ProcessPlayerVsMobAttack(attacker, mobInstanceId, cancellationToken);
                }
                else
                {
                    // Fighting another player
                    var victim = connections.FirstOrDefault(c => c.Id == targetConnectionId.Value);
                    if (victim == null || victim.Player.RoomId != attacker.Player.RoomId)
                    {
                        // Target left room or disconnected
                        CombatService.StopFighting(attacker.Player);
                        await attacker.Session.SendLineAsync("Your opponent has left.", cancellationToken);
                        continue;
                    }

                    await ProcessPlayerVsPlayerAttack(attacker, victim, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Combat] Error processing combat for {attacker.Player.Name}: {ex.Message}");
            }
        }
    }

    private async Task ProcessPlayerVsPlayerAttack(
        ConnectionContext attacker,
        ConnectionContext victim,
        CancellationToken cancellationToken)
    {
        var result = CombatService.PerformAttack(attacker.Player, victim.Player);
        
        if (result.Hit)
        {
            await attacker.Session.SendLineAsync(
                $"You hit {victim.Player.Name} for {result.Damage} damage!", cancellationToken);
            await victim.Session.SendLineAsync(
                $"{attacker.Player.Name} hits you for {result.Damage} damage! [{victim.Player.HitPoints}/{victim.Player.MaxHitPoints} HP]", 
                cancellationToken);

            // Award experience
            attacker.Player.Experience += CombatService.CalculateExperienceGain(victim.Player, result.Damage);

            // Check if victim died
            if (victim.Player.Position == CombatService.POS_DEAD)
            {
                await HandlePlayerDeath(attacker, victim, cancellationToken);
            }
        }
        else
        {
            await attacker.Session.SendLineAsync(
                $"You miss {victim.Player.Name}!", cancellationToken);
        }
    }

    private async Task ProcessPlayerVsMobAttack(
        ConnectionContext attacker,
        int mobInstanceId,
        CancellationToken cancellationToken)
    {
        // Find the mob in the player's room
        var mobs = _worldState.GetMobsInRoom(attacker.Player.RoomId);
        var mob = mobs.FirstOrDefault(m => m.InstanceId == mobInstanceId);

        if (mob == null)
        {
            // Mob is gone (killed by someone else, or despawned)
            CombatService.StopFighting(attacker.Player);
            await attacker.Session.SendLineAsync("Your opponent has left.", cancellationToken);
            return;
        }

        // Simplified mob combat - calculate damage based on player stats
        // TODO: Make this more accurate with mob AC, level, etc.
        int damage = CombatService.CalculateBareDamage(attacker.Player);
        
        // Apply damage to mob
        mob.HitPoints -= damage;
        
        await attacker.Session.SendLineAsync(
            $"You hit {mob.Definition.ShortDescription} for {damage} damage!", cancellationToken);

        // Award experience
        attacker.Player.Experience += mob.Definition.Level * damage / 2;

        // Check if mob died
        if (mob.HitPoints <= 0)
        {
            await HandleMobDeath(attacker, mob, cancellationToken);
        }
        else
        {
            // Mob fights back (simplified - just damage the player)
            var mobDamage = mob.Definition.Level + Random.Shared.Next(1, 5);
            CombatService.ApplyDamage(attacker.Player, mobDamage);
            
            await attacker.Session.SendLineAsync(
                $"{mob.Definition.ShortDescription} hits you for {mobDamage} damage! [{attacker.Player.HitPoints}/{attacker.Player.MaxHitPoints} HP]",
                cancellationToken);

            // Check if player died
            if (attacker.Player.Position == CombatService.POS_DEAD)
            {
                await HandlePlayerDeathFromMob(attacker, mob, cancellationToken);
            }
        }
    }

    private async Task HandlePlayerDeath(
        ConnectionContext killer,
        ConnectionContext victim,
        CancellationToken cancellationToken)
    {
        // Stop combat
        CombatService.StopFighting(killer.Player);
        CombatService.StopFighting(victim.Player);

        // Award full experience (bonus for kill)
        int killBonus = victim.Player.Level * 100;
        killer.Player.Experience += killBonus;

        // Messages
        await killer.Session.SendLineAsync(
            $"You have slain {victim.Player.Name}! (+{killBonus} exp)", cancellationToken);
        await victim.Session.SendLineAsync(
            "You have been KILLED!!", cancellationToken);

        // Broadcast to room
        var roomMessage = $"{victim.Player.Name} is DEAD! R.I.P.";
        var otherPlayers = _connectionRegistry.GetConnections()
            .Where(c => c.Player.RoomId == victim.Player.RoomId && c.Id != killer.Id && c.Id != victim.Id);
        
        foreach (var observer in otherPlayers)
        {
            await observer.Session.SendLineAsync(roomMessage, cancellationToken);
        }

        // TODO: Create corpse, transfer items
        // For now, just respawn the player
        victim.Player.HitPoints = victim.Player.MaxHitPoints;
        victim.Player.Mana = victim.Player.MaxMana;
        victim.Player.Movement = victim.Player.MaxMovement;
        victim.Player.Position = CombatService.POS_STANDING;
        victim.Player.RoomId = 1; // Respawn at starting room
        
        await victim.Session.SendLineAsync(
            "You have been resurrected...", cancellationToken);
    }

    private async Task HandlePlayerDeathFromMob(
        ConnectionContext victim,
        MobInstance mob,
        CancellationToken cancellationToken)
    {
        // Stop combat
        CombatService.StopFighting(victim.Player);
        mob.FightingConnectionId = null;
        mob.Position = CombatService.POS_STANDING;

        // Messages
        await victim.Session.SendLineAsync(
            $"You have been KILLED by {mob.Definition.ShortDescription}!!", cancellationToken);

        // Broadcast to room
        var roomMessage = $"{victim.Player.Name} has been killed by {mob.Definition.ShortDescription}!";
        var otherPlayers = _connectionRegistry.GetConnections()
            .Where(c => c.Player.RoomId == victim.Player.RoomId && c.Id != victim.Id);
        
        foreach (var observer in otherPlayers)
        {
            await observer.Session.SendLineAsync(roomMessage, cancellationToken);
        }

        // Respawn the player
        victim.Player.HitPoints = victim.Player.MaxHitPoints;
        victim.Player.Mana = victim.Player.MaxMana;
        victim.Player.Movement = victim.Player.MaxMovement;
        victim.Player.Position = CombatService.POS_STANDING;
        victim.Player.RoomId = 1; // Respawn at starting room
        
        await victim.Session.SendLineAsync(
            "You have been resurrected...", cancellationToken);
    }

    private async Task HandleMobDeath(
        ConnectionContext killer,
        MobInstance mob,
        CancellationToken cancellationToken)
    {
        // Stop combat
        CombatService.StopFighting(killer.Player);
        mob.FightingConnectionId = null;

        // Award kill experience
        int killBonus = mob.Definition.Level * 100;
        killer.Player.Experience += killBonus;

        // Messages
        await killer.Session.SendLineAsync(
            $"You have slain {mob.Definition.ShortDescription}! (+{killBonus} exp)", cancellationToken);

        // Broadcast to room
        var roomMessage = $"{mob.Definition.ShortDescription} is DEAD!";
        var otherPlayers = _connectionRegistry.GetConnections()
            .Where(c => c.Player.RoomId == killer.Player.RoomId && c.Id != killer.Id);
        
        foreach (var observer in otherPlayers)
        {
            await observer.Session.SendLineAsync(roomMessage, cancellationToken);
        }

        // TODO: Create corpse with loot
        // TODO: Remove mob from world (needs IWorldState.RemoveMob method)
        // For now, just mark it as dead by setting HP to 0
    }

    private void ProcessRegeneration()
    {
        var connections = _connectionRegistry.GetConnections().ToList();
        
        if (connections.Count == 0)
        {
            return; // No players online, skip
        }

        int playersRegenerated = 0;
        
        foreach (var connection in connections)
        {
            try
            {
                // Apply regeneration
                bool didRegen = RegenerationService.RegeneratePlayer(connection.Player);
                
                if (didRegen)
                {
                    playersRegenerated++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Regen] Error regenerating player {connection.Player.Name}: {ex.Message}");
            }
        }
        
        if (playersRegenerated > 0)
        {
            Console.WriteLine($"[Regen] Tick #{_tickCount}: {playersRegenerated}/{connections.Count} players regenerated");
        }
    }

    private async Task ProcessAutoSaveAsync(CancellationToken cancellationToken)
    {
        var connections = _connectionRegistry.GetConnections().ToList();
        
        if (connections.Count == 0)
        {
            return; // No players to save
        }

        Console.WriteLine($"[AutoSave] Saving {connections.Count} player(s)...");
        
        int savedCount = 0;
        int errorCount = 0;
        
        // Create a scope to get the scoped repository
        await using var scope = _serviceProvider.CreateAsyncScope();
        var characterRepository = scope.ServiceProvider.GetRequiredService<ICharacterRepository>();
        
        foreach (var connection in connections)
        {
            try
            {
                // Load the character from database
                var character = await characterRepository.GetByIdAsync(connection.CharacterId, cancellationToken);
                if (character is null)
                {
                    Console.WriteLine($"[AutoSave] Warning: Character {connection.Player.Name} (ID:{connection.CharacterId}) not found in database");
                    errorCount++;
                    continue;
                }
                
                // Update the character with current player state
                CharacterMapper.UpdateCharacterFromPlayerState(character, connection.Player);
                
                // Save to database
                await characterRepository.UpdateAsync(character, cancellationToken);
                savedCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AutoSave] Error saving {connection.Player.Name}: {ex.Message}");
                errorCount++;
            }
        }
        
        if (savedCount > 0)
        {
            Console.WriteLine($"[AutoSave] Complete: {savedCount} saved, {errorCount} errors");
        }
    }
}
