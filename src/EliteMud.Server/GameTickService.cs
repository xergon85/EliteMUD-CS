using EliteMud.Application.Ai;
using EliteMud.Application.Combat;
using EliteMud.Application.Commands.Flee;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Session;
using EliteMud.Application.World;
using EliteMud.Data;
using EliteMud.Data.Repositories;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Look;
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
    private readonly Application.Commands.Flee.FleeHandler _fleeService;
    private readonly LookCommandHandler _lookHandler;
    private readonly CombatCalculator _combatCalculator;
    private readonly CharacterSaveQueue _saveQueue;
    private readonly MobAiService _mobAiService;
    
    private readonly TimeSpan _combatInterval = TimeSpan.FromSeconds(2); // PULSE_VIOLENCE
    private readonly TimeSpan _gainInterval = TimeSpan.FromSeconds(6); // PULSE_GAIN - increment gain_count based on position (legacy: 6 seconds)
    private readonly TimeSpan _regenInterval = TimeSpan.FromSeconds(75); // SECS_PER_MUD_HOUR - apply regeneration (legacy: 75 seconds)
    private readonly TimeSpan _autoSaveInterval = TimeSpan.FromMinutes(5); // Auto-save every 5 minutes
    
    // Legacy: mortal_start_room = 3001 (Temple of Midgaard)
    // Players respawn here after death
    private const int MortalStartRoom = 3001;
    
    private int _tickCount;
    private DateTime _lastGain = DateTime.UtcNow;
    private DateTime _lastRegen = DateTime.UtcNow;
    private DateTime _lastAutoSave = DateTime.UtcNow;

    public GameTickService(
        ConnectionRegistry connectionRegistry,
        IServiceProvider serviceProvider,
        IWorldState worldState,
        ActMessageService actService,
        Application.Commands.Flee.FleeHandler fleeService,
        LookCommandHandler lookHandler,
        CombatCalculator combatCalculator,
        CharacterSaveQueue saveQueue,
        MobAiService mobAiService)
    {
        _connectionRegistry = connectionRegistry;
        _serviceProvider = serviceProvider;
        _worldState = worldState;
        _actService = actService;
        _fleeService = fleeService;
        _lookHandler = lookHandler;
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
        
        // Decrement wait states for all players (happens every combat tick)
        foreach (var connection in connections)
        {
            connection.Player.DecrementWaitState();
        }
        
        // Find all players in combat
        var fightingPlayers = connections.Where(c => c.Player.FightingConnectionId != null).ToList();
        
        // Process player attacks
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
                        _combatCalculator.StopFighting(attacker.Player);
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
        
        // Process mob attacks (mobs attacking players who can't fight back)
        await ProcessMobAttacksAsync(connections, cancellationToken);
    }

    /// <summary>
    /// Process mobs attacking players (including helpless ones).
    /// </summary>
    private async Task ProcessMobAttacksAsync(
        List<ConnectionContext> connections,
        CancellationToken cancellationToken)
    {
        // Find all mobs that are fighting
        foreach (var roomId in _worldState.World.Rooms.Keys)
        {
            var mobs = _worldState.GetMobsInRoom(roomId);
            foreach (var mob in mobs)
            {
                if (mob.FightingConnectionId == null || mob.HitPoints <= 0)
                {
                    continue; // Not fighting or dead
                }

                // Find the player this mob is fighting
                var victim = connections.FirstOrDefault(c => c.Id == mob.FightingConnectionId.Value);
                if (victim == null || victim.Player.RoomId != roomId)
                {
                    // Player left or disconnected
                    mob.FightingConnectionId = null;
                    mob.Position = Position.Standing;
                    continue;
                }

                // Mob attacks the player (even if player is helpless)
                try
                {
                    await ProcessMobAttackOnPlayerAsync(mob, victim, cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Combat] Error processing mob attack: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Handle a mob attacking a player (used when player is helpless/stunned).
    /// </summary>
    private async Task ProcessMobAttackOnPlayerAsync(
        MobInstance mob,
        ConnectionContext victim,
        CancellationToken cancellationToken)
    {
        // Calculate mob damage (legacy: fight.c:1440-1473)
        // Base: str_todam + damroll
        int damage = 0;
        
        // Get mob's effective damroll (base + equipment + affects)
        int mobDamroll = WorldStateExtensions.GetMobEffectiveDamroll(mob);
        damage += mobDamroll;
        
        // Get wielded weapon
        ObjectWeapon? weaponDetails = null;
        ObjectInstance? weapon = null;
        if (mob.Equipment.TryGetValue(EquipmentSlot.Wield, out weapon))
        {
            weaponDetails = weapon.Definition.Details?.Weapon;
        }
        
        if (weaponDetails != null)
        {
            // Wielded weapon: dam += dice(weapon.DiceCount, weapon.DiceSides)
            damage += _combatCalculator.RollDice(weaponDetails.DiceCount, weaponDetails.DiceSides);
            
            // Mobs ALSO add their natural attack dice when wielding weapons (legacy: fight.c:1471)
            if (mob.Definition.Attacks.Count > 0)
            {
                var primaryAttack = mob.Definition.Attacks[0]; // Use first attack
                damage += _combatCalculator.RollDice(primaryAttack.DamageDiceCount, primaryAttack.DamageDiceSides);
                damage += primaryAttack.DamageBonus;
            }
            
            // Apply weapon special effects (bless/evil/flame) for mobs too!
            if (weapon != null)
            {
                int weaponEffectDamage = _combatCalculator.ApplyWeaponEffects(weapon.Definition.Flags, victim.Player);
                damage += weaponEffectDamage;
            }
        }
        else
        {
            // No weapon: use natural attacks (legacy: fight.c:1443-1444)
            if (mob.Definition.Attacks.Count > 0)
            {
                var primaryAttack = mob.Definition.Attacks[0]; // Use first attack
                damage += _combatCalculator.RollDice(primaryAttack.DamageDiceCount, primaryAttack.DamageDiceSides);
                damage += primaryAttack.DamageBonus;
            }
            else
            {
                // Fallback for mobs with no attack data
                damage += mob.Definition.Level + Random.Shared.Next(1, 5);
            }
        }
        
        var damageResult = _combatCalculator.ApplyDamage(victim.Player, damage);
        
        // Show dodge message if dodged
        if (damageResult.Dodged && !string.IsNullOrEmpty(damageResult.Message))
        {
            await victim.Session.SendLineAsync(damageResult.Message, cancellationToken);
        }
        
        // Format legacy combat messages
        var victimEffectiveMaxHp = _worldState.GetTotalEffectiveMaxHitPoints(victim.Player);
        var victimMsg = CombatMessageFormatter.FormatCombatMessage(
            mob.Definition.ShortDescription,
            victim.Player.Name,
            damageResult.Damage,
            victimEffectiveMaxHp,
            MessagePerspective.ToVict);
        
        await victim.Session.SendLineAsync(
            $"{victimMsg} [{victim.Player.HitPoints}/{victimEffectiveMaxHp} HP]",
            cancellationToken);
        
        // Show damage feedback (HURT/bleeding messages)
        var feedbackMsg = CombatMessageFormatter.GetDamageFeedbackMessage(
            victimEffectiveMaxHp, 
            victim.Player.HitPoints, 
            damageResult.Damage);
        if (feedbackMsg != null)
        {
            await victim.Session.SendLineAsync(feedbackMsg, cancellationToken);
        }
            
        // Broadcast to room
        var roomMsg = CombatMessageFormatter.FormatCombatMessage(
            mob.Definition.ShortDescription,
            victim.Player.Name,
            damageResult.Damage,
            victimEffectiveMaxHp,
            MessagePerspective.ToRoom);
            
        var otherPlayers = _connectionRegistry.GetConnections()
            .Where(c => c.Player.RoomId == victim.Player.RoomId && c.Id != victim.Id);
        
        foreach (var observer in otherPlayers)
        {
            await observer.Session.SendLineAsync(roomMsg, cancellationToken);
        }

        // Check player position after mob attack
        await CheckPlayerPositionAsync(victim, mob, cancellationToken);
    }

    private async Task ProcessPlayerVsPlayerAttack(
        ConnectionContext attacker,
        ConnectionContext victim,
        CancellationToken cancellationToken)
    {
        // Double-check that both players are still in fighting state
        // (guards against race conditions with kick/kill commands)
        if (attacker.Player.Position < Position.Fighting || attacker.Player.FightingConnectionId == null)
        {
            return;
        }
        
        if (victim.Player.Position == Position.Dead)
        {
            // Victim died (possibly from concurrent kick command)
            _combatCalculator.StopFighting(attacker.Player);
            return;
        }
        
        // Get wielded weapon for damage calculation
        ObjectWeapon? weaponDetails = null;
        ObjectInstance? weapon = null;
        if (attacker.Player.EquipmentSlotToObjectId.TryGetValue((int)EquipmentSlot.Wield, out var weaponInstanceId))
        {
            weapon = _worldState.GetObjectInstance(weaponInstanceId);
            weaponDetails = weapon?.Definition.Details?.Weapon;
        }
        
        // Calculate effective stats (base + equipment + spell bonuses)
        var attackerEffectiveStr = _worldState.GetTotalEffectiveStrength(attacker.Player);
        var attackerEffectiveInt = _worldState.GetTotalEffectiveIntelligence(attacker.Player);
        var attackerEffectiveWis = _worldState.GetTotalEffectiveWisdom(attacker.Player);
        var victimEffectiveDex = _worldState.GetTotalEffectiveDexterity(victim.Player);
        
        var result = _combatCalculator.PerformAttack(
            attacker.Player,
            attackerEffectiveStr,
            attackerEffectiveInt,
            attackerEffectiveWis,
            victim.Player,
            victimEffectiveDex,
            weaponDetails);
        
        // Apply weapon special effects (bless/evil/flame) to damage
        int totalDamage = result.Damage;
        if (weapon != null && result.Hit)
        {
            int weaponEffectDamage = _combatCalculator.ApplyWeaponEffects(weapon.Definition.Flags, victim.Player);
            totalDamage += weaponEffectDamage;
            // Apply additional damage to victim
            if (weaponEffectDamage > 0)
            {
                victim.Player.HitPoints -= (short)weaponEffectDamage;
                _combatCalculator.UpdatePosition(victim.Player);
            }
        }
        
        // Format legacy combat messages
        var victimEffectiveMaxHp = _worldState.GetTotalEffectiveMaxHitPoints(victim.Player);
        var attackerMsg = CombatMessageFormatter.FormatCombatMessage(
            attacker.Player.Name,
            victim.Player.Name,
            totalDamage,
            victimEffectiveMaxHp,
            MessagePerspective.ToChar);
            
        var victimMsg = CombatMessageFormatter.FormatCombatMessage(
            attacker.Player.Name,
            victim.Player.Name,
            totalDamage,
            victimEffectiveMaxHp,
            MessagePerspective.ToVict);
        
        // Send messages
        await attacker.Session.SendLineAsync(attackerMsg, cancellationToken);
        await victim.Session.SendLineAsync(
            $"{victimMsg} [{victim.Player.HitPoints}/{victimEffectiveMaxHp} HP]", 
            cancellationToken);
        
        // Show damage feedback to victim (HURT/bleeding messages)
        if (result.Hit && totalDamage > 0)
        {
            var feedbackMsg = CombatMessageFormatter.GetDamageFeedbackMessage(
                victimEffectiveMaxHp,
                victim.Player.HitPoints,
                totalDamage);
            if (feedbackMsg != null)
            {
                await victim.Session.SendLineAsync(feedbackMsg, cancellationToken);
            }
        }

        // Broadcast to room if hit
        if (result.Hit)
        {
            var roomMsg = CombatMessageFormatter.FormatCombatMessage(
                attacker.Player.Name,
                victim.Player.Name,
                totalDamage,
                victimEffectiveMaxHp,
                MessagePerspective.ToRoom);
                
            var otherPlayers = _connectionRegistry.GetConnections()
                .Where(c => c.Player.RoomId == attacker.Player.RoomId 
                         && c.Id != attacker.Id 
                         && c.Id != victim.Id);
            
            foreach (var observer in otherPlayers)
            {
                await observer.Session.SendLineAsync(roomMsg, cancellationToken);
            }

            // Award experience
            attacker.Player.Experience += _combatCalculator.CalculateExperienceGain(victim.Player, totalDamage);

            // Check if victim died
            if (victim.Player.Position == Position.Dead)
            {
                await HandlePlayerDeath(attacker, victim, cancellationToken);
            }
        }
    }

    private async Task ProcessPlayerVsMobAttack(
        ConnectionContext attacker,
        int mobInstanceId,
        CancellationToken cancellationToken)
    {
        // Double-check that player is still in fighting state
        // (guards against race conditions with commands)
        if (attacker.Player.Position < Position.Fighting || attacker.Player.FightingConnectionId == null)
        {
            return;
        }
        
        // Find the mob in the player's room
        var mobs = _worldState.GetMobsInRoom(attacker.Player.RoomId);
        var mob = mobs.FirstOrDefault(m => m.InstanceId == mobInstanceId);

        if (mob == null)
        {
            // Mob is gone (killed by someone else, or despawned)
            _combatCalculator.StopFighting(attacker.Player);
            await attacker.Session.SendLineAsync("Your opponent has left.", cancellationToken);
            return;
        }

        // Get mob's max HP from definition
        int mobMaxHp = mob.Definition.MaxHitPoints;
        
        // Get wielded weapon for damage calculation
        ObjectWeapon? weaponDetails = null;
        ObjectInstance? weapon = null;
        if (attacker.Player.EquipmentSlotToObjectId.TryGetValue((int)EquipmentSlot.Wield, out var weaponInstanceId))
        {
            weapon = _worldState.GetObjectInstance(weaponInstanceId);
            weaponDetails = weapon?.Definition.Details?.Weapon;
        }
        
        // Calculate effective STR (base + equipment + spell bonuses)
        var attackerEffectiveStr = _worldState.GetTotalEffectiveStrength(attacker.Player);
        
        // Player attacks mob (using effective STR for damage)
        int damage = _combatCalculator.CalculateDamage(attacker.Player, attackerEffectiveStr, weaponDetails);
        
        // Apply weapon special effects (bless/evil/flame)
        if (weapon != null)
        {
            int weaponEffectDamage = _combatCalculator.ApplyWeaponEffects(weapon.Definition.Flags, mob);
            damage += weaponEffectDamage;
        }
        
        mob.HitPoints -= (short)damage;
        
        // MOB_MEMORY: mob remembers this attacker
        // Legacy: fight.c:824-827 - remember_attack() called when hit
        if (mob.Definition.ParsedFlags.HasFlag(MobFlags.Memory))
        {
            mob.RememberPlayer(attacker.Player.Id);
        }
        
        // Format legacy combat messages for player hitting mob
        var attackerMsg = CombatMessageFormatter.FormatCombatMessage(
            attacker.Player.Name,
            mob.Definition.ShortDescription,
            damage,
            mobMaxHp,
            MessagePerspective.ToChar);
            
        await attacker.Session.SendLineAsync(attackerMsg, cancellationToken);
        
        // Broadcast to room
        var roomMsg = CombatMessageFormatter.FormatCombatMessage(
            attacker.Player.Name,
            mob.Definition.ShortDescription,
            damage,
            mobMaxHp,
            MessagePerspective.ToRoom);
            
        var otherPlayers = _connectionRegistry.GetConnections()
            .Where(c => c.Player.RoomId == attacker.Player.RoomId && c.Id != attacker.Id);
        
        foreach (var observer in otherPlayers)
        {
            await observer.Session.SendLineAsync(roomMsg, cancellationToken);
        }

        // Award experience
        attacker.Player.Experience += mob.Definition.Level * damage / 2;

        // Check if mob died
        if (mob.HitPoints <= 0)
        {
            await HandleMobDeath(attacker, mob, cancellationToken);
        }
        else
        {
            // Mob fights back
            var mobDamage = mob.Definition.Level + Random.Shared.Next(1, 5);
            var mobDamageResult = _combatCalculator.ApplyDamage(attacker.Player, mobDamage);
            
            // Show dodge message if dodged
            if (mobDamageResult.Dodged && !string.IsNullOrEmpty(mobDamageResult.Message))
            {
                await attacker.Session.SendLineAsync(mobDamageResult.Message, cancellationToken);
            }
            
            // Format legacy combat messages for mob hitting player
            var attackerEffectiveMaxHp = _worldState.GetTotalEffectiveMaxHitPoints(attacker.Player);
            var mobAttackMsg = CombatMessageFormatter.FormatCombatMessage(
                mob.Definition.ShortDescription,
                attacker.Player.Name,
                mobDamageResult.Damage,
                attackerEffectiveMaxHp,
                MessagePerspective.ToVict);
            
            await attacker.Session.SendLineAsync(
                $"{mobAttackMsg} [{attacker.Player.HitPoints}/{attackerEffectiveMaxHp} HP]",
                cancellationToken);
            
            // Show damage feedback (HURT/bleeding messages)
            var feedbackMsg = CombatMessageFormatter.GetDamageFeedbackMessage(
                attackerEffectiveMaxHp,
                attacker.Player.HitPoints,
                mobDamageResult.Damage);
            if (feedbackMsg != null)
            {
                await attacker.Session.SendLineAsync(feedbackMsg, cancellationToken);
            }
                
            // Broadcast mob attack to room
            var mobRoomMsg = CombatMessageFormatter.FormatCombatMessage(
                mob.Definition.ShortDescription,
                attacker.Player.Name,
                mobDamageResult.Damage,
                attackerEffectiveMaxHp,
                MessagePerspective.ToRoom);
                
            foreach (var observer in otherPlayers)
            {
                await observer.Session.SendLineAsync(mobRoomMsg, cancellationToken);
            }

            // Check player position after mob attack
            await CheckPlayerPositionAsync(attacker, mob, cancellationToken);
        }
    }

    private async Task HandlePlayerDeath(
        ConnectionContext killer,
        ConnectionContext victim,
        CancellationToken cancellationToken)
    {
        // Stop combat
        _combatCalculator.StopFighting(killer.Player);
        _combatCalculator.StopFighting(victim.Player);

        // Award full experience (bonus for kill)
        int killBonus = victim.Player.Level * 100;
        killer.Player.Experience += killBonus;

        // PvP kills always shift killer toward evil
        int alignmentShift = _combatCalculator.CalculateAlignmentShift(killer.Player, victim.Player, isPvP: true);
        _combatCalculator.ApplyAlignmentShift(killer.Player, alignmentShift);

        // Messages
        await killer.Session.SendLineAsync(
            $"You have slain {victim.Player.Name}! (+{killBonus} exp)", cancellationToken);
        await killer.Session.SendLineAsync(
            $"#rYour soul darkens from the murder.#N ({alignmentShift})", cancellationToken);
        await victim.Session.SendLineAsync(
            "You are dead!  Sorry...", cancellationToken);

        // Broadcast to room
        var roomMessage = $"{victim.Player.Name} is dead! R.I.P.";
        var otherPlayers = _connectionRegistry.GetConnections()
            .Where(c => c.Player.RoomId == victim.Player.RoomId && c.Id != killer.Id && c.Id != victim.Id);
        
        foreach (var observer in otherPlayers)
        {
            await observer.Session.SendLineAsync(roomMessage, cancellationToken);
        }

        // Create corpse (fight.c:530)
        _worldState.CreatePlayerCorpse(victim.Player, victim.Player.RoomId);

        // Death experience loss - lose half of what's needed to level (fight.c:541)
        // TODO: Implement exp_needed() function for proper calculation
        // For now, use a simple penalty of 10% of current experience
        int expLoss = victim.Player.Experience / 10;
        victim.Player.Experience = Math.Max(0, victim.Player.Experience - expLoss);

        // Respawn the player at Temple of Midgaard
        victim.Player.HitPoints = _worldState.GetTotalEffectiveMaxHitPoints(victim.Player);
        victim.Player.Mana = _worldState.GetTotalEffectiveMaxMana(victim.Player);
        victim.Player.Movement = _worldState.GetTotalEffectiveMaxMovement(victim.Player);
        victim.Player.Position = Position.Standing;
        victim.Player.RoomId = MortalStartRoom;
        
        await victim.Session.SendLineAsync(
            $"You have been resurrected... (-{expLoss} exp)", cancellationToken);
    }

    private async Task HandlePlayerDeathFromMob(
        ConnectionContext victim,
        MobInstance mob,
        CancellationToken cancellationToken)
    {
        // Stop combat
        _combatCalculator.StopFighting(victim.Player);
        mob.FightingConnectionId = null;
        mob.Position = Position.Standing;

        // Messages (fight.c:966-969)
        await victim.Session.SendLineAsync(
            "You are dead!  Sorry...", cancellationToken);

        // Broadcast to room (fight.c:967)
        var roomMessage = $"{victim.Player.Name} is dead! R.I.P.";
        var otherPlayers = _connectionRegistry.GetConnections()
            .Where(c => c.Player.RoomId == victim.Player.RoomId && c.Id != victim.Id);
        
        foreach (var observer in otherPlayers)
        {
            await observer.Session.SendLineAsync(roomMessage, cancellationToken);
        }

        // Create corpse (fight.c:530)
        _worldState.CreatePlayerCorpse(victim.Player, victim.Player.RoomId);

        // Death experience loss - lose half of what's needed to level (fight.c:541)
        // TODO: Implement exp_needed() function for proper calculation
        // For now, use a simple penalty of 10% of current experience
        int expLoss = victim.Player.Experience / 10;
        victim.Player.Experience = Math.Max(0, victim.Player.Experience - expLoss);

        // Respawn the player at Temple of Midgaard
        victim.Player.HitPoints = _worldState.GetTotalEffectiveMaxHitPoints(victim.Player);
        victim.Player.Mana = _worldState.GetTotalEffectiveMaxMana(victim.Player);
        victim.Player.Movement = _worldState.GetTotalEffectiveMaxMovement(victim.Player);
        victim.Player.Position = Position.Standing;
        victim.Player.RoomId = MortalStartRoom;
        
        await victim.Session.SendLineAsync(
            $"You have been resurrected... (-{expLoss} exp)", cancellationToken);
    }

    /// <summary>
    /// Check player's position after taking damage and send appropriate messages.
    /// Also handle auto-flee (wimpy) if HP drops below player's wimpy threshold.
    /// Legacy: update_pos() with position-based messages, fight.c:987-992
    /// </summary>
    private async Task CheckPlayerPositionAsync(
        ConnectionContext player,
        MobInstance mob,
        CancellationToken cancellationToken)
    {
        var position = player.Player.Position;
        
        // Auto-flee if HP drops below wimpy level - Legacy: fight.c:987-992
        // Only flee if wimpy is set and not already incapacitated/mortally wounded/stunned
        if (position >= Position.Fighting && 
            player.Player.WimpyLevel > 0 &&
            player.Player.HitPoints > 0 &&
            player.Player.HitPoints < player.Player.WimpyLevel)
        {
            await player.Session.SendLineAsync(
                "You wimp out, and attempt to flee!", 
                cancellationToken);
            
            // Attempt to flee
            await AttemptAutoFleeAsync(player, cancellationToken);
            return; // Don't process other messages if fleeing
        }
        
        if (position == Position.Dead)
        {
            // Player is dead
            await HandlePlayerDeathFromMob(player, mob, cancellationToken);
        }
        else if (position == Position.MortallyWounded)
        {
            // Mortally wounded (-6 to -10 HP)
            await player.Session.SendLineAsync(
                "You are mortally wounded, and will die soon, if not aided.", 
                cancellationToken);
            
            // Stop player from fighting, but mob keeps attacking
            _combatCalculator.StopFighting(player.Player);
        }
        else if (position == Position.Incapacitated)
        {
            // Incapacitated (-3 to -5 HP)
            await player.Session.SendLineAsync(
                "You are incapacitated and will slowly die, if not aided.", 
                cancellationToken);
            
            // Stop player from fighting, but mob keeps attacking
            _combatCalculator.StopFighting(player.Player);
        }
        else if (position == Position.Stunned)
        {
            // Stunned (0 to -2 HP)
            await player.Session.SendLineAsync(
                "You are stunned, but will probably regain consciousness again.", 
                cancellationToken);
            
            // Stop player from fighting, but mob keeps attacking
            _combatCalculator.StopFighting(player.Player);
        }
        // else: player is still conscious and fighting
    }

    /// <summary>
    /// Attempt to auto-flee (wimpy).
    /// Legacy: fight.c:987-992 calls do_flee()
    /// </summary>
    private async Task AttemptAutoFleeAsync(
        ConnectionContext player,
        CancellationToken cancellationToken)
    {
        var currentRoomId = player.Player.RoomId;

        // Attempt to flee using FleeHandler
        var result = _fleeService.AttemptFlee(
            player.Player,
            currentRoomId,
            () => _connectionRegistry.GetConnections().Select(c => c.Player),
            () => _worldState.GetMobsInRoom(currentRoomId));

        if (!result.Success)
        {
            // No valid exits found
            await player.Session.SendLineAsync("PANIC!  You couldn't escape!", cancellationToken);
            return;
        }

        // Apply the flee result (moves player, stops combat, applies XP loss)
        _fleeService.ApplyFleeResult(
            player.Player,
            result,
            () => _connectionRegistry.GetConnections().Select(c => c.Player),
            player.Id);

        // Send success message
        await player.Session.SendLineAsync("You flee head over heels.", cancellationToken);

        // Show new room using LookCommandHandler (same as manual flee and move)
        await _lookHandler.HandleAsync(
            new CommandRequest("look", null, null),
            player,
            cancellationToken);
    }

    private async Task HandleMobDeath(
        ConnectionContext killer,
        MobInstance mob,
        CancellationToken cancellationToken)
    {
        // Stop combat
        _combatCalculator.StopFighting(killer.Player);
        mob.FightingConnectionId = null;

        // Award kill experience
        int killBonus = mob.Definition.Level * 100;
        killer.Player.Experience += killBonus;

        // Calculate and apply alignment shift
        int alignmentShift = _combatCalculator.CalculateAlignmentShift(killer.Player, mob, isPvP: false);
        if (alignmentShift != 0)
        {
            _combatCalculator.ApplyAlignmentShift(killer.Player, alignmentShift);
        }

        // Messages
        var mobDesc = mob.Definition.ShortDescription?.Trim() ?? "something";
        string alignmentMsg = alignmentShift switch
        {
            >= 50 => $" #GYou feel more virtuous.#N ({alignmentShift:+0})",
            <= -50 => $" #RYou feel more sinister.#N ({alignmentShift})",
            _ => ""
        };
        
        await killer.Session.SendLineAsync(
            $"You have slain {mobDesc}! (+{killBonus} exp){alignmentMsg}", cancellationToken);

        // Broadcast to room (fight.c:967)
        var roomMessage = $"{mobDesc} is dead!";
        var otherPlayers = _connectionRegistry.GetConnections()
            .Where(c => c.Player.RoomId == killer.Player.RoomId && c.Id != killer.Id);
        
        foreach (var observer in otherPlayers)
        {
            await observer.Session.SendLineAsync(roomMessage, cancellationToken);
        }

        // Create corpse (fight.c:501)
        _worldState.CreateMobCorpse(mob, killer.Player.RoomId);

        // Remove mob from world (fight.c:502 - extract_char)
        _worldState.RemoveMob(mob.InstanceId, killer.Player.RoomId);
    }

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
}
