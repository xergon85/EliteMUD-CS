using EliteMud.Application.Combat;
using EliteMud.Application.Commands.Shared;
using EliteMud.Application.World;
using EliteMud.Game;

namespace EliteMud.Application.Skills;

/// <summary>
/// Executes backstab skill logic in the Application layer.
/// 
/// Backstab is a devastating surprise attack that can only be used on unsuspecting victims.
/// Legacy reference: act.offensive.c:203-256 (do_backstab), fight.c:1519-1521 (damage multiplier)
/// 
/// Requirements:
/// - Victim must not be fighting anyone
/// - TODO: Should require piercing weapon (not yet implemented)
/// 
/// Mechanics:
/// - Hit check: random(1, 101) vs skill proficiency
/// - Damage multiplier: MIN(level/10 + 1, 5) (1x at low level, up to 5x at level 40+)
/// - WAIT_STATE: 3 rounds (6 seconds)
/// - Skill improvement: Only on successful hit
/// </summary>
[Command("backstab", Aliases = new[] { "bs" })]
public sealed class BackstabSkillExecutor : ISkillExecutor
{
    private readonly BackstabSkill _backstabSkill;
    private readonly CombatCalculator _combatCalculator;
    private readonly IWorldState _worldState;
    private readonly CombatDamage _combatDamage;

    public SkillType SkillType => SkillType.Backstab;
    public TargetingMode Targeting => TargetingMode.RequiredInRoom; // Must specify target

    public BackstabSkillExecutor(
        SkillRegistry skillRegistry,
        CombatCalculator combatCalculator,
        IWorldState worldState,
        CombatDamage combatDamage)
    {
        _combatDamage = combatDamage;
        _backstabSkill = (BackstabSkill)skillRegistry.GetActiveSkill(SkillType.Backstab);
        _combatCalculator = combatCalculator;
        _worldState = worldState;
    }

    /// <summary>
    /// Execute backstab against a target (player or mob).
    /// Rolls hit and damage; the shared damage path handles combat, death and experience.
    /// </summary>
    public SkillResult Execute(SkillContext context)
    {
        var attacker = context.Actor;
        var victim = context.Victim;

        // Victim is required for backstab
        if (victim == null)
        {
            return SkillResult.Failed("Backstab who?");
        }

        // Can't backstab yourself
        if (victim == attacker)
        {
            return SkillResult.Failed("How can you sneak up on yourself?");
        }

        // Check if player can use backstab
        if (!_backstabSkill.CanUse(attacker))
        {
            return SkillResult.Failed(_backstabSkill.GetCannotUseMessage(attacker));
        }

        // Legacy check: Can't backstab a fighting person (too alert)
        // Reference: act.offensive.c:241-244
        if (victim.Fighting != null)
        {
            return SkillResult.Failed("You can't backstab a fighting person, too alert!");
        }

        // TODO: Check for piercing weapon requirement
        // Legacy: ch->equipment[WIELD]->obj_flags.value[3] != 11
        // Reference: act.offensive.c:224-231

        // Roll hit/miss
        // If victim is asleep (position < fighting), auto-hit in legacy
        // Reference: act.offensive.c:248 (AWAKE(victim) && ...)
        bool victimAwake = victim.Position >= Position.Resting;
        bool hit = !victimAwake || _backstabSkill.RollHit(attacker);

        if (!hit)
        {
            // Legacy: damage(ch, victim, 0, SKILL_BACKSTAB) - a miss still starts the fight
            _combatDamage.Apply(attacker, victim, 0);

            // Apply wait state even on miss
            attacker.WaitState = CombatConstants.WaitStates.Backstab;

            return SkillResult.Succeeded(
                new SkillMessage(SkillMessageTarget.Actor, "you try to backstab $N, but miss!", victim),
                new SkillMessage(SkillMessageTarget.Victim, "$n tries to backstab you, but misses!"),
                new SkillMessage(SkillMessageTarget.Others, "$n tries to backstab $N, but misses!", victim)
            );
        }

        // Calculate backstab damage
        // Legacy: dam = strength_bonus + damroll + weapon_dice
        // Then: dam *= MIN(level/10 + 1, 5)
        
        // Get wielded weapon for damage calculation
        ObjectWeapon? weaponDetails = null;
        ObjectInstance? weapon = null;
        if (attacker.EquipmentSlotToObjectId.TryGetValue((int)EquipmentSlot.Wield, out var weaponInstanceId))
        {
            weapon = _worldState.GetObjectInstance(weaponInstanceId);
            weaponDetails = weapon?.Definition.Details?.Weapon;
        }
        
        // Calculate effective STR (base + equipment + spell bonuses)
        var attackerEffectiveStr = _worldState.GetTotalEffectiveStrength(attacker);
        
        int baseDamage = _combatCalculator.CalculateDamage(attacker, attackerEffectiveStr, weaponDetails);
        
        // Apply weapon special effects (bless/evil/flame) BEFORE multiplier
        if (weapon != null)
        {
            int weaponEffectDamage = _combatCalculator.ApplyWeaponEffects(weapon.Definition.Flags, victim);
            baseDamage += weaponEffectDamage;
        }
        
        int multiplier = _backstabSkill.CalculateDamageMultiplier(attacker);

        var outcome = _combatDamage.Apply(
            attacker,
            victim,
            baseDamage * multiplier);

        var messages = new List<SkillMessage>
        {
            new(SkillMessageTarget.Actor, $"you backstab $N! [{outcome.Damage}]", victim),
            new(SkillMessageTarget.Victim, $"$n backstabs you! [{outcome.Damage}]"),
            new(SkillMessageTarget.Others, "$n backstabs $N!", victim)
        };
        messages.AddRange(outcome.Messages);

        // Improve skill on successful hit
        // Legacy: improve_skill(ch, SKILL_BACKSTAB) only on successful hit
        // Reference: act.offensive.c:252
        if (attacker.TryImproveSkill(SkillType.Backstab))
        {
            messages.Add(new SkillMessage(SkillMessageTarget.Actor, "Your skill - backstab - just improved!"));
        }

        // Apply combat lag (WAIT_STATE) - backstab takes 3 rounds
        // Legacy: WAIT_STATE(ch, PULSE_VIOLENCE * 3) in act.offensive.c:254
        attacker.WaitState = CombatConstants.WaitStates.Backstab;

        return new SkillResult(Success: true, Messages: messages.ToArray());
    }
}
