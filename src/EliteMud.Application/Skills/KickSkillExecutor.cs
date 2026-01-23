using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Skills;

/// <summary>
/// Result of executing a kick skill.
/// </summary>
public sealed record KickSkillResult(
    bool CanUse,
    string? CannotUseMessage,
    bool Hit,
    int Damage,
    bool VictimDodged,
    string? DodgeMessage,
    bool VictimDied,
    bool ImprovedSkill);

/// <summary>
/// Executes kick skill logic in the Application layer.
/// Contains all business logic for kick execution, separate from infrastructure concerns.
/// 
/// This follows clean architecture:
/// - Application layer (this class): Business logic, skill execution
/// - Game layer (KickSkill): Pure domain logic (formulas, calculations)
/// - Server layer (KickCommandHandler): Thin adapter (routing, message formatting)
/// </summary>
public sealed class KickSkillExecutor
{
    private readonly ISkillHandler _kickSkill;
    private readonly CombatCalculator _combatCalculator;
    private readonly IWorldState _worldState;

    public KickSkillExecutor(
        SkillRegistry skillRegistry,
        CombatCalculator combatCalculator,
        IWorldState worldState)
    {
        _kickSkill = skillRegistry.GetActiveSkill(SkillType.Kick);
        _combatCalculator = combatCalculator;
        _worldState = worldState;
    }

    /// <summary>
    /// Execute kick against a target (player or mob).
    /// Handles combat initiation, damage calculation, death, and skill improvement.
    /// </summary>
    /// <param name="attacker">Player performing kick</param>
    /// <param name="victim">Target of kick (player or mob)</param>
    /// <param name="attackerConnectionId">Attacker's connection ID</param>
    /// <param name="victimConnectionId">Victim's connection ID (null for mobs)</param>
    /// <returns>Result with all information needed for message formatting</returns>
    public KickSkillResult Execute(
        PlayerState attacker,
        ICombatant victim,
        int attackerConnectionId,
        int? victimConnectionId)
    {
        // Check if player can use kick
        if (!_kickSkill.CanUse(attacker))
        {
            return new KickSkillResult(
                CanUse: false,
                CannotUseMessage: _kickSkill.GetCannotUseMessage(attacker),
                Hit: false,
                Damage: 0,
                VictimDodged: false,
                DodgeMessage: null,
                VictimDied: false,
                ImprovedSkill: false);
        }

        // Start combat if not already fighting
        if (attacker.FightingConnectionId == null)
        {
            if (victimConnectionId != null)
            {
                // PvP - both players fight each other
                var victimPlayer = (PlayerState)victim;
                _combatCalculator.SetFighting(attacker, victimConnectionId.Value);
                _combatCalculator.SetFighting(victimPlayer, attackerConnectionId);
            }
            else
            {
                // PvE - player fights mob
                var mobInstance = (MobInstance)victim;
                _combatCalculator.SetFighting(attacker, -mobInstance.InstanceId);
                mobInstance.FightingConnectionId = attackerConnectionId;
            }
        }

        // Roll hit/miss
        var hit = KickSkill.RollHit(attacker, victim);
        if (!hit)
        {
            return new KickSkillResult(
                CanUse: true,
                CannotUseMessage: null,
                Hit: false,
                Damage: 0,
                VictimDodged: false,
                DodgeMessage: null,
                VictimDied: false,
                ImprovedSkill: false);
        }

        // Calculate damage
        var damage = KickSkill.CalculateDamage(attacker);

        // Apply damage
        var dodged = false;
        string? dodgeMessage = null;
        var victimDied = false;

        if (victimConnectionId != null)
        {
            // Player victim - use CombatCalculator for dodge support
            var victimPlayer = (PlayerState)victim;
            var damageResult = _combatCalculator.ApplyDamage(victimPlayer, damage);

            damage = damageResult.Damage;
            dodged = damageResult.Dodged;
            dodgeMessage = damageResult.Message;
            victimDied = victimPlayer.Position == Position.Dead;

            // Stop fighting if victim died
            if (victimDied)
            {
                _combatCalculator.StopFighting(attacker);
                _combatCalculator.StopFighting(victimPlayer);
            }
        }
        else
        {
            // Mob victim - direct damage application
            var mobInstance = (MobInstance)victim;
            mobInstance.HitPoints -= (short)damage;

            if (mobInstance.HitPoints <= 0)
            {
                mobInstance.Position = Position.Dead;
                victimDied = true;

                // Stop fighting and clean up mob
                _combatCalculator.StopFighting(attacker);
                mobInstance.FightingConnectionId = null;

                _worldState.CreateMobCorpse(mobInstance, attacker.RoomId);
                _worldState.RemoveMob(mobInstance.InstanceId, attacker.RoomId);
            }
        }

        // Improve skill on successful hit
        var improved = attacker.TryImproveSkill(SkillType.Kick);

        return new KickSkillResult(
            CanUse: true,
            CannotUseMessage: null,
            Hit: true,
            Damage: damage,
            VictimDodged: dodged,
            DodgeMessage: dodgeMessage,
            VictimDied: victimDied,
            ImprovedSkill: improved);
    }
}
