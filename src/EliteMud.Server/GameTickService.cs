using EliteMud.Application.Ai;
using EliteMud.Application.Combat;
using EliteMud.Application.Session;
using EliteMud.Application.Skills;
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
    private readonly CombatDamage _combatDamage;
    private readonly SkillMessageSender _messageSender;
    private readonly CombatCalculator _combatCalculator;
    private readonly CharacterSaveQueue _saveQueue;
    private readonly MobAiService _mobAiService;
    
    private readonly TimeSpan _combatInterval = TimeSpan.FromSeconds(2); // PULSE_VIOLENCE
    private readonly TimeSpan _gainInterval = TimeSpan.FromSeconds(6); // PULSE_GAIN - increment gain_count based on position (legacy: 6 seconds)
    private readonly TimeSpan _regenInterval = TimeSpan.FromSeconds(75); // SECS_PER_MUD_HOUR - apply regeneration (legacy: 75 seconds)
    private readonly TimeSpan _autoSaveInterval = TimeSpan.FromMinutes(5); // Auto-save every 5 minutes
    
    private int _tickCount;
    private DateTime _lastGain = DateTime.UtcNow;
    private DateTime _lastRegen = DateTime.UtcNow;
    private DateTime _lastAutoSave = DateTime.UtcNow;
    private readonly GameStateGate _gate;

    public GameTickService(
        ConnectionRegistry connectionRegistry,
        IServiceProvider serviceProvider,
        IWorldState worldState,
        CombatDamage combatDamage,
        SkillMessageSender messageSender,
        CombatCalculator combatCalculator,
        CharacterSaveQueue saveQueue,
        MobAiService mobAiService,
        GameStateGate gate)
    {
        _gate = gate;
        _connectionRegistry = connectionRegistry;
        _serviceProvider = serviceProvider;
        _worldState = worldState;
        _combatDamage = combatDamage;
        _messageSender = messageSender;
        _combatCalculator = combatCalculator;
        _saveQueue = saveQueue;
        _mobAiService = mobAiService;
    }

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine($"[GameTick] Service started. Combat: {_combatInterval.TotalSeconds}s, Gain: {_gainInterval.TotalSeconds}s, Regen: {_regenInterval.TotalSeconds}s, Auto-save: {_autoSaveInterval.TotalMinutes}min");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_combatInterval, stoppingToken);

            // One tick pass at a time with commands, joins and saves (#61)
            using var gate = await _gate.EnterAsync(stoppingToken);
            try
            {
                _tickCount++;
                
                // Combat runs every tick (2 seconds)
                await ProcessCombatRoundAsync(stoppingToken);
                
                // Mob AI runs every tick (same as combat)
                ProcessMobAi();
                
                // Gain count increment runs every 2 seconds (same as combat for simplicity)
                if (DateTime.UtcNow - _lastGain >= _gainInterval)
                {
                    ProcessGainIncrement();
                    _lastGain = DateTime.UtcNow;
                }
                
                // Regeneration runs every 60 seconds
                if (DateTime.UtcNow - _lastRegen >= _regenInterval)
                {
                    ProcessRegeneration();
                    ProcessPortalTimeouts();
                    _lastRegen = DateTime.UtcNow;
                }
                
                // Characters that asked for a save (e.g. after a level-up) are saved right away
                ProcessRequestedSaves(stoppingToken);

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
    internal async Task ProcessCombatRoundAsync(CancellationToken cancellationToken)
    {
        var connections = _connectionRegistry.GetConnections().ToList();
        
        // Decrement wait states for all players (happens every combat tick)
        foreach (var connection in connections)
        {
            connection.Player.DecrementWaitState();
        }
        
        // Find all players in combat
        var fightingPlayers = connections.Where(c => c.Player.Fighting != null).ToList();
        
        // Process player attacks
        // Legacy: perform_violence() in fight.c:1664-1729
        // Each combatant can perform up to 5 attacks per round:
        // 1. Primary attack (always)
        // 2. 2nd attack (requires SKILL_2ATTACK > random(1,120))
        // 3. 3rd attack (requires SKILL_3ATTACK > random(1,140))
        // 4. 4th attack (requires SKILL_4ATTACK > random(1,160))
        // 5. Dual-wield attack (requires SKILL_DUAL > random(1,160) AND weapon in HOLD slot)
        foreach (var attacker in fightingPlayers)
        {
            try
            {
                // Skip if attacker can't fight (stunned, incapacitated, mortally wounded, or dead)
                // Players in these positions can't attack but can still be attacked
                if (attacker.Player.Position < Position.Fighting)
                {
                    continue;
                }

                // Process up to 5 attacks per round
                for (int attackNum = 1; attackNum <= 5; attackNum++)
                {
                    // Check if target is still valid before each attack
                    var opponent = attacker.Player.Fighting;
                    if (opponent == null)
                    {
                        break; // Combat ended (target died or fled)
                    }

                    // First attack always happens, subsequent attacks require skill checks
                    if (attackNum > 1 && !CanPerformExtraAttack(attacker.Player, attackNum))
                    {
                        continue; // Skip this attack, but continue loop to check remaining attacks
                    }

                    // The opponent must still be here: a mob that died or left is in another room or nowhere
                    if (opponent.RoomId != attacker.Player.RoomId)
                    {
                        _combatCalculator.StopFighting(attacker.Player);
                        await attacker.Session.SendLineAsync("Your opponent has left.", cancellationToken);
                        break;
                    }

                    await HitAsync(attacker.Player, opponent, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Combat] Error processing combat for {attacker.Player.Name}: {ex.Message}");
            }
        }
        
        // Process mob attacks
        await ProcessMobAttacksAsync(cancellationToken);
    }

    /// <summary>
    /// Every mob fighting a player hits it once, on its own turn (legacy perform_violence), whether or not the player
    /// can fight back.
    /// </summary>
    private async Task ProcessMobAttacksAsync(CancellationToken cancellationToken)
    {
        var fightingMobs = _worldState.World.Rooms.Keys
            .SelectMany(roomId => _worldState.GetMobsInRoom(roomId))
            .Where(mob => mob.Fighting is PlayerState) // mob against mob doesn't attack yet
            .ToList();

        foreach (var mob in fightingMobs)
        {
            var opponent = mob.Fighting;
            if (opponent == null)
            {
                continue; // the fight ended earlier this round, e.g. the player fled
            }

            if (opponent.RoomId != mob.RoomId)
            {
                // Player left or disconnected
                _combatCalculator.StopFighting(mob);
                continue;
            }

            try
            {
                await HitAsync(mob, opponent, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Combat] Error processing mob attack: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// One melee hit (legacy hit(), then damage()): the attacker's damage roll, the victim's defense, then the
    /// shared damage path. Everyone in the room sees the hit, then whatever followed it (position, wimpy, death).
    /// </summary>
    private async Task HitAsync(ICombatant attacker, ICombatant victim, CancellationToken cancellationToken)
    {
        var (damage, defense) = RollMelee(attacker, victim);
        var hitPointsBefore = victim.HitPoints;
        var victimMaxHp = MaxHitPoints(victim); // before a death moves their gear into the corpse
        var outcome = _combatDamage.Apply(attacker, victim, damage);

        var messages = new List<SkillMessage>();
        if (defense != null)
        {
            messages.Add(new SkillMessage(SkillMessageTarget.Victim, defense, victim));
        }

        // The hit as it landed, before any death or respawn changed the victim's hit points
        var hitPointsAfterHit = hitPointsBefore - outcome.Damage;
        var victimLine = CombatMessageFormatter.FormatCombatMessage(
            attacker.Name, victim.Name, outcome.Damage, victimMaxHp, MessagePerspective.ToVict);
        messages.Add(new SkillMessage(SkillMessageTarget.Actor, CombatMessageFormatter.FormatCombatMessage(
            attacker.Name, victim.Name, outcome.Damage, victimMaxHp, MessagePerspective.ToChar)));
        messages.Add(new SkillMessage(
            SkillMessageTarget.Victim, $"{victimLine} [{hitPointsAfterHit}/{victimMaxHp} HP]", victim));
        messages.Add(new SkillMessage(SkillMessageTarget.Others, CombatMessageFormatter.FormatCombatMessage(
            attacker.Name, victim.Name, outcome.Damage, victimMaxHp, MessagePerspective.ToRoom), victim));
        messages.AddRange(outcome.Messages);

        await _messageSender.SendAsync(messages, attacker, cancellationToken);
    }

    /// <summary>
    /// The damage of a melee hit after the victim's defense, and the victim's dodge or parry line if any. Players
    /// roll to hit only against players for now (#48); weapon effects come on top (step 7 of #42 unifies the roll).
    /// </summary>
    private (int Damage, string? Defense) RollMelee(ICombatant attacker, ICombatant victim)
    {
        var weapon = Wielded(attacker);
        var weaponDetails = weapon?.Definition.Details?.Weapon;

        if (attacker is PlayerState player && victim is PlayerState victimPlayer)
        {
            var result = _combatCalculator.ResolveAttack(
                player,
                _worldState.GetTotalEffectiveStrength(player),
                _worldState.GetTotalEffectiveIntelligence(player),
                _worldState.GetTotalEffectiveWisdom(player),
                victimPlayer,
                _worldState.GetTotalEffectiveDexterity(victimPlayer),
                weaponDetails);
            if (!result.Hit)
            {
                return (0, null);
            }

            var effects = weapon != null ? _combatCalculator.ApplyWeaponEffects(weapon.Definition.Flags, victim) : 0;
            return (result.Damage + effects, result.Message);
        }

        var damage = attacker switch
        {
            PlayerState attackingPlayer => _combatCalculator.CalculateDamage(
                attackingPlayer, _worldState.GetTotalEffectiveStrength(attackingPlayer), weaponDetails),
            MobInstance mob => MobDamage(mob, weaponDetails),
            _ => 0
        };
        if (weapon != null)
        {
            damage += _combatCalculator.ApplyWeaponEffects(weapon.Definition.Flags, victim);
        }

        var defended = _combatCalculator.DefendAgainstMelee(victim, damage);
        return (defended.Damage, defended.Message);
    }

    /// <summary>
    /// A mob's melee damage before weapon effects (legacy hit(), fight.c:1440-1473): damroll, plus the weapon's dice
    /// and its natural attack dice when it wields one, or only the natural attack without.
    /// </summary>
    private int MobDamage(MobInstance mob, ObjectWeapon? weaponDetails)
    {
        int damage = WorldStateExtensions.GetMobEffectiveDamroll(mob);
        if (weaponDetails != null)
        {
            damage += _combatCalculator.RollDice(weaponDetails.DiceCount, weaponDetails.DiceSides);
        }

        if (mob.Definition.Attacks.Count > 0)
        {
            var primaryAttack = mob.Definition.Attacks[0];
            damage += _combatCalculator.RollDice(primaryAttack.DamageDiceCount, primaryAttack.DamageDiceSides);
            damage += primaryAttack.DamageBonus;
        }
        else if (weaponDetails == null)
        {
            // Fallback for mobs with no attack data
            damage += mob.Definition.Level + _combatCalculator.RollDice(1, 4);
        }

        return damage;
    }

    private ObjectInstance? Wielded(ICombatant combatant) => combatant switch
    {
        PlayerState player when player.EquipmentSlotToObjectId.TryGetValue((int)EquipmentSlot.Wield, out var id) =>
            _worldState.GetObjectInstance(id),
        MobInstance mob when mob.Equipment.TryGetValue(EquipmentSlot.Wield, out var weapon) => weapon,
        _ => null
    };

    private int MaxHitPoints(ICombatant combatant) =>
        combatant is PlayerState player
            ? _worldState.GetTotalEffectiveMaxHitPoints(player)
            : ((MobInstance)combatant).Definition.MaxHitPoints;

    /// <summary>
    /// Increment gain_count for all players based on their position.
    /// This accumulator is used in regeneration formulas.
    /// Legacy: check_gain() in comm.c runs every PULSE_GAIN
    /// </summary>
    private void ProcessGainIncrement()
    {
        var connections = _connectionRegistry.GetConnections().ToList();
        
        foreach (var connection in connections)
        {
            try
            {
                RegenerationService.IncrementGainCount(connection.Player);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Gain] Error incrementing gain for player {connection.Player.Name}: {ex.Message}");
            }
        }
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
                var effectiveMaxHP = _worldState.GetTotalEffectiveMaxHitPoints(connection.Player);
                var effectiveMaxMana = _worldState.GetTotalEffectiveMaxMana(connection.Player);
                var effectiveMaxMove = _worldState.GetTotalEffectiveMaxMovement(connection.Player);
                bool didRegen = RegenerationService.RegeneratePlayer(connection.Player, effectiveMaxHP, effectiveMaxMana, effectiveMaxMove);
                
                if (didRegen)
                {
                    playersRegenerated++;
                }
                
                // Tick affects (decrement duration and remove expired)
                var expiredAffects = connection.Player.TickAffects();
                if (expiredAffects.Count > 0)
                {
                    foreach (var affect in expiredAffects)
                    {
                        // Use custom wear-off message if available, otherwise use generic message
                        var message = affect.WearOffMessage ?? $"The {affect.Type.ToString().ToLowerInvariant()} spell wears off.";
                        _ = connection.Session.SendLineAsync(message, CancellationToken.None);
                    }
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

    /// <summary>
    /// Close activated portals whose open time has run out.
    /// Legacy: portal timeout code in point_update(), limits.c:631
    /// </summary>
    private void ProcessPortalTimeouts()
    {
        var closedPortals = _worldState.TickPortals();
        if (closedPortals.Count == 0)
        {
            return;
        }

        var connections = _connectionRegistry.GetConnections().ToList();
        foreach (var (roomId, portal) in closedPortals)
        {
            var message = $"The {portal.Definition.ShortDescription} closes mysteriously.";
            foreach (var connection in connections.Where(c => c.Player.RoomId == roomId))
            {
                _ = connection.Session.SendLineAsync(message, CancellationToken.None);
            }
        }
    }

    private void ProcessRequestedSaves(CancellationToken cancellationToken)
    {
        foreach (var connection in _connectionRegistry.GetConnections().Where(c => c.Player.SaveRequested).ToList())
        {
            connection.Player.SaveRequested = false;
            var player = connection.Player;
            var saved = _saveQueue.QueueSaveAndWaitAsync(connection.CharacterId, player, cancellationToken);

            // Don't block the tick on the database (the save worker needs the gate this tick holds)
            _ = RetryIfFailedAsync(saved, player, _gate);
        }
    }

    /// <summary>
    /// If a requested save fails, ask again so a later tick retries. The flag is part of the player, so it is set
    /// under the game state gate like any other change (#61).
    /// </summary>
    internal static async Task RetryIfFailedAsync(Task<bool> saved, PlayerState player, GameStateGate gate)
    {
        bool succeeded;
        try
        {
            succeeded = await saved;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Save] Requested save of {player.Name} threw: {ex.Message}");
            succeeded = false;
        }

        if (succeeded)
        {
            return;
        }

        Console.WriteLine($"[Save] Requested save of {player.Name} failed; retrying next tick");
        using (await gate.EnterAsync(CancellationToken.None))
        {
            player.SaveRequested = true;
        }
    }

    private async Task ProcessAutoSaveAsync(CancellationToken cancellationToken)
    {
        var connections = _connectionRegistry.GetConnections().ToList();
        
        if (connections.Count == 0)
        {
            return; // No players to save
        }

        Console.WriteLine($"[AutoSave] Queueing {connections.Count} player(s) for save...");
        
        // Queue all saves (fire-and-forget)
        // The save queue handles deduplication automatically
        foreach (var connection in connections)
        {
            await _saveQueue.QueueSaveAsync(connection.CharacterId, connection.Player, cancellationToken);
        }
        
        Console.WriteLine($"[AutoSave] All saves queued");
    }

    /// <summary>
    /// Process mob AI for all mobs in the world.
    /// Legacy: mobile_activity() in mobact.c:347-386
    /// </summary>
    private void ProcessMobAi()
    {
        // Build player connection dictionary for aggro/memory checks
        var connections = _connectionRegistry.GetConnections()
            .Select(c => new Application.Ai.PlayerConnection 
            { 
                ConnectionId = c.Id, 
                Player = c.Player 
            })
            .ToDictionary(pc => pc.ConnectionId);
        
        // Process each room's mobs
        foreach (var room in _worldState.World.Rooms.Values)
        {
            var mobs = _worldState.GetMobsInRoom(room.Id);
            
            // ToList to avoid modification during iteration (mobs may move to different rooms)
            foreach (var mob in mobs.ToList())
            {
                try
                {
                    _mobAiService.ProcessMobTick(mob, room.Id, _worldState, connections);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MobAI] Error processing mob {mob.InstanceId} in room {room.Id}: {ex.Message}");
                }
            }
        }
    }
    
    /// <summary>
    /// Check if a player can perform an extra attack beyond the primary attack.
    /// Legacy: fight.c:1778-1803 (perform_violence multi-attack loop)
    /// </summary>
    /// <param name="player">The attacking player</param>
    /// <param name="attackNumber">Which attack this is (2-5)</param>
    /// <returns>True if the attack can proceed, false otherwise</returns>
    private bool CanPerformExtraAttack(PlayerState player, int attackNumber)
    {
        SkillType requiredSkill;
        int difficulty;
        
        switch (attackNumber)
        {
            case 2:
                requiredSkill = SkillType.SecondAttack;
                difficulty = 120;
                break;
            case 3:
                requiredSkill = SkillType.ThirdAttack;
                difficulty = 140;
                break;
            case 4:
                requiredSkill = SkillType.FourthAttack;
                difficulty = 160;
                break;
            case 5:
                requiredSkill = SkillType.DualWield;
                difficulty = 160;
                // Dual-wield also requires a weapon in the HOLD slot
                if (!player.EquipmentSlotToObjectId.ContainsKey((int)EquipmentSlot.Hold))
                {
                    return false; // No offhand weapon
                }
                break;
            default:
                return false; // Invalid attack number
        }
        
        // Check if player has this skill (0-100)
        var skillPercent = player.GetSkill(requiredSkill);
        if (skillPercent == 0)
        {
            return false; // Don't have this skill or it's at 0%
        }
        
        // Legacy skill check: number(1, difficulty) < skill_percent
        // Example: 95% skill vs difficulty 120 → random(1,120) < 95 → ~79% chance
        int roll = Random.Shared.Next(1, difficulty + 1);
        return roll < skillPercent;
    }
}
