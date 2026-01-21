using EliteMud.Application.Session;
using EliteMud.Data;
using EliteMud.Data.Repositories;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EliteMud.Server;

/// <summary>
/// Background service that handles periodic game ticks.
/// Runs regeneration every 75 seconds (MUD hour) matching legacy behavior.
/// Also handles auto-save every 5 minutes.
/// </summary>
internal sealed class GameTickService
{
    private readonly ConnectionRegistry _connectionRegistry;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _tickInterval = TimeSpan.FromSeconds(75); // MUD hour (matches legacy)
    private readonly TimeSpan _autoSaveInterval = TimeSpan.FromMinutes(5); // Auto-save every 5 minutes
    
    private int _tickCount;
    private DateTime _lastAutoSave = DateTime.UtcNow;

    public GameTickService(ConnectionRegistry connectionRegistry, IServiceProvider serviceProvider)
    {
        _connectionRegistry = connectionRegistry;
        _serviceProvider = serviceProvider;
    }

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine($"[GameTick] Service started. Tick interval: {_tickInterval.TotalSeconds}s, Auto-save interval: {_autoSaveInterval.TotalMinutes}min");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_tickInterval, stoppingToken);
            
            try
            {
                _tickCount++;
                ProcessGameTick();
                
                // Check if it's time for auto-save
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

    private void ProcessGameTick()
    {
        var connections = _connectionRegistry.GetConnections().ToList();
        
        if (connections.Count == 0)
        {
            return; // No players online, skip tick
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
                Console.WriteLine($"[GameTick] Error regenerating player {connection.Player.Name}: {ex.Message}");
            }
        }
        
        if (playersRegenerated > 0)
        {
            Console.WriteLine($"[GameTick] Tick #{_tickCount}: {playersRegenerated}/{connections.Count} players regenerated");
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
