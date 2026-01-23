using EliteMud.Application.Commands.Shared;
using EliteMud.Application.Spells;
using EliteMud.Application.World;
using EliteMud.Game;
using EliteMud.Server.Adapters.Commands.Shared;

namespace EliteMud.Server.Adapters.Commands.Cast;

/// <summary>
/// Handles the 'cast' command for spell casting.
/// Supports: cast 'spell name' [target] OR cast #id [target]
/// Examples:
///   cast 'magic missile'
///   cast 1 guard
///   cast 'cure light wounds' bob
///   cast 28 (heals self)
/// </summary>
[Command("cast", Aliases = new[] { "c", "ca" })]
internal sealed class CastCommandHandler : ICommandHandler
{
    private readonly SpellRegistry _spellRegistry;
    private readonly SpellMetadataRegistry _metadataRegistry;
    private readonly IWorldState _worldState;
    private readonly ConnectionRegistry _connectionRegistry;
    private readonly ActMessageService _actService;

    public CastCommandHandler(
        SpellRegistry spellRegistry,
        SpellMetadataRegistry metadataRegistry,
        IWorldState worldState,
        ConnectionRegistry connectionRegistry,
        ActMessageService actService)
    {
        _spellRegistry = spellRegistry;
        _metadataRegistry = metadataRegistry;
        _worldState = worldState;
        _connectionRegistry = connectionRegistry;
        _actService = actService;
    }

    public async ValueTask<CommandOutcome> HandleAsync(
        CommandRequest command,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var caster = context.Player;
        var args = command.Argument?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(args))
        {
            await context.Session.SendLineAsync("Cast which spell? (usage: cast 'spell name' or cast #id)", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Parse spell name/ID and optional target
        var (spellIdentifier, targetName) = ParseCastCommand(args);
        if (string.IsNullOrEmpty(spellIdentifier))
        {
            await context.Session.SendLineAsync("Cast which spell? (usage: cast 'spell name' or cast #id)", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Look up spell metadata by name, alias, or ID
        SpellMetadata? metadata = null;

        // Try parsing as ID first (e.g., "cast 15" for armor)
        if (int.TryParse(spellIdentifier, out var spellId))
        {
            metadata = _metadataRegistry.GetById(spellId);
        }
        else
        {
            // Try name/alias lookup
            metadata = _metadataRegistry.GetByName(spellIdentifier) ?? _metadataRegistry.GetByAlias(spellIdentifier);
        }

        if (metadata == null)
        {
            await context.Session.SendLineAsync($"You don't know a spell called '{spellIdentifier}'.", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Get spell handler (convert metadata ID to SpellType enum)
        var spellType = (SpellType)metadata.Id;
        if (!_spellRegistry.TryGetSpell(spellType, out var spell))
        {
            await context.Session.SendLineAsync($"The spell '{metadata.Name}' is not implemented yet.", cancellationToken);
            return CommandOutcome.Continue;
        }

        // Check if caster can cast this spell
        if (!spell.CanCast(caster))
        {
            await context.Session.SendLineAsync(spell.GetCannotCastMessage(caster), cancellationToken);
            return CommandOutcome.Continue;
        }

        // Resolve target based on spell target type
        var (target, targetConnectionId) = await ResolveTargetAsync(spell, targetName, context, cancellationToken);
        if (target == null)
        {
            // Error message already sent
            return CommandOutcome.Continue;
        }

        // Deduct mana
        caster.Mana -= (short)spell.ManaCost;

        // Cast the spell
        await CastSpellAsync(spell, metadata, caster, target, context, targetConnectionId, cancellationToken);

        // Apply wait state
        caster.WaitState = spell.WaitStateRounds;

        return CommandOutcome.Continue;
    }

    /// <summary>
    /// Parse cast command into spell identifier and optional target.
    /// Examples:
    ///   "1" -> ("1", null)
    ///   "1 guard" -> ("1", "guard")
    ///   "'magic missile'" -> ("magic missile", null)
    ///   "'magic missile' guard" -> ("magic missile", "guard")
    /// </summary>
    private (string? SpellIdentifier, string? TargetName) ParseCastCommand(string args)
    {
        if (args.StartsWith('\''))
        {
            // Quoted spell name
            var closeQuote = args.IndexOf('\'', 1);
            if (closeQuote == -1)
            {
                return (null, null); // Unclosed quote
            }

            var spellName = args.Substring(1, closeQuote - 1).Trim();
            var remainder = args.Substring(closeQuote + 1).Trim();
            return (spellName, string.IsNullOrEmpty(remainder) ? null : remainder);
        }
        else
        {
            // Unquoted (ID or single-word spell name)
            var parts = args.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var spellIdentifier = parts[0];
            var targetName = parts.Length > 1 ? parts[1] : null;
            return (spellIdentifier, targetName);
        }
    }

    /// <summary>
    /// Resolve spell target based on spell's TargetType and optional target name.
    /// </summary>
    private async Task<(ICombatant? Target, int? TargetConnectionId)> ResolveTargetAsync(
        ISpellHandler spell,
        string? targetName,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var caster = context.Player;

        switch (spell.TargetType)
        {
            case SpellTargetType.Self:
                return (caster, context.Id);

            case SpellTargetType.SingleEnemy:
                return await ResolveSingleEnemyTargetAsync(targetName, context, cancellationToken);

            case SpellTargetType.SingleAlly:
                return await ResolveSingleAllyTargetAsync(targetName, context, cancellationToken);

            case SpellTargetType.AreaEnemy:
            case SpellTargetType.AreaAlly:
            case SpellTargetType.Room:
                await context.Session.SendLineAsync("Area spells are not yet implemented.", cancellationToken);
                return (null, null);

            default:
                await context.Session.SendLineAsync("Unknown spell target type.", cancellationToken);
                return (null, null);
        }
    }

    /// <summary>
    /// Resolve target for offensive spells (SingleEnemy).
    /// If no target specified, uses current fighting opponent.
    /// </summary>
    private async Task<(ICombatant? Target, int? TargetConnectionId)> ResolveSingleEnemyTargetAsync(
        string? targetName,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var caster = context.Player;

        // If no target specified, use current fighting opponent
        if (string.IsNullOrWhiteSpace(targetName))
        {
            if (caster.FightingConnectionId == null)
            {
                await context.Session.SendLineAsync("Cast this spell on whom?", cancellationToken);
                return (null, null);
            }

            // Find current fighting target
            if (caster.FightingConnectionId.Value > 0)
            {
                // Fighting a player
                var targetConnection = _connectionRegistry.GetConnections()
                    .FirstOrDefault(c => c.Id == caster.FightingConnectionId.Value);
                if (targetConnection != null)
                {
                    return (targetConnection.Player, (int?)targetConnection.Id);
                }
            }
            else
            {
                // Fighting a mob
                var mobInstanceId = -caster.FightingConnectionId.Value;
                var mob = _worldState.GetMobsInRoom(caster.RoomId).FirstOrDefault(m => m.InstanceId == mobInstanceId);
                if (mob != null)
                {
                    return (mob, null);
                }
            }

            await context.Session.SendLineAsync("They aren't here.", cancellationToken);
            return (null, null);
        }

        // Target specified - find in room
        return await FindTargetInRoomAsync(targetName, context, cancellationToken);
    }

    /// <summary>
    /// Resolve target for healing/buff spells (SingleAlly).
    /// If no target specified, targets self.
    /// </summary>
    private async Task<(ICombatant? Target, int? TargetConnectionId)> ResolveSingleAllyTargetAsync(
        string? targetName,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var caster = context.Player;

        // If no target specified, target self
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return (caster, context.Id);
        }

        // Target specified - find in room (allow targeting self by name too)
        return await FindTargetInRoomAsync(targetName, context, cancellationToken);
    }

    /// <summary>
    /// Find a target (player or mob) in the room by name.
    /// </summary>
    private async Task<(ICombatant? Target, int? TargetConnectionId)> FindTargetInRoomAsync(
        string targetName,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var caster = context.Player;

        // Check for players in room (including self)
        var targetPlayer = _connectionRegistry.GetConnections()
            .FirstOrDefault(c => c.Player.RoomId == caster.RoomId
                                 && c.Player.Name.Contains(targetName, StringComparison.OrdinalIgnoreCase));

        if (targetPlayer != null)
        {
            return (targetPlayer.Player, (int?)targetPlayer.Id);
        }

        // Check for mobs in room
        var targetMob = _worldState.GetMobsInRoom(caster.RoomId)
            .FirstOrDefault(m => m.Definition.ShortDescription.Contains(targetName, StringComparison.OrdinalIgnoreCase));

        if (targetMob != null)
        {
            return (targetMob, null);
        }

        await context.Session.SendLineAsync($"You don't see '{targetName}' here.", cancellationToken);
        return (null, null);
    }

    /// <summary>
    /// Execute the spell and send appropriate messages.
    /// </summary>
    private async Task CastSpellAsync(
        ISpellHandler spell,
        SpellMetadata metadata,
        PlayerState caster,
        ICombatant target,
        ConnectionContext context,
        int? targetConnectionId,
        CancellationToken cancellationToken)
    {
        // Check if spell succeeds
        var success = spell.RollSuccess(caster, target);
        if (!success)
        {
            await context.Session.SendLineAsync($"You try to cast {metadata.Name}, but fail!", cancellationToken);
            await BroadcastToRoomAsync(context, "$n tries to cast a spell, but fails!", cancellationToken);
            return;
        }

        // Apply spell effects
        var damage = spell.CalculateDamage(caster, target);
        var healing = spell.CalculateHealing(caster, target);
        var affects = spell.CreateAffects(caster, target);

        if (damage > 0)
        {
            await CastDamageSpellAsync(spell, metadata, caster, target, damage, context, targetConnectionId, cancellationToken);
        }
        else if (healing > 0)
        {
            await CastHealingSpellAsync(spell, metadata, caster, target, healing, context, targetConnectionId, cancellationToken);
        }
        else if (affects.Count > 0)
        {
            // Buff/debuff spell - apply affects
            await CastAffectSpellAsync(spell, metadata, caster, target, affects, context, targetConnectionId, cancellationToken);
        }
        else
        {
            // Unknown spell type (no damage, healing, or affects)
            await context.Session.SendLineAsync($"You cast {metadata.Name}.", cancellationToken);
            await BroadcastToRoomAsync(context, $"$n casts {metadata.Name}.", cancellationToken);
        }

        // Try to improve spell proficiency
        var spellType = (SpellType)metadata.Id;
        if (caster.TryImproveSpell(spellType))
        {
            await context.Session.SendLineAsync($"Your proficiency in {metadata.Name} has improved!", cancellationToken);
        }
    }

    /// <summary>
    /// Cast a damage spell and apply damage to target.
    /// </summary>
    private async Task CastDamageSpellAsync(
        ISpellHandler spell,
        SpellMetadata metadata,
        PlayerState caster,
        ICombatant target,
        int damage,
        ConnectionContext context,
        int? targetConnectionId,
        CancellationToken cancellationToken)
    {
        // Start combat if not already fighting
        if (caster.FightingConnectionId == null && target != caster)
        {
            if (targetConnectionId != null)
            {
                // PvP
                var targetPlayer = (PlayerState)target;
                caster.FightingConnectionId = targetConnectionId.Value;
                targetPlayer.FightingConnectionId = (int?)context.Id;
            }
            else
            {
                // PvE
                var mobInstance = (MobInstance)target;
                caster.FightingConnectionId = -mobInstance.InstanceId;
                mobInstance.FightingConnectionId = (int?)context.Id;
            }
        }

        // Apply damage
        var victimDied = false;

        if (targetConnectionId != null)
        {
            // Player victim
            var targetPlayer = (PlayerState)target;
            targetPlayer.HitPoints -= (short)damage;

            if (targetPlayer.HitPoints <= 0)
            {
                targetPlayer.Position = Position.Dead;
                targetPlayer.HitPoints = 0;
                victimDied = true;
                caster.FightingConnectionId = null;
                targetPlayer.FightingConnectionId = null;
            }
        }
        else
        {
            // Mob victim
            var mobInstance = (MobInstance)target;
            mobInstance.HitPoints -= (short)damage;

            if (mobInstance.HitPoints <= 0)
            {
                mobInstance.Position = Position.Dead;
                victimDied = true;
                caster.FightingConnectionId = null;
                mobInstance.FightingConnectionId = null;

                _worldState.CreateMobCorpse(mobInstance, caster.RoomId);
                _worldState.RemoveMob(mobInstance.InstanceId, caster.RoomId);
            }
        }

        // Send messages
        await context.Session.SendLineAsync(
            $"Your {metadata.Name} hits {target.Name}! [{damage}]",
            cancellationToken);

        if (targetConnectionId != null)
        {
            var targetConnection = _connectionRegistry.GetConnections().FirstOrDefault(c => c.Id == targetConnectionId.Value);
            if (targetConnection != null)
            {
                await targetConnection.Session.SendLineAsync(
                    $"{caster.Name} casts {metadata.Name} at you! [{damage}]",
                    cancellationToken);
            }
        }

        await BroadcastToRoomExceptAsync(
            context,
            targetConnectionId,
            $"$n casts {metadata.Name} at {target.Name}! [{damage}]",
            cancellationToken);

        if (victimDied)
        {
            await context.Session.SendLineAsync($"{target.Name} is DEAD!!", cancellationToken);
            await BroadcastToRoomAsync(context, $"{target.Name} is dead! R.I.P.", cancellationToken);
        }
    }

    /// <summary>
    /// Cast a healing spell and apply healing to target.
    /// </summary>
    private async Task CastHealingSpellAsync(
        ISpellHandler spell,
        SpellMetadata metadata,
        PlayerState caster,
        ICombatant target,
        int healing,
        ConnectionContext context,
        int? targetConnectionId,
        CancellationToken cancellationToken)
    {
        // Apply healing (only players can be healed for now)
        if (target is PlayerState targetPlayer)
        {
            var effectiveMaxHP = _worldState.GetTotalEffectiveMaxHitPoints(targetPlayer);
            var oldHp = targetPlayer.HitPoints;
            targetPlayer.HitPoints = (short)Math.Min(targetPlayer.HitPoints + healing, effectiveMaxHP);
            var actualHealing = targetPlayer.HitPoints - oldHp;

            if (target == caster)
            {
                await context.Session.SendLineAsync(
                    $"You cast {metadata.Name} and heal yourself for {actualHealing} hit points.",
                    cancellationToken);
                await BroadcastToRoomAsync(context, $"$n casts {metadata.Name} on $mself.", cancellationToken);
            }
            else
            {
                await context.Session.SendLineAsync(
                    $"You cast {metadata.Name} on {target.Name}, healing them for {actualHealing} hit points.",
                    cancellationToken);

                if (targetConnectionId != null)
                {
                    var targetConnection = _connectionRegistry.GetConnections().FirstOrDefault(c => c.Id == targetConnectionId.Value);
                    if (targetConnection != null)
                    {
                        await targetConnection.Session.SendLineAsync(
                            $"{caster.Name} casts {metadata.Name} on you, healing you for {actualHealing} hit points.",
                            cancellationToken);
                    }
                }

                await BroadcastToRoomExceptAsync(
                    context,
                    targetConnectionId,
                    $"$n casts {metadata.Name} on {target.Name}.",
                    cancellationToken);
            }
        }
    }

    /// <summary>
    /// Cast an affect spell (buff/debuff) and apply affects to target.
    /// </summary>
    private async Task CastAffectSpellAsync(
        ISpellHandler spell,
        SpellMetadata metadata,
        PlayerState caster,
        ICombatant target,
        List<Affect> affects,
        ConnectionContext context,
        int? targetConnectionId,
        CancellationToken cancellationToken)
    {
        // Apply all affects to target
        foreach (var affect in affects)
        {
            target.AddAffect(affect);

            // Send messages only if they exist (first affect typically has messages)
            if (target == caster)
            {
                // Casting on self
                if (affect.ToCharMessage != null)
                {
                    await context.Session.SendLineAsync(affect.ToCharMessage, cancellationToken);
                }

                if (affect.ToRoomMessage != null)
                {
                    await BroadcastToRoomAsync(context, affect.ToRoomMessage, cancellationToken);
                }
            }
            else
            {
                // Casting on another target
                if (affect.ToCharMessage != null)
                {
                    // Send spell cast message to caster
                    await context.Session.SendLineAsync($"You cast {metadata.Name} on {target.Name}.", cancellationToken);

                    // Send affect message to target
                    if (targetConnectionId != null)
                    {
                        var targetConnection = _connectionRegistry.GetConnections().FirstOrDefault(c => c.Id == targetConnectionId.Value);
                        if (targetConnection != null)
                        {
                            await targetConnection.Session.SendLineAsync(affect.ToCharMessage, cancellationToken);
                        }
                    }
                }

                if (affect.ToRoomMessage != null)
                {
                    await BroadcastToRoomExceptAsync(context, targetConnectionId, affect.ToRoomMessage, cancellationToken);
                }
            }

            // Only show messages from the first affect (to avoid spam from multi-affect spells like Bless)
            break;
        }

        // If no affect had messages, send generic cast messages
        if (affects.All(a => a.ToCharMessage == null))
        {
            if (target == caster)
            {
                await context.Session.SendLineAsync($"You cast {metadata.Name} on yourself.", cancellationToken);
                await BroadcastToRoomAsync(context, $"$n casts {metadata.Name}.", cancellationToken);
            }
            else
            {
                await context.Session.SendLineAsync($"You cast {metadata.Name} on {target.Name}.", cancellationToken);
                
                if (targetConnectionId != null)
                {
                    var targetConnection = _connectionRegistry.GetConnections().FirstOrDefault(c => c.Id == targetConnectionId.Value);
                    if (targetConnection != null)
                    {
                        await targetConnection.Session.SendLineAsync($"{caster.Name} casts {metadata.Name} on you.", cancellationToken);
                    }
                }

                await BroadcastToRoomExceptAsync(context, targetConnectionId, $"$n casts {metadata.Name} on {target.Name}.", cancellationToken);
            }
        }
    }

    /// <summary>
    /// Broadcast message to all players in room except the caster.
    /// </summary>
    private async Task BroadcastToRoomAsync(
        ConnectionContext caster,
        string message,
        CancellationToken cancellationToken)
    {
        var roomPlayers = _connectionRegistry.GetConnections()
            .Where(c => c.Player.RoomId == caster.Player.RoomId && c.Id != caster.Id);

        foreach (var player in roomPlayers)
        {
            var formatted = _actService.FormatMessage(message, player.Player, caster.Player, null);
            await player.Session.SendLineAsync(formatted, cancellationToken);
        }
    }

    /// <summary>
    /// Broadcast message to all players in room except the caster and optionally the target.
    /// </summary>
    private async Task BroadcastToRoomExceptAsync(
        ConnectionContext caster,
        int? targetConnectionId,
        string message,
        CancellationToken cancellationToken)
    {
        var roomPlayers = _connectionRegistry.GetConnections()
            .Where(c => c.Player.RoomId == caster.Player.RoomId
                        && c.Id != caster.Id
                        && (targetConnectionId == null || c.Id != targetConnectionId.Value));

        foreach (var player in roomPlayers)
        {
            var formatted = _actService.FormatMessage(message, player.Player, caster.Player, null);
            await player.Session.SendLineAsync(formatted, cancellationToken);
        }
    }
}
