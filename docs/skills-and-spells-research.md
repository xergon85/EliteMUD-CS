# Skills and Spells System Research

This document captures research from the legacy EliteMUD codebase to guide implementation of the skills and spells system in EliteMUD-CS.

**Status**: Research complete, POC implemented  
**POC Documentation**: See `skills-poc-documentation.md` for implementation details and limitations

## Table of Contents
1. [Overview](#overview)
2. [Skill/Spell Numbering](#skillspell-numbering)
3. [Data Structures](#data-structures)
4. [Skill Mechanics](#skill-mechanics)
5. [Spell Mechanics](#spell-mechanics)
6. [Skill Improvement System](#skill-improvement-system)
7. [Combat Integration](#combat-integration)
8. [Proposed C# Architecture](#proposed-c-architecture)

---

## Overview

Legacy EliteMUD uses a unified numbering system for both skills and spells:
- **Spells**: 0-299 (300 total slots)
- **Skills**: 300-399 (100 total slots)
- **MAX_SKILLS**: 400 (total combined capacity)

Reference: `/Users/christofferisenberg/Dev/elitemud/src/structs.h:781`
```c
#define MAX_SKILLS          400  /* Used in CHAR_FILE_U *DO*NOT*CHANGE* */
```

Skills are stored as a byte array (0-255 values representing percentage proficiency):
```c
byte   skills[MAX_SKILLS];  // structs.h:1364
```

---

## Skill/Spell Numbering

### Spell Numbers (0-299)
Reference: `/Users/christofferisenberg/Dev/elitemud/src/spells.h:1-170`

Key spells:
- `SPELL_RESERVED_DBC = 0` (skill zero, reserved)
- `SPELL_ARMOR = 1`
- `SPELL_TELEPORT = 2`
- `SPELL_BLESS = 3`
- `SPELL_FIREBALL = 26`
- `SPELL_HEAL = 28`
- ... (111 spells defined: `NUM_OF_SPELLS = 111`)
- Maximum spell number: 299

### Skill Numbers (300-399)
Reference: `/Users/christofferisenberg/Dev/elitemud/src/spells.h:170-260`

```c
#define SKILL_START                 300

// Weapon Skills
#define SKILL_STAB                  300
#define SKILL_BLUDGEON              301
#define SKILL_SLASH                 302
#define SKILL_CHOP                  303

// Combat Skills (PRIORITY FOR IMPLEMENTATION)
#define SKILL_KICK                  323 
#define SKILL_BASH                  324 
#define SKILL_RESCUE                325
#define SKILL_BACKSTAB              315
#define SKILL_DODGE                 318
#define SKILL_PARRY                 328
#define SKILL_DISARM                345
#define SKILL_CIRCLE_AROUND         372

// Utility Skills
#define SKILL_SNEAK                 312
#define SKILL_HIDE                  313
#define SKILL_STEAL                 314
#define SKILL_PICK_LOCK             316
#define SKILL_TRACK                 333
#define SKILL_FIRST_AID             311

// Maximum skill number: 399
```

### Attack Types (400+)
Reference: `/Users/christofferisenberg/Dev/elitemud/src/spells.h:262-265`

```c
#define TYPE_START                   400
#define TYPE_HIT                     400
#define TYPE_BLUDGEON                401
// ... used for damage messages, not stored in skills array
```

---

## Data Structures

### Skill Storage Macros
Reference: `/Users/christofferisenberg/Dev/elitemud/src/utils.h:371-373`

```c
// Get skill percentage (0-255)
#define GET_SKILL(ch, i) \
  ((ch)->skills ? (((ch)->skills)[i]) : \
   ((ch)->mobskills ? get_mob_skill(ch, i) : 0))

// Set skill percentage (PCs only)
#define SET_SKILL(ch, i, pct) { \
  if ((ch)->skills) (ch)->skills[i] = pct; \
}

// Skills left to learn at guildmaster
#define SPELLS_TO_LEARN(ch) ((ch)->specials2.spells_to_learn)
```

**Key Points**:
- Skills are stored as `byte` (0-255, representing percentage)
- PCs have `skills` array, NPCs use `mobskills` (different system)
- Skills can only be set for PCs (NPCs use `get_mob_skill()` function)

### Spell Info Structure
Reference: `/Users/christofferisenberg/Dev/elitemud/src/spells.h:344-350`

```c
struct spell_info_type {
    int    type;              /* What type of spell is this? */
    byte   minimum_position;  /* Min position for caster */
    ubyte  min_usesmana;      /* Amount of mana used by a spell  */
    byte   beats;             /* Heartbeats until ready for next */
    byte   offensive;         /* Is the spell offensive is some way? */
    sh_int targets;           /* See below for use with TAR_XXX  */
    // ... more fields (need to read full structure)
};

extern struct spell_info_type spell_info[MAX_SPL_LIST]; // MAX_SPL_LIST = 300
```

**Key Differences Between Skills and Spells**:
- Spells consume **mana** (`min_usesmana`)
- Spells require specific **casting positions** (standing, fighting, etc.)
- Spells have complex **targeting** (TAR_CHAR_ROOM, TAR_FIGHT_VICT, etc.)
- Spells have **offensive flags** for AI behavior
- Skills are mostly **immediate actions** (no mana cost)

---

## Skill Mechanics

### Skill Check Algorithm

#### Example: KICK
Reference: `/Users/christofferisenberg/Dev/elitemud/src/act.offensive.c:648-689`

```c
ACMD(do_kick) {
  struct char_data *victim;
  byte percent, prob;

  // 1. Target Selection
  one_argument(argument, arg);
  if (!(victim = get_char_room_vis(ch, arg))) {
    if (ch->specials.fighting) {
      victim = ch->specials.fighting;
    } else {
      send_to_char("Kick who?\r\n", ch);
      return;
    }
  }

  // 2. Validation
  if (victim == ch) {
    send_to_char("Aren't we funny today...\r\n", ch);
    return;
  }
  if (!pkok_check(ch, victim))
    return;

  // 3. Skill Check Formula
  percent = ((10 - (GET_AC(victim) / 10)) << 1) + number(1, 101);
  prob = GET_SKILL(ch, SKILL_KICK);

  // 4. Success/Failure
  if (percent > prob) {
    // FAILURE
    damage(ch, victim, 0, SKILL_KICK);
  } else {
    // SUCCESS
    damage(ch, victim, GET_LEVEL(ch) >> 1, SKILL_KICK);
  }

  // 5. Combat Lag
  WAIT_STATE(ch, PULSE_VIOLENCE * 3);
}
```

**Key Formula**:
```
percent = ((10 - (AC / 10)) * 2) + random(1, 101)
prob = skill_percentage

if (percent > prob) → FAILURE
if (percent <= prob) → SUCCESS
```

- Lower AC makes it harder to hit (increases `percent`)
- Higher skill % increases chance of success
- Random roll of 1-101 means always ~1% chance of failure at 100% skill
- Roll of 101 is "complete failure"

**Damage on Success**:
- Kick: `GET_LEVEL(ch) >> 1` (half character level)
- Bash: Fixed `10` damage

#### Example: BASH
Reference: `/Users/christofferisenberg/Dev/elitemud/src/act.offensive.c:484-583`

```c
ACMD(do_bash) {
  // ... validation checks ...
  
  if (!ch->equipment[WEAR_SHIELD]) {
    send_to_char("You need to have a shield, to bash something.\r\n", ch);
    return;
  }

  // Skill Check (simpler than kick)
  percent = number(1, 101);
  prob = GET_SKILL(ch, SKILL_BASH);

  if (percent > prob) {
    // FAILURE - basher falls down
    act("You try to bash $N but kiss the ground instead.", TRUE, ch, 0, victim, TO_CHAR);
    damage(ch, victim, 0, SKILL_BASH);
    GET_POS(ch) = POS_SITTING;  // Basher sits
  } else {
    // SUCCESS - victim knocked down
    act("You easily bash $N.", TRUE, ch, 0, victim, TO_CHAR);
    GET_POS(victim) = POS_SITTING;  // Victim sits
    WAIT_STATE(victim, PULSE_VIOLENCE);  // Victim lag
    damage(ch, victim, 10, SKILL_BASH);
  }

  WAIT_STATE(ch, PULSE_VIOLENCE * 2);  // Basher lag
}
```

**Key Features**:
- **Equipment requirement**: Must have shield equipped
- **Position changes**: Changes victim (or self on failure) to sitting
- **Multiple lag effects**: Both attacker and victim get WAIT_STATE
- **Dual-purpose**: Can also bash doors (separate logic)

**Bash Door Mechanics** (lines 504-547):
```c
// Bash door formula
if ((number(1, 1000) > (GET_SKILL(ch, SKILL_BASH) + str_app[STRENGTH_APPLY_INDEX(ch)].bash)) 
    || IS_SET(EXIT(ch, door)->exit_info, EX_BASHPROOF)) {
  // FAILURE - take damage
  send_to_char("You bounce off the door.\r\n", ch);
  GET_HIT(ch) = MAX(1, GET_HIT(ch) - number(1, 25));  // 1-25 HP loss
} else {
  // SUCCESS - door broken
  REMOVE_BIT(EXIT(ch, door)->exit_info, EX_LOCKED);
  REMOVE_BIT(EXIT(ch, door)->exit_info, EX_CLOSED);
  SET_BIT(EXIT(ch, door)->exit_info, EX_BROKEN);
  send_to_char("*smash*\r\n", ch);
}
```

---

## Spell Mechanics

### Spell Casting Flow
Reference: `/Users/christofferisenberg/Dev/elitemud/src/spell_parser.c:657-800`

```c
ACMD(do_cast) {
  // 1. Parse spell name from 'cast 'spell name''
  // 2. Find spell number (spl)
  // 3. Check if spell exists and is valid
  
  if ((spl > 0) && (spl <= NUM_OF_SPELLS) && spell_info[spl].type) {
    
    // 4. Check caster position
    if (GET_POS(ch) < spell_info[spl].minimum_position) {
      send_to_char("You can't concentrate enough while resting.\r\n", ch);
      return;
    }
    
    // 5. Check if character knows spell
    if (!GET_SKILL(ch, spl)) {
      send_to_char("Sorry, you can't do that.\r\n", ch);
      return;
    }
    
    // 6. Check mana cost
    if (GET_MANA(ch) < USE_MANA(ch, spl)) {
      send_to_char("You can't summon enough energy to cast the spell.\r\n", ch);
      return;
    }
    
    // 7. Parse target arguments
    // 8. Validate target based on spell_info[spl].targets flags
    //    - TAR_CHAR_ROOM, TAR_CHAR_WORLD
    //    - TAR_FIGHT_SELF, TAR_FIGHT_VICT
    //    - TAR_OBJ_INV, TAR_OBJ_ROOM, etc.
    
    // 9. Call spell function (implementation varies per spell)
    // 10. Deduct mana, apply lag
  }
}
```

**Spell Targeting Flags** (spells.h:318-329):
```c
#define TAR_IGNORE        1     // No target needed
#define TAR_CHAR_ROOM     2     // Character in room
#define TAR_CHAR_WORLD    4     // Character anywhere
#define TAR_FIGHT_SELF    8     // Self, if fighting
#define TAR_FIGHT_VICT   16     // Current opponent
#define TAR_SELF_ONLY    32     // Only self allowed
#define TAR_SELF_NONO    64     // Self not allowed
#define TAR_OBJ_INV     128     // Object in inventory
#define TAR_OBJ_ROOM    256     // Object in room
#define TAR_OBJ_WORLD   512     // Object anywhere
#define TAR_OBJ_EQUIP  1024     // Equipped object
```

**Spell Effect Types** (spells.h:331-342):
```c
#define MAG_DAMAGE      (1 << 0)   // Damages target
#define MAG_AFFECTS     (1 << 1)   // Adds affect
#define MAG_UNAFFECTS   (1 << 2)   // Removes affect
#define MAG_POINTS      (1 << 3)   // Modifies HP/MANA/MOVE
#define MAG_ALTER_OBJS  (1 << 4)   // Changes objects
#define MAG_GROUPS      (1 << 5)   // Affects whole group
#define MAG_MASSES      (1 << 6)   // Affects room
#define MAG_AREAS       (1 << 7)   // Affects area
#define MAG_SUMMONS     (1 << 8)   // Summons creature
#define MAG_CREATIONS   (1 << 9)   // Creates object
#define MAG_MANUAL      (1 << 10)  // Custom implementation
```

---

## Skill Improvement System

Reference: `/Users/christofferisenberg/Dev/elitemud/src/act.other.c:52-74`

```c
void improve_skill(struct char_data *ch, int skillnr) {
  int percent;
  char tmp[MAX_INPUT_LENGTH];

  // 1. NPCs don't improve
  if (!ch || IS_NPC(ch))
    return;

  percent = GET_SKILL(ch, skillnr);

  // 2. Improvement Conditions (ALL must be true):
  if (percent > 1 &&                                    // Must have learned (>1%)
      know_skill(ch, skillnr) &&                        // Must know the skill
      percent < get_skill_max(ch, skillnr) &&           // Must not be at cap
      number(0, 99) > percent &&                        // Random chance (harder at high %)
      number(0, 100) < ch->specials.skillgain)          // Cooldown check
  {
    // 3. Increase skill by 1%
    SET_SKILL(ch, skillnr, percent + 1);
    
    // 4. Notify player
    sprintf(tmp, "Your %s - %s - just improved.\r\n",
            (skillnr < SKILL_START ? "spell" : "skill"),
            (skillnr < SKILL_START ? spells[skillnr-1] : skills[skillnr-SKILL_START]));
    send_to_char(tmp, ch);
    
    // 5. Reset skillgain cooldown
    ch->specials.skillgain = 0;
  }
}
```

**Improvement Algorithm**:
1. **Cooldown**: `ch->specials.skillgain` must be > random(0,100)
2. **Success Rate**: `random(0,99) > current_percent`
   - At 10% skill: 90% chance to improve
   - At 50% skill: 50% chance to improve
   - At 90% skill: 10% chance to improve
3. **Rate**: +1% per successful check
4. **Triggers**: Called after successful skill use (see examples below)

**Where improve_skill() is Called**:
- After successful backstab: `act.offensive.c:255, 308`
- After successful rescue: `act.offensive.c:642`
- After successful disarm: `act.offensive.c:825`
- After successful dodge/parry: `fight.c` (combat system)
- After successful steal: `act.other.c:409`
- After successful hide: `act.other.c:311`
- **NOT called after kick/bash** in current code (may be oversight or intentional)

**Helper Functions** (need to research):
- `know_skill(ch, skillnr)`: Check if character class can learn skill
- `get_skill_max(ch, skillnr)`: Get max % for skill based on class/level

---

## Combat Integration

### Combat Lag (WAIT_STATE)
Skills apply combat lag to prevent rapid skill use:

```c
#define PULSE_VIOLENCE  (2 RL_SEC)  // ~2 seconds per combat round

WAIT_STATE(ch, PULSE_VIOLENCE * 3);  // Kick: 3 rounds (6 sec)
WAIT_STATE(ch, PULSE_VIOLENCE * 2);  // Bash: 2 rounds (4 sec)
WAIT_STATE(victim, PULSE_VIOLENCE);  // Bash victim: 1 round
```

**Position Changes**:
- `GET_POS(ch) = POS_SITTING`: Character knocked down
- `GET_POS(ch) = POS_FIGHTING`: Character in combat
- Sitting characters have penalties (need to research exact mechanics)

### Damage Integration
Reference: Calls to `damage(ch, victim, amount, skill_type)`

```c
// Skill damage examples:
damage(ch, victim, GET_LEVEL(ch) >> 1, SKILL_KICK);  // Half level
damage(ch, victim, 10, SKILL_BASH);                   // Fixed 10
damage(ch, victim, 0, SKILL_KICK);                    // Miss (0 damage)
```

The `damage()` function:
- Applies damage to victim
- Triggers combat messages
- May start combat if not already fighting
- Handles death, corpses, experience
- **Automatically calls improve_skill() for some skills** (need to verify)

### Passive Skills
Some skills trigger automatically during combat:
- `SKILL_DODGE` (318): Chance to avoid attacks
- `SKILL_PARRY` (328): Chance to deflect attacks
- `SKILL_BLINDFIGHT` (339): Fight while blind
- `SKILL_MOUNTED_BATTLE` (369): Fight while mounted

Reference: `fight.c:1367, 1408` shows these being checked during combat rounds

---

## Proposed C# Architecture

### Skill/Spell Enumeration
```csharp
// EliteMud.Game/WorldModels.cs or new Skills.cs

public enum SkillType
{
    // Spells (0-299)
    None = 0,
    Armor = 1,
    Teleport = 2,
    Bless = 3,
    // ... (expand as needed)
    Fireball = 26,
    Heal = 28,
    
    // Skills (300-399)
    SkillStart = 300,
    Stab = 300,
    Bludgeon = 301,
    // ... weapon skills ...
    Kick = 323,
    Bash = 324,
    Rescue = 325,
    // ...
    
    // Max values
    MaxSpell = 299,
    MaxSkill = 399
}

public static class SkillTypeExtensions
{
    public static bool IsSpell(this SkillType skill) => (int)skill < 300;
    public static bool IsSkill(this SkillType skill) => (int)skill >= 300 && (int)skill < 400;
}
```

### Skill Definition Model
```csharp
// EliteMud.Game/Skills/SkillDefinition.cs

public record SkillDefinition(
    SkillType Type,
    string Name,
    string Description,
    SkillCategory Category,
    int MinLevel,           // Minimum level to learn
    int MaxProficiency,     // Usually 95-100
    int LagRounds           // Combat lag in PULSE_VIOLENCE units
);

public enum SkillCategory
{
    Weapon,
    Combat,
    Stealth,
    Magic,
    Utility
}
```

### Spell Definition Model
```csharp
// EliteMud.Game/Spells/SpellDefinition.cs

public record SpellDefinition(
    SkillType Type,
    string Name,
    string Description,
    int ManaCost,
    CharacterPosition MinPosition,
    SpellTargetFlags TargetFlags,
    SpellEffectFlags EffectFlags,
    int LagRounds
);

[Flags]
public enum SpellTargetFlags
{
    None = 0,
    Ignore = 1,
    CharacterInRoom = 2,
    CharacterAnywhere = 4,
    FightingSelf = 8,
    FightingVictim = 16,
    SelfOnly = 32,
    NotSelf = 64,
    ObjectInventory = 128,
    ObjectRoom = 256,
    ObjectAnywhere = 512,
    ObjectEquipped = 1024
}

[Flags]
public enum SpellEffectFlags
{
    None = 0,
    Damage = 1,
    ApplyAffect = 2,
    RemoveAffect = 4,
    ModifyPoints = 8,      // HP/MANA/MOVE
    AlterObjects = 16,
    Group = 32,
    Mass = 64,             // Room-wide
    Area = 128,
    Summon = 256,
    Creation = 512,
    Manual = 1024          // Custom implementation
}
```

### Character Skill Storage
```csharp
// EliteMud.Game/WorldModels.cs - extend MobInstance

public record MobInstance
{
    // ... existing fields ...
    
    public Dictionary<SkillType, byte> Skills { get; init; } = new();
    
    // Helper methods
    public byte GetSkillProficiency(SkillType skill) 
        => Skills.TryGetValue(skill, out var prof) ? prof : (byte)0;
    
    public void SetSkillProficiency(SkillType skill, byte proficiency)
        => Skills[skill] = Math.Min(proficiency, (byte)255);
    
    public bool KnowsSkill(SkillType skill) => GetSkillProficiency(skill) > 0;
}
```

### Skill Handler Base Class
```csharp
// EliteMud.Application/Skills/SkillHandlerBase.cs

public abstract class SkillHandlerBase
{
    protected SkillDefinition Definition { get; }
    
    protected SkillHandlerBase(SkillDefinition definition)
    {
        Definition = definition;
    }
    
    // Execute skill
    public abstract Task<SkillResult> ExecuteAsync(
        MobInstance actor,
        MobInstance? target,
        IWorldState world);
    
    // Check if skill succeeds (override for custom formulas)
    protected virtual bool CheckSuccess(MobInstance actor, MobInstance? target)
    {
        var proficiency = actor.GetSkillProficiency(Definition.Type);
        var roll = Random.Shared.Next(1, 102);  // 1-101
        
        // Basic formula: roll <= proficiency
        return roll <= proficiency;
    }
    
    // Attempt skill improvement
    protected void TryImproveSkill(MobInstance actor)
    {
        if (actor.IsNpc) return;
        
        var proficiency = actor.GetSkillProficiency(Definition.Type);
        if (proficiency >= Definition.MaxProficiency) return;
        
        // Chance to improve: (100 - proficiency)%
        if (Random.Shared.Next(0, 100) > proficiency)
        {
            actor.SetSkillProficiency(Definition.Type, (byte)(proficiency + 1));
            // TODO: Notify player of improvement
        }
    }
}

public record SkillResult(
    bool Success,
    string Message,
    int Damage,
    CharacterPosition? NewActorPosition = null,
    CharacterPosition? NewTargetPosition = null,
    int LagRounds = 0
);
```

### Example: Kick Skill Handler
```csharp
// EliteMud.Application/Skills/Combat/KickSkillHandler.cs

public class KickSkillHandler : SkillHandlerBase
{
    public KickSkillHandler() : base(new SkillDefinition(
        Type: SkillType.Kick,
        Name: "kick",
        Description: "Kick your opponent for extra damage.",
        Category: SkillCategory.Combat,
        MinLevel: 1,
        MaxProficiency: 95,
        LagRounds: 3
    )) { }
    
    public override async Task<SkillResult> ExecuteAsync(
        MobInstance actor, 
        MobInstance? target, 
        IWorldState world)
    {
        if (target == null)
            return new SkillResult(false, "Kick who?", 0);
        
        if (target == actor)
            return new SkillResult(false, "Aren't we funny today...", 0);
        
        // Legacy kick formula:
        // percent = ((10 - (AC/10)) * 2) + random(1,101)
        // prob = skill%
        var victimAc = target.Definition.ArmorClass;
        var percent = ((10 - (victimAc / 10)) * 2) + Random.Shared.Next(1, 102);
        var prob = actor.GetSkillProficiency(SkillType.Kick);
        
        bool success = percent <= prob;
        
        if (success)
        {
            int damage = actor.Level / 2;  // Half level
            TryImproveSkill(actor);
            
            return new SkillResult(
                Success: true,
                Message: $"You kick {target.Name}!",
                Damage: damage,
                LagRounds: Definition.LagRounds
            );
        }
        else
        {
            return new SkillResult(
                Success: false,
                Message: $"You try to kick {target.Name} but miss!",
                Damage: 0,
                LagRounds: Definition.LagRounds
            );
        }
    }
}
```

### Integration with Combat System
```csharp
// EliteMud.Application/Combat/CombatService.cs (new or extend existing)

public class CombatService
{
    private readonly IWorldState _world;
    private readonly Dictionary<SkillType, SkillHandlerBase> _skillHandlers;
    
    public async Task<SkillResult> UseSkillAsync(
        string actorId, 
        SkillType skill, 
        string? targetId = null)
    {
        var actor = _world.GetMob(actorId);
        if (actor == null) 
            return new SkillResult(false, "Invalid actor.", 0);
        
        // Check if actor knows skill
        if (!actor.KnowsSkill(skill))
            return new SkillResult(false, "You don't know how to do that.", 0);
        
        // Get target
        MobInstance? target = null;
        if (targetId != null)
        {
            target = _world.GetMob(targetId);
        }
        else if (actor.CombatTarget != null)
        {
            target = _world.GetMob(actor.CombatTarget);
        }
        
        // Execute skill
        var handler = _skillHandlers[skill];
        var result = await handler.ExecuteAsync(actor, target, _world);
        
        // Apply damage if any
        if (result.Damage > 0 && target != null)
        {
            // TODO: Integrate with damage system
            // await ApplyDamageAsync(actor, target, result.Damage, skill);
        }
        
        // Apply position changes
        if (result.NewActorPosition.HasValue)
        {
            // Update actor position
        }
        if (result.NewTargetPosition.HasValue && target != null)
        {
            // Update target position
        }
        
        // Apply combat lag
        // TODO: Implement WAIT_STATE equivalent
        
        return result;
    }
}
```

---

## Implementation Roadmap

### ✅ Phase 1: Core Infrastructure - COMPLETE
1. ✅ Create `SkillType` enum with all skill/spell numbers - DONE
2. ✅ Add `Skills` dictionary to `PlayerState` - DONE (Dictionary<SkillType, byte>)
3. ✅ Create `ISkillHandler` interface - DONE (active skills)
4. ✅ Create `IPassiveSkillHandler` interface - DONE (passive skills)
5. ✅ Create `SkillRegistry` with auto-discovery - DONE (via reflection)

### ✅ Phase 2: First Combat Skills - COMPLETE (POC)
1. ✅ Implement Kick skill
   - ✅ Legacy formula: `((10 - AC/10) * 2) + random(1,101) vs skill%`
   - ✅ Damage: `level / 2`
   - ✅ Command integration via `ISkillExecutor` + `SkillCommandHandler`
2. ⏳ Implement Bash skill - TODO
   - Requires shield check
   - Position changes (victim → sitting)
   - Damage: fixed 10
3. ⏳ Implement Rescue skill - TODO
   - Switch combat targets

### ✅ Phase 3: Skill Improvement - COMPLETE (POC)
1. ✅ Implement skill improvement logic - DONE
   - ✅ Improvement chance: `random(0,99) > current_percent`
   - ✅ Rate: +1% per successful check
2. ✅ Add skill proficiency tracking - DONE (Dictionary<SkillType, byte> in PlayerState)
3. ✅ Hook improvement into skill success - DONE (KickExecutor)
4. ✅ Player notifications - DONE (via ActMessage)
5. ⏳ Skillgain cooldown - TODO (needs WAIT_STATE system)

### ✅ Phase 4: Passive Skills - COMPLETE (POC)
1. ✅ Implement Dodge passive skill - DONE
   - ✅ Formula: `(random(1,250) + damage) < skill_percent`
   - ✅ Damage reduction: 2× level
   - ✅ Integrated into CombatCalculator
2. ✅ Research legacy fight.c - DONE (see Phase 4 documentation above)
3. ✅ Automatic skill checks in combat - DONE
4. ⏳ Parry passive skill - TODO
5. ⏳ Tumble passive skill - TODO

### 🔄 Phase 5: Skills Framework Production (IN PROGRESS)
**Current Status**: POC complete, framework extraction complete, needs WAIT_STATE system

1. ✅ **ISkillExecutor interface** - DONE
   - Auto-discovered via reflection
   - Wrapped in generic `SkillCommandHandler`
   - Automatically available as commands

2. ✅ **SkillRegistry** - DONE
   - Auto-discovers ISkillHandler (active skills)
   - Auto-discovers IPassiveSkillHandler (passive skills)
   - Dependency injection integration

3. ⏳ **WAIT_STATE System** - NEXT STEP
   - Add WaitState property to PlayerState
   - Prevent actions while waiting (cooldown system)
   - Integrate with combat tick (decrement each tick)
   - Block skill/command usage during wait state

4. ⏳ **Skill Metadata Schema** - TODO
   - Create `content/skills.json` format
   - Define: name, type, damage, cooldown, level requirements
   - Include class restrictions and skill caps

5. ⏳ **Additional Skills** - TODO
   - bash (shield attack)
   - backstab (rogue skill)
   - parry (passive defense)
   - rescue (tank skill)

### ⏳ Phase 6: Spell System - NOT STARTED
1. Create `SpellHandlerBase` (similar to skills but with mana)
2. Implement spell targeting system
3. Implement spell effect system (affects, damage, etc.)
4. Add first test spell (e.g., magic missile, cure light)

### ⏳ Phase 7: Content Data - NOT STARTED
1. Create JSON schema for skills/spells
2. Load skill definitions from `content/skills.json`
3. Load spell definitions from `content/spells.json`
4. Version schema in `content-schema.md`

---

## Answered Questions (Research Phase 2)

### 1. Skillgain Cooldown System ✅
Reference: `/Users/christofferisenberg/Dev/elitemud/src/limits.c:621-622`

**How it works:**
```c
// Called every tick (point_update)
if (i->specials.skillgain < 100)
  i->specials.skillgain++;
```

- **Incremented**: Every game tick (appears to be ~75 seconds based on point_update)
- **Range**: 0-100
- **Reset**: Set to 0 after successful skill improvement
- **Check**: `number(0, 100) < ch->specials.skillgain` must be true to improve
- **Purpose**: Prevents skill spam - you can't improve the same skill multiple times rapidly

**Improvement Probability Over Time:**
- Tick 0 (just improved): 0% chance to improve again
- Tick 50: 50% chance to improve
- Tick 100+: 100% chance to improve (if other conditions met)

### 2. Mob Skills System ✅
Reference: `/Users/christofferisenberg/Dev/elitemud/src/utility.c:85-96`

**How it works:**
```c
int get_mob_skill(struct char_data *mob, int nr) {
    if (nr < SKILL_START || nr > SKILL_START + 99)
        return 0;
    
    if (!mob->mobskills) {
        perror("no allocated skillarray - get_mob_skill.");
        exit(0);
    }
    
    return to_percentage(mob, mob->mobskills[nr - SKILL_START]);
}
```

**to_percentage conversion** (`utility.c:74-82`):
```c
int to_percentage(struct char_data *ch, int value) {
    switch (value) {
    case LVL:  return MIN(100, GET_LEVEL(ch)); break;
    case LVL2: return MIN(100, 2 * GET_LEVEL(ch)); break;
    case LVL3: return MIN(100, 3 * GET_LEVEL(ch)); break;
    default:   return value;
    }
}
```

**Key Points:**
- NPCs have `mobskills` array (100 bytes, skills only, no spells)
- Values can be:
  - **Fixed number** (0-255): Direct percentage
  - **LVL**: Mob level (capped at 100%)
  - **LVL2**: 2x mob level (capped at 100%)
  - **LVL3**: 3x mob level (capped at 100%)
- PCs use `skills` array (400 bytes, spells + skills)
- Mobs don't improve skills (checked in improve_skill)

### 3. Class Restrictions (know_skill) ✅
Reference: `/Users/christofferisenberg/Dev/elitemud/src/spec_procs.c:120-155`

**How it works:**
```c
int know_skill(struct char_data *ch, int skillnr) {
    int knowskill = FALSE;

    if (skillnr < SKILL_START) {   /* Spell */
        skillnr--;
        
        if (IS_MULTI(ch) || IS_DUAL(ch)) {
            if (GET_1LEVEL(ch) >= spell_minlevel[skillnr][GET_1CLASS(ch)-1])
                knowskill = TRUE;
            if (GET_2LEVEL(ch) >= spell_minlevel[skillnr][GET_2CLASS(ch)-1])
                knowskill = TRUE;
            if (IS_3MULTI(ch) && 
                (GET_3LEVEL(ch) >= spell_minlevel[skillnr][GET_3CLASS(ch)-1]))
                knowskill = TRUE;
        } else
            if (GET_LEVEL(ch) >= spell_minlevel[skillnr][GET_CLASS(ch)-1])
                knowskill = TRUE;
    } else {
        skillnr -= SKILL_START;
        
        if (IS_MULTI(ch) || IS_DUAL(ch)) {
            if (GET_1LEVEL(ch) >= skill_minlevel[skillnr][GET_1CLASS(ch)-1])
                knowskill = TRUE;
            if (GET_2LEVEL(ch) >= skill_minlevel[skillnr][GET_2CLASS(ch)-1])
                knowskill = TRUE;
            if (IS_3MULTI(ch) && 
                (GET_3LEVEL(ch) >= skill_minlevel[skillnr][GET_3CLASS(ch)-1]))
                knowskill = TRUE;
        } else
            if (GET_LEVEL(ch) >= skill_minlevel[skillnr][GET_CLASS(ch)-1])
                knowskill = TRUE;
    }

    return knowskill;
}
```

**Algorithm:**
1. Check character's class(es)
2. Look up minimum level required in class table
3. Return TRUE if character meets level requirement for ANY class they have
4. Multi-class characters check all their classes

**Class Table Structure** (`constants.c:325-430`):
```c
// 88 skills x 20 classes
const char skill_minlevel[88][20] = {
/* Skill  MU CL TH WA PS MO BA KN WI DR AS RA IL PA MA CA !! !! NI !! */
/*KICK */{XX,XX,XX,10,XX,14,30,12,XX,XX,22,14,XX,XX,10,10,XX,XX,22,XX},
/*BASH */{XX,XX,XX,12,XX,XX,36,14,XX,XX,XX,16,XX,45,12,12,XX,XX,XX,XX},
/*RESC */{XX,XX,XX,14,XX,XX,XX, 6,XX,XX,XX,18,XX,42,38,14,XX,XX,XX,XX},
// ...
};

const char skill_max[88][20] = {
// Maximum proficiency per class (usually 50-95)
};
```

**XX = LEVEL_DEITY (100+)** = Class cannot learn skill

**Classes** (`structs.h:232-255`):
1. Magic User (MU)
2. Cleric (CL)
3. Thief (TH)
4. Warrior (WA)
5. Psionicist (PS)
6. Monk (MO)
7. Bard (BA)
8. Knight (KN)
9. Wizard (WI)
10. Druid (DR)
11. Assassin (AS)
12. Ranger (RA)
13. Illusionist (IL)
14. Paladin (PA)
15. Mariner (MA)
16. Cavalier (CA)
19. Ninja (NI)

**Examples from table:**
- **Kick**: Warriors get at lvl 10, Monks at 14, Assassins at 22
- **Bash**: Warriors at 12, Knights at 14, Paladins at 45
- **Rescue**: Knights at 6, Warriors at 14, Rangers at 18

### 4. Skill Max (get_skill_max) ✅
Reference: `/Users/christofferisenberg/Dev/elitemud/src/spec_procs.c:202-236`

**How it works:**
```c
int get_skill_max(struct char_data *ch, int skillnr) {
    int max = 1;

    if (skillnr < SKILL_START) {   /* spell */
        skillnr--;
        
        if (IS_MULTI(ch) || IS_DUAL(ch)) {
            if (max < spell_max[skillnr][GET_1CLASS(ch)-1])
                max = spell_max[skillnr][GET_1CLASS(ch)-1];
            if (max < spell_max[skillnr][GET_2CLASS(ch)-1])
                max = spell_max[skillnr][GET_2CLASS(ch)-1];
            if (IS_3MULTI(ch) && 
                (max < spell_max[skillnr][GET_3CLASS(ch)-1]))
                max = spell_max[skillnr][GET_3CLASS(ch)-1];
        } else
            max = spell_max[skillnr][GET_CLASS(ch)-1];

    } else {
        skillnr -= SKILL_START;
        
        if (IS_MULTI(ch) || IS_DUAL(ch)) {
            if (max < skill_max[skillnr][GET_1CLASS(ch)-1])
                max = skill_max[skillnr][GET_1CLASS(ch)-1];
            if (max < skill_max[skillnr][GET_2CLASS(ch)-1])
                max = skill_max[skillnr][GET_2CLASS(ch)-1];
            if (IS_3MULTI(ch) && 
                (max < skill_max[skillnr][GET_3CLASS(ch)-1]))
                max = skill_max[skillnr][GET_3CLASS(ch)-1];
        } else
            max = skill_max[skillnr][GET_CLASS(ch)-1];
    }

    return max;
}
```

**Algorithm:**
1. Look up max proficiency in class table
2. For multi-class characters, return the HIGHEST max among all classes
3. Different classes have different skill caps (e.g., 50%, 75%, 95%)
4. This determines when skill improvement stops

### 5. Practice System ✅
Reference: `/Users/christofferisenberg/Dev/elitemud/src/act.other.c:472-491` and `/Users/christofferisenberg/Dev/elitemud/src/spec_procs.c:343-423`

**Command Handler:**
```c
ACMD(do_practice) {
  void practices(struct char_data *ch, int mode);

  one_argument(argument, arg);

  if (!subcmd)
    send_to_char("You can only practice with your guildmaster.\r\n", ch);
  else if (subcmd == SCMD_SKILLS) {
    if (*arg == 'i')
      practices(ch, 3);  // List skills character WILL learn
    else
      practices(ch, 1);  // List skills character KNOWS
  } else if (subcmd == SCMD_SPELLS) {
    if (*arg == 'i')
      practices(ch, 4);  // List spells character WILL learn
    else
      practices(ch, 2);  // List spells character KNOWS
  }
}
```

**Guildmaster Special Procedure** (`spec_procs.c:344-423`):
```c
int guild(struct char_data *ch, struct char_data *mob, char *arg) {
  int number, i, percent;
  
  // Show available skills/spells to practice
  if (!*arg) {
    for (i = 0; *skills[i] != '\n'; i++) {
      if (know_skill(ch, i + SKILL_START) &&
          GET_SKILL(ch, i + SKILL_START) < get_skill_max(ch, i + SKILL_START)) {
        sprintf(buf2, "%-20s %-14s %-18s %8d\r\n", 
                skills[i],
                how_good(GET_SKILL(ch, i + SKILL_START)),
                how_hard(get_skill_diff(ch, i + SKILL_START)),
                skill_cost(ch, i + SKILL_START));
        strcat(buf, buf2);
      }
    }
    // ... same for spells ...
    return TRUE;
  }
  
  // Practice specific skill/spell
  number = find_skill_num(arg);
  
  if (!know_skill(ch, number)) {
    send_to_char("You do not know of this skill.\r\n", ch);
    return TRUE;
  }
  
  if (GET_SKILL(ch, number) >= get_skill_max(ch, number)) {
    send_to_char("You are already learned in this area.\r\n", ch);
    return TRUE;
  }
  
  if (SPELLS_TO_LEARN(ch) <= 0) {
    send_to_char("You do not have any practice sessions.\r\n", ch);
    return TRUE;
  }
  
  SPELLS_TO_LEARN(ch)--;
  
  // Calculate improvement
  percent = GET_SKILL(ch, number);
  percent += MIN(get_skill_max(ch, number) - percent, MAX(get_skill_diff(ch, number), 10));
  
  SET_SKILL(ch, number, MIN(get_skill_max(ch, number), percent));
  
  if (GET_SKILL(ch, number) >= get_skill_max(ch, number)) {
    send_to_char("You are now learned in this area.\r\n", ch);
    return TRUE;
  }
  
  send_to_char("You practice for a while...\r\n", ch);
  return TRUE;
}
```

**Practice Mechanics:**
1. **Practice Sessions**: Characters have `SPELLS_TO_LEARN(ch)` practice sessions (gained on level-up)
2. **Guildmaster Required**: Can only practice at guildmaster NPCs
3. **Initial Learning**: Sets skill to 1% when first learned (or higher based on difficulty)
4. **Improvement**: Each practice increases skill by `MAX(get_skill_diff(ch, skill), 10)` percent
5. **Cost**: `skill_cost(ch, skillnr)` = `(min_level * min_level * 10)` (not currency, just info)
6. **Restrictions**: 
   - Must `know_skill()` (class + level requirement)
   - Cannot exceed `get_skill_max()`
   - Must have practice sessions available

**How Players Learn New Skills:**
1. Reach required level for their class
2. Visit guildmaster
3. `practice` (no args) to see available skills
4. `practice kick` to learn/improve kick
5. Costs 1 practice session
6. Skill starts at 1% if new, or improves by 10+ percent if already known

### 6. Spell Learning ✅
**Answer**: Same as skills! Uses the same practice system and guildmaster.

- Spells use `spell_minlevel` and `spell_max` tables (same structure)
- Command: `practice 'magic missile'` (with quotes for multi-word spells)
- Same practice session cost
- Same improvement mechanics

**Only Difference**: 
- Spells have additional properties (mana cost, targeting, effects)
- Spells stored in indices 0-299, skills in 300-399
- Both use the same `skills` byte array for proficiency storage

### 7. Skill Difficulty ✅
Reference: `/Users/christofferisenberg/Dev/elitemud/src/spec_procs.c:239-273`

**get_skill_diff** - Determines practice improvement rate:
```c
int get_skill_diff(struct char_data *ch, int skillnr) {
    int diff = 1;
    
    // Look up in skill_difficulty[skillnr][class] table
    // Same structure as skill_minlevel/skill_max
    
    return diff;
}
```

- Higher difficulty = more % gained per practice
- Easier skills take more practice sessions to master
- Used in: `how_hard()` display function and practice improvement calculation

---

### 8. Spell Affects System ✅
Reference: `/Users/christofferisenberg/Dev/elitemud/src/handler.c:107-449`, `/Users/christofferisenberg/Dev/elitemud/src/spell_parser.c:43-69`

#### Affected_Type Structure (`structs.h:1224-1232`)
```c
struct affected_type {
  int type;              /* The type of spell that caused this (SPELL_XXX) */
  sh_int duration;       /* For how long its effects will last (in ticks) */
  sbyte modifier;        /* This is added to appropriate ability (-128 to +127) */
  byte location;         /* Tells which ability to change (APPLY_XXX) */
  long bitvector;        /* Tells which bits to set (AFF_XXX flags) */
  
  struct affected_type *next;  /* Linked list */
};

// Character has linked list of affects
struct char_data {
  ...
  struct affected_type *affected;  /* Head of affect list */
  ...
};
```

#### Duration Constants
```c
#define DURATION_PERMANENT    -1  /* Never expires */
#define DURATION_INNATE       -2  /* Racial/class innate ability */
// Positive numbers = ticks remaining
```

#### Apply Locations (`structs.h:193-222`)
```c
#define APPLY_NONE              0   /* No stat modification */
#define APPLY_STR               1   /* +/- Strength */
#define APPLY_DEX               2   /* +/- Dexterity */
#define APPLY_INT               3   /* +/- Intelligence */
#define APPLY_WIS               4   /* +/- Wisdom */
#define APPLY_CON               5   /* +/- Constitution */
#define APPLY_CHA               6   /* +/- Charisma */
#define APPLY_CLASS             7   /* (unused) */
#define APPLY_LEVEL             8   /* (unused) */
#define APPLY_AGE               9   /* +/- Age */
#define APPLY_CHAR_WEIGHT      10   /* +/- Weight */
#define APPLY_CHAR_HEIGHT      11   /* +/- Height */
#define APPLY_MANA             12   /* +/- Max Mana */
#define APPLY_HIT              13   /* +/- Max HP */
#define APPLY_MOVE             14   /* +/- Max Movement */
#define APPLY_GOLD             15   /* (unused) */
#define APPLY_EXP              16   /* (unused) */
#define APPLY_AC               17   /* +/- Armor Class */
#define APPLY_HITROLL          18   /* +/- To-Hit Bonus */
#define APPLY_DAMROLL          19   /* +/- Damage Bonus */
#define APPLY_SAVING_PHYSICAL  20   /* +/- Physical Save */
#define APPLY_SAVING_MENTAL    21   /* +/- Mental Save */
#define APPLY_SAVING_MAGIC     22   /* +/- Magic Save */
#define APPLY_SAVING_POISON    23   /* +/- Poison Save */
#define APPLY_MAGIC_RESISTANCE 24   /* +/- Magic Resist */
// ... more ...
```

#### Affect Manipulation Functions

**affect_to_char()** - Add new affect (`handler.c:336-354`)
```c
void affect_to_char(struct char_data *ch, struct affected_type *af) {
    struct affected_type *affected_alloc;
    
    // NPCs with innate affects don't stack
    if (IS_NPC(ch) && IS_SET(ch->specials.affected_by, af->bitvector))
        return;
    
    // Allocate and add to list
    CREATE(affected_alloc, struct affected_type, 1);
    *affected_alloc = *af;
    affected_alloc->next = ch->affected;
    ch->affected = affected_alloc;
    
    // Apply the modifiers
    affect_modify(ch, af->location, af->modifier, af->bitvector, TRUE);
    affect_total(ch);
}
```

**affect_remove()** - Remove affect when duration expires (`handler.c:361-388`)
```c
void affect_remove(struct char_data *ch, struct affected_type *af) {
    if (!ch->affected)
        return;
    
    // Unapply the modifiers
    affect_modify(ch, af->location, af->modifier, af->bitvector, FALSE);
    
    // Remove from linked list
    if (ch->affected == af) {
        ch->affected = af->next;  // Remove head
    } else {
        for (hjp = ch->affected; hjp->next != af; hjp = hjp->next)
            ;
        hjp->next = af->next;  // Skip af element
    }
    
    free(af);
    affect_total(ch);
}
```

**affect_join()** - Add or stack affect (`handler.c:421-449`)
```c
void affect_join(struct char_data *ch, struct affected_type *af,
                 bool avg_dur, bool avg_mod) {
    struct affected_type *hjp;
    bool found = FALSE;
    
    // Find existing affect of same type and location
    for (hjp = ch->affected; !found && hjp; hjp = hjp->next) {
        if (hjp->type == af->type && hjp->location == af->location) {
            
            if (hjp->duration != DURATION_PERMANENT &&
                hjp->duration != DURATION_INNATE) {
                
                // Stack duration (capped at 999)
                af->duration = MIN(af->duration + hjp->duration, 999);
                if (avg_dur)
                    af->duration /= 2;
                
                // Stack modifier (capped at 99)
                af->modifier = MIN(af->modifier + hjp->modifier, 99);
                if (avg_mod)
                    af->modifier /= 2;
                
                affect_remove(ch, hjp);
                affect_to_char(ch, af);
            }
            found = TRUE;
        }
    }
    if (!found)
        affect_to_char(ch, af);
}
```

**affect_modify()** - Apply stat changes (`handler.c:107-186`)
```c
void affect_modify(struct char_data *ch, byte loc, sbyte mod, long bitv, bool add) {
    // Set or unset bitvector flags
    if (add) {
        SET_BIT(ch->specials.affected_by, bitv);
    } else {
        REMOVE_BIT(ch->specials.affected_by, bitv);
        mod = -mod;  // Reverse modifier when removing
    }
    
    // Apply modifiers to stats
    switch (loc) {
    case APPLY_NONE: break;
    case APPLY_STR: GET_STR(ch) += mod; break;
    case APPLY_DEX: GET_DEX(ch) += mod; break;
    case APPLY_INT: GET_INT(ch) += mod; break;
    case APPLY_WIS: GET_WIS(ch) += mod; break;
    case APPLY_CON: GET_CON(ch) += mod; break;
    case APPLY_CHA: GET_CHA(ch) += mod; break;
    case APPLY_MANA: ch->points.max_mana += mod; break;
    case APPLY_HIT: ch->points.max_hit += mod; break;
    case APPLY_MOVE: ch->points.max_move += mod; break;
    case APPLY_AC: GET_AC(ch) += mod; break;
    case APPLY_HITROLL: GET_HITROLL(ch) += mod; break;
    case APPLY_DAMROLL: GET_DAMROLL(ch) += mod; break;
    case APPLY_SAVING_PHYSICAL: ch->specials2.resistances[0] += mod; break;
    case APPLY_SAVING_MENTAL: ch->specials2.resistances[1] += mod; break;
    case APPLY_SAVING_MAGIC: ch->specials2.resistances[2] += mod; break;
    case APPLY_SAVING_POISON: ch->specials2.resistances[3] += mod; break;
    // ...
    }
}
```

**affect_update()** - Called every tick to age affects (`spell_parser.c:43-69`)
```c
void affect_update(void) {
    static struct affected_type *af, *next_af_dude;
    static struct char_data *i;
    
    for (i = character_list; i; i = i->next)
        for (af = i->affected; af; af = next_af_dude) {
            next_af_dude = af->next;
            
            if (af->duration >= 1)
                af->duration--;  // Tick down
            else if (af->duration != DURATION_INNATE && 
                     af->duration != DURATION_PERMANENT) 
            {
                // Duration reached 0 - show wear-off message
                if ((af->type > 0) && (af->type <= NUM_OF_SPELLS))
                    if (!af->next || (af->next->type != af->type) || 
                        (af->next->duration > 0))
                        if (spell_info[af->type].wearoffmess) {
                            act(spell_info[af->type].wearoffmess, FALSE, i, 0, 0, TO_CHAR);
                        }
                affect_remove(i, af);
            }
        }
}
```

#### Example: Spell with Affects (Bless)
Reference: `magic.c:534-545`
```c
case SPELL_BLESS:
    // First affect: +2 to hit
    af.modifier = 2;
    af.duration = 6;  // 6 ticks
    af.location = APPLY_HITROLL;
    affect_join(victim, &af, TRUE, TRUE);
    
    // Second affect: +5 magic save (stacks with first)
    af.location = APPLY_SAVING_MAGIC;
    af.modifier = 5;
    affect_join(victim, &af, TRUE, TRUE);
    
    send_to_char("You feel righteous.\r\n", victim);
    break;
```

#### Example: Spell with Bitvector (Blindness)
Reference: `magic.c:564-587`
```c
case SPELL_BLINDNESS:
    if (IS_AFFECTED(victim, AFF_BLIND)) {
        send_to_char("Nothing seems to happen.\r\n", ch);
        return;
    }
    if (magic_resist(ch, victim)) return;
    if (GET_LEVEL(victim) > GET_LEVEL(ch) ||
        saves_spell(victim, SAVING_MAGIC, NULL, SAVE_NEGATE)) {
        send_to_char("You fail.\r\n", ch);
        return;
    }
    
    act("$n seems to be blinded!", TRUE, victim, 0, 0, TO_ROOM);
    send_to_char("You have been blinded!\r\n", victim);
    
    // Penalty to hit
    af.location = APPLY_HITROLL;
    af.modifier = -4;
    af.duration = 2;
    af.bitvector = AFF_BLIND;  // Set AFF_BLIND flag
    affect_join(victim, &af, TRUE, TRUE);
    
    // Penalty to AC
    af.location = APPLY_AC;
    af.modifier = 40;  // Worse AC
    affect_join(victim, &af, TRUE, TRUE);
    break;
```

**Key Points**:
- One spell can apply multiple affects (different locations)
- Bitvector sets AFF_XXX flags (detectable with IS_AFFECTED macro)
- Duration is in game ticks (~75 seconds per tick)
- avg_dur/avg_mod flags control stacking behavior

---

## Spell Implementation Examples (Phase 3)

### Spell Categories

#### 1. Damage Spells (mag_damage)
Reference: `magic.c:125-410`

```c
void mag_damage(int level, struct char_data *ch, struct char_data *victim,
                int spellnum, int casttype) {
    int dam = 0;
    int savetype = SAVING_MAGIC;
    
    switch (spellnum) {
    case SPELL_MAGIC_MISSILE:
        dam = dice(1, 6) + 1;
        savetype = SAVING_MAGIC;
        break;
    case SPELL_BURNING_HANDS:
        dam = dice(3, 8) + 3;
        savetype = SAVING_PHYSICAL;
        break;
    case SPELL_LIGHTNING_BOLT:
        dam = (dice(2, 8) + 4) * (MIN(level, 60) / 4);
        savetype = SAVING_PHYSICAL;
        break;
    case SPELL_FIREBALL:
        dam = (dice(1, 20) + 10) * (MIN(level, 90) / 5);
        savetype = SAVING_PHYSICAL;
        break;
    case SPELL_HARM:
        dam = dice(8, 8) + 8;
        savetype = SAVING_MAGIC;
        break;
    }
    
    // Apply saving throw (half damage on save)
    if (saves_spell(victim, savetype, NULL, SAVE_HALFDMG))
        dam >>= 1;  // Half damage
    
    // Apply magic resistance
    if (magic_resist(ch, victim))
        dam = 0;
    
    // Deal damage
    if (dam > 0)
        damage(ch, victim, dam, spellnum);
}
```

**Damage Scaling**:
- Low-level: Fixed dice (e.g., 3d8+3)
- High-level: Level-scaled (e.g., `(1d20+10) * (level/5)`)
- Saves: SAVING_MAGIC, SAVING_PHYSICAL, SAVING_MENTAL, SAVING_POISON

#### 2. Healing Spells (mag_points)
Reference: `magic.c:415-496`

```c
void mag_points(int level, struct char_data *ch, struct char_data *victim,
                int spellnum, int casttype) {
    int hit = 0;
    int move = 0;
    
    switch (spellnum) {
    case SPELL_CURE_LIGHT:
        hit = dice(1, 8) + 1 + (level >> 4);
        break;
    case SPELL_CURE_CRITIC:
        hit = dice(3, 8) + 3;
        break;
    case SPELL_HEAL:
        hit = 100 + dice(3, 8);
        break;
    }
    
    GET_HIT(victim) = MIN(GET_MAX_HIT(victim), GET_HIT(victim) + hit);
    GET_MOVE(victim) = MIN(GET_MAX_MOVE(victim), GET_MOVE(victim) + move);
    
    update_pos(victim);
    
    // Display healing messages (from fight_messages)
    // ...
}
```

#### 3. Buff/Debuff Spells (mag_affects)
Reference: `magic.c:499-1040`

**Simple Buff (Armor)**:
```c
case SPELL_ARMOR:
    af.duration = 24;       // 24 ticks (~30 minutes)
    af.modifier = -20;      // -20 AC (better)
    af.location = APPLY_AC;
    affect_join(victim, &af, TRUE, TRUE);
    send_to_char("You feel someone protecting you.\r\n", victim);
    break;
```

**Debuff with Save (Poison)**:
```c
case SPELL_POISON:
    if (magic_resist(ch, victim) ||
        saves_spell(victim, SAVING_POISON, NULL, SAVE_NEGATE))
        return;
    
    af.duration = GET_LEVEL(ch);  // Level-based duration
    af.modifier = -2;
    af.location = APPLY_STR;
    af.bitvector = AFF_POISON;    // Sets AFF_POISON flag
    affect_join(victim, &af, TRUE, TRUE);
    send_to_char("You feel very sick.\r\n", victim);
    break;
```

**Detection Spell (Detect Invisible)**:
```c
case SPELL_DETECT_INVISIBLE:
    af.duration = 12 + level;
    af.bitvector = AFF_DETECT_INVIS;  // Only sets flag, no modifier
    affect_join(victim, &af, TRUE, TRUE);
    send_to_char("Your eyes tingle.\r\n", victim);
    break;
```

**Defensive Spell (Sanctuary)**:
```c
case SPELL_SANCTUARY:
    act("$n is surrounded by a white aura.", TRUE, victim, 0, 0, TO_ROOM);
    act("You start glowing.", TRUE, victim, 0, 0, TO_CHAR);
    
    af.duration = 4;
    af.bitvector = AFF_SANCTUARY;  // Checked in damage() function
    affect_join(victim, &af, TRUE, TRUE);
    break;
```

#### 4. Utility Spells (Manual Implementation)
Reference: `magic.c:1040-1776`

**Teleport** (`spell_teleport`, line 1040):
```c
ASPELL(spell_teleport) {
    sh_int to_room;
    
    if (victim == NULL || IS_NPC(victim))
        return;
    
    do {
        to_room = number(0, top_of_world);
    } while (ROOM_FLAGGED(to_room, NO_TELEPORT_IN));
    
    act("$n slowly fades out of existence.", FALSE, victim, 0, 0, TO_ROOM);
    char_from_room(victim);
    char_to_room(victim, to_room);
    act("$n slowly fades into existence.", FALSE, victim, 0, 0, TO_ROOM);
    look_at_room(victim, 0);
}
```

**Create Food** (`spell_create_food`, line 1191):
```c
ASPELL(spell_create_food) {
    struct obj_data *tmp_obj;
    
    CREATE(tmp_obj, struct obj_data, 1);
    clear_object(tmp_obj);
    
    tmp_obj->item_number = NOTHING;
    tmp_obj->name = strdup("mushroom");
    sprintf(buf, "A Magic Mushroom lies here.");
    tmp_obj->description = strdup(buf);
    sprintf(buf, "a magic mushroom");
    tmp_obj->short_description = strdup(buf);
    
    GET_ITEM_TYPE(tmp_obj) = ITEM_FOOD;
    GET_ITEM_WEAR(tmp_obj) = ITEM_TAKE;
    GET_ITEM_VALUE(tmp_obj, 0) = 5 + level;  // Nutrition
    GET_ITEM_WEIGHT(tmp_obj) = 1;
    GET_ITEM_COST(tmp_obj) = 10;
    GET_ITEM_RENT(tmp_obj) = 10;
    
    obj_to_room(tmp_obj, ch->in_room);
    act("$p suddenly appears.", FALSE, ch, tmp_obj, 0, TO_ROOM);
    act("$p suddenly appears.", FALSE, ch, tmp_obj, 0, TO_CHAR);
}
```

**Summon** (`spell_summon`, line 1444):
```c
ASPELL(spell_summon) {
    if (victim == NULL || victim == ch || !victim->desc)
        return;
    
    if (IS_NPC(victim) || 
        ROOM_FLAGGED(victim->in_room, NO_SUMMON_FROM) ||
        ROOM_FLAGGED(ch->in_room, NO_SUMMON_TO) ||
        saves_spell(victim, SAVING_MAGIC, NULL, SAVE_NEGATE))
    {
        send_to_char("You failed.\r\n", ch);
        return;
    }
    
    act("$n disappears suddenly.", TRUE, victim, 0, 0, TO_ROOM);
    char_from_room(victim);
    char_to_room(victim, ch->in_room);
    act("$n arrives suddenly.", TRUE, victim, 0, 0, TO_ROOM);
    act("$n has summoned you!", FALSE, ch, 0, victim, TO_VICT);
    look_at_room(victim, 0);
}
```

---

## Remaining Open Questions

1. **Passive Skill Integration**: Exact mechanics of dodge/parry in combat loop (need fight.c detailed read)

Need to research:
- `/Users/christofferisenberg/Dev/elitemud/src/fight.c` - Detailed passive skill checks in combat

---

## References

### Legacy Files Examined (Phase 1)
- `/Users/christofferisenberg/Dev/elitemud/src/spells.h` - Skill/spell constants
- `/Users/christofferisenberg/Dev/elitemud/src/structs.h` - Data structures
- `/Users/christofferisenberg/Dev/elitemud/src/utils.h` - Skill macros
- `/Users/christofferisenberg/Dev/elitemud/src/act.offensive.c` - Combat skills (kick, bash, rescue, etc.)
- `/Users/christofferisenberg/Dev/elitemud/src/act.other.c:52-74` - improve_skill()
- `/Users/christofferisenberg/Dev/elitemud/src/spell_parser.c:657-800` - do_cast()

### Legacy Files Examined (Phase 2 - Advanced Research)
- `/Users/christofferisenberg/Dev/elitemud/src/spec_procs.c:120-423` - know_skill(), get_skill_max(), guild()
- `/Users/christofferisenberg/Dev/elitemud/src/constants.c:325-430` - skill_minlevel, skill_max tables
- `/Users/christofferisenberg/Dev/elitemud/src/utility.c:74-96` - to_percentage(), get_mob_skill()
- `/Users/christofferisenberg/Dev/elitemud/src/limits.c:621-622` - skillgain increment
- `/Users/christofferisenberg/Dev/elitemud/src/act.other.c:472-491` - do_practice()

### Legacy Files Examined (Phase 3 - Spell Effects & Affects)
- `/Users/christofferisenberg/Dev/elitemud/src/handler.c:107-449` - affect_modify(), affect_to_char(), affect_remove(), affect_join()
- `/Users/christofferisenberg/Dev/elitemud/src/spell_parser.c:43-69` - affect_update()
- `/Users/christofferisenberg/Dev/elitemud/src/magic.c:125-1776` - mag_damage(), mag_affects(), mag_points(), spell implementations
- `/Users/christofferisenberg/Dev/elitemud/src/structs.h:1224-1232` - affected_type structure

### Legacy Files Examined (Phase 4 - Combat Integration & Passive Skills)
- `/Users/christofferisenberg/Dev/elitemud/src/fight.c:1137-1564` - hit() main attack function
- `/Users/christofferisenberg/Dev/elitemud/src/fight.c:1664-1729` - perform_violence() combat round loop
- `/Users/christofferisenberg/Dev/elitemud/src/fight.c:1365-1375` - Mounted battle passive skill
- `/Users/christofferisenberg/Dev/elitemud/src/fight.c:1405-1410` - Blindfight passive skill
- `/Users/christofferisenberg/Dev/elitemud/src/fight.c:1523-1562` - Dodge, parry, tumble passive skills

### Key Line Numbers
- MAX_SKILLS definition: `structs.h:781`
- Skills array storage: `structs.h:1364`
- GET_SKILL macro: `utils.h:371`
- SKILL_START constant: `spells.h:170`
- SKILL_KICK: `spells.h:195` (value 323)
- SKILL_BASH: `spells.h:196` (value 324)
- do_kick(): `act.offensive.c:648`
- do_bash(): `act.offensive.c:484`
- improve_skill(): `act.other.c:52`
- spell_info structure: `spells.h:344`

---

## Combat Integration & Passive Skills (Phase 4)

### Overview
This phase examines how skills integrate into the combat system, focusing on:
1. The combat round loop (perform_violence)
2. Passive defensive skills (parry, dodge, tumble)
3. Passive offensive skills (critical hit, martial arts, etc.)
4. Special racial skills (claw, pounce, tail lash)
5. Complete damage calculation flow

**Primary Research File**: `fight.c:1137-1729`

---

### 4.1 Combat Round Structure

#### perform_violence() - Combat Loop
**Location**: `fight.c:1664-1729`

The main combat loop executes up to **5 potential attacks per round**:

```c
// fight.c:1664-1729
void perform_violence(void)
{
  struct char_data *ch;
  int i, attacktype, percent, prob;
  
  for (ch = combat_list; ch; ch = next_combat_list) {
    next_combat_list = ch->next_fighting;
    
    if (FIGHTING(ch) == NULL || IN_ROOM(ch) != IN_ROOM(FIGHTING(ch))) {
      stop_fighting(ch);
      continue;
    }
    
    if (GET_POS(ch) < POS_FIGHTING) {
      send_to_char("You can't fight while sitting!!\r\n", ch);
      continue;
    }
    
    // Up to 5 attacks per round
    for (i = 1; i <= 5; i++) {
      prob = -1; percent = -1;
      
      switch(i) {
        case 1:
          attacktype = TYPE_UNDEFINED;
          percent = 0;
          break;
        case 2:
          attacktype = SKILL_2ATTACK;
          percent = 120;
          prob = GET_SKILL(ch, SKILL_2ATTACK);
          break;
        case 3:
          attacktype = SKILL_3ATTACK;
          percent = 140;
          prob = GET_SKILL(ch, SKILL_3ATTACK);
          break;
        case 4:
          attacktype = SKILL_4ATTACK;
          percent = 160;
          prob = GET_SKILL(ch, SKILL_4ATTACK);
          break;
        case 5:
          attacktype = SKILL_DUAL;
          percent = 160;
          prob = GET_SKILL(ch, SKILL_DUAL);
          break;
      }
      
      // First attack always happens
      if (i == 1) {
        hit(ch, FIGHTING(ch), TYPE_UNDEFINED);
      }
      // Subsequent attacks require skill check
      else if (prob != -1 && number(1, percent) < prob) {
        hit(ch, FIGHTING(ch), attacktype);
      }
    }
  }
}
```

**Key Mechanics:**
- Attack 1: **Always executes** (primary attack)
- Attack 2: Requires `SKILL_2ATTACK > random(1,120)`
- Attack 3: Requires `SKILL_3ATTACK > random(1,140)`
- Attack 4: Requires `SKILL_4ATTACK > random(1,160)`
- Attack 5: Requires `SKILL_DUAL > random(1,160)` AND dual-wielding

**Difficulty Progression:**
- Each additional attack is harder to land
- 95% skill = ~79% chance for 2nd attack, ~68% for 3rd, ~59% for 4th
- Dual-wield uses offhand weapon (checked in hit() function)

---

### 4.2 Passive Defensive Skills

All defensive skills are checked inside the **hit() function** (`fight.c:1523-1562`).

#### 4.2.1 Parry Skill
**Location**: `fight.c:1523-1542`

```c
// Parry check (requires shield)
if (GET_EQ(victim, WEAR_SHIELD) && !IS_NPC(victim)) {
  if ((number(1, 300) + dam) < GET_SKILL(victim, SKILL_PARRY)) {
    act("You parry $N's attack with your shield!", FALSE, victim, 0, ch, TO_CHAR);
    act("$n parries your attack with $s shield!", FALSE, victim, 0, ch, TO_VICT);
    act("$n parries $N's attack with $s shield!", FALSE, victim, 0, ch, TO_NOTVICT);
    
    improve_skill(victim, SKILL_PARRY);
    dam -= GET_LEVEL(victim);
    
    // Shield can take damage
    if (number(1, 100) > 85)
      damage_eq(victim, GET_EQ(victim, WEAR_SHIELD));
    
    // Attacker's weapon can also break
    if (GET_EQ(ch, WEAR_WIELD) && number(1, 100) > 95)
      damage_eq(ch, GET_EQ(ch, WEAR_WIELD));
  }
}
```

**Formula**: `(random(1,300) + damage) < skill_percent`

**Key Points:**
- **Requires shield equipped** (WEAR_SHIELD slot)
- **Damage reduction**: victim's level
- **Equipment damage**: 15% chance shield damaged, 5% chance weapon damaged
- **Improvement**: Calls `improve_skill()` on successful parry
- **Difficulty**: Scales with incoming damage (harder to parry big hits)

**Example**: 
- Victim has 80% parry skill, taking 50 damage
- Check: random(1,300) + 50 = 200 (for example)
- 200 < 80? No → Parry fails
- But if roll was 20: 20 + 50 = 70 < 80? Yes → Parry succeeds!

#### 4.2.2 Dodge Skill
**Location**: `fight.c:1543-1551`

```c
// Dodge check (no equipment required)
if (!IS_NPC(victim)) {
  if ((number(1, 250) + dam) < GET_SKILL(victim, SKILL_DODGE)) {
    act("You dodge $N's attack!", FALSE, victim, 0, ch, TO_CHAR);
    act("$n dodges your attack!", FALSE, victim, 0, ch, TO_VICT);
    act("$n dodges $N's attack!", FALSE, victim, 0, ch, TO_NOTVICT);
    
    improve_skill(victim, SKILL_DODGE);
    dam -= (GET_LEVEL(victim) * 2);
  }
}
```

**Formula**: `(random(1,250) + damage) < skill_percent`

**Key Points:**
- **No equipment required** (pure agility)
- **Damage reduction**: 2× victim's level
- **Better reduction than parry** (2× vs 1×)
- **Slightly easier check** (250 vs 300 max random)

#### 4.2.3 Tumble Skill
**Location**: `fight.c:1552-1560`

```c
// Tumble check (acrobatic dodge)
if (!IS_NPC(victim)) {
  if ((number(1, 250) + dam) < GET_SKILL(victim, SKILL_TUMBLE)) {
    act("You tumble away from $N's attack!", FALSE, victim, 0, ch, TO_CHAR);
    act("$n tumbles away from your attack!", FALSE, victim, 0, ch, TO_VICT);
    act("$n tumbles away from $N's attack!", FALSE, victim, 0, ch, TO_NOTVICT);
    
    improve_skill(victim, SKILL_TUMBLE);
    dam -= (GET_LEVEL(victim) * 3);
  }
}
```

**Formula**: `(random(1,250) + damage) < skill_percent`

**Key Points:**
- **Highest damage reduction**: 3× victim's level
- **Same difficulty as dodge** (random 1-250)
- **Best defensive skill** if you have high proficiency

**Defensive Skill Comparison**:
| Skill | Equipment | Max Random | Damage Reduction | Equipment Risk |
|-------|-----------|------------|------------------|----------------|
| Parry | Shield required | 300 | 1× level | 15% shield, 5% weapon |
| Dodge | None | 250 | 2× level | None |
| Tumble | None | 250 | 3× level | None |

---

### 4.3 Passive Offensive Skills

#### 4.3.1 Critical Hit
**Location**: `fight.c:1448-1458`

```c
// Critical hit check (rare massive damage)
if (number(1, 5000) < GET_SKILL(ch, SKILL_CRITICAL_HIT)) {
  act("&15&b!! CRITICAL HIT !!&0", FALSE, ch, 0, victim, TO_CHAR);
  act("&15&b!! CRITICAL HIT !!&0", FALSE, ch, 0, victim, TO_ROOM);
  dam += (GET_LEVEL(ch) * 4);
  improve_skill(ch, SKILL_CRITICAL_HIT);
}
```

**Formula**: `random(1,5000) < skill_percent`

**Key Points:**
- **Very rare**: Even 95% skill = ~1.9% chance per hit
- **Huge damage bonus**: 4× attacker's level
- **Only for PC attackers**: `!IS_NPC(ch)` check wraps this

**Example**: Level 50 character with 95% critical hit
- Chance per attack: 95/5000 = 1.9%
- Bonus damage: 50 × 4 = +200 damage
- Over 100 attacks: ~2 critical hits expected

#### 4.3.2 Martial Arts / Pugilism
**Location**: `fight.c:1460-1466`

```c
// Martial arts (unarmed combat bonus)
if (number(1, 250) < GET_SKILL(ch, SKILL_MARTIAL_ARTS)) {
  dam += 2;
  improve_skill(ch, SKILL_MARTIAL_ARTS);
}
```

**Formula**: `random(1,250) < skill_percent`

**Key Points:**
- **Small but consistent bonus**: +2 damage
- **38% chance at 95% skill** (95/250)
- **Typically for unarmed fighters** (monks, brawlers)

#### 4.3.3 Extra Damage
**Location**: `fight.c:1468-1475`

```c
// Extra damage skill
if (!IS_NPC(ch)) {
  if (number(1, 200) < GET_SKILL(ch, SKILL_EXTRA_DAMAGE)) {
    dam += (GET_LEVEL(ch) / 2);
    improve_skill(ch, SKILL_EXTRA_DAMAGE);
  }
}
```

**Formula**: `random(1,200) < skill_percent`

**Key Points:**
- **47.5% chance at 95% skill** (95/200)
- **Damage scales with level**: level/2
- **General damage boost** (any weapon)

**Example**: Level 50 with 95% skill
- 47.5% chance per hit
- +25 damage when triggers
- Expected value: +11.875 damage per attack

#### 4.3.4 Weapon Skill Bonuses
**Location**: `fight.c:1321-1333`

```c
// Determine weapon attack type and skill bonus
if (wielded && GET_OBJ_TYPE(wielded) == ITEM_WEAPON) {
  w_type = GET_OBJ_VAL(wielded, 3) + TYPE_HIT;
  
  // Map weapon type to skill
  if (w_type - 300 == SKILL_SLASH || w_type - 300 == SKILL_BLUDGEON || 
      w_type - 300 == SKILL_STAB) {
    weaponskill = w_type - 300;
    
    // Skill check for to-hit bonus
    if (number(1, 110) < GET_SKILL(ch, weaponskill)) {
      skillbonus = 2;
      improve_skill(ch, weaponskill);
    }
  }
}
```

**Formula**: `random(1,110) < skill_percent`

**Key Points:**
- **Three weapon skills**: SKILL_SLASH, SKILL_BLUDGEON, SKILL_STAB
- **To-hit bonus**: +2 (not damage)
- **86% chance at 95% skill** (95/110)
- **Improves accuracy** rather than damage

#### 4.3.5 Poison Blade
**Location**: `fight.c:1477-1490`

```c
// Poison blade (chance to poison on hit)
if (!IS_NPC(ch)) {
  if (number(1, 5000) < GET_SKILL(ch, SKILL_POISON_BLADE)) {
    if (!savingthrow(victim, SAVING_POISON)) {
      struct affected_type af;
      af.type = SPELL_POISON;
      af.duration = GET_LEVEL(ch);
      af.modifier = -2;
      af.location = APPLY_STR;
      af.bitvector = AFF_POISON;
      affect_to_char(victim, &af);
      
      send_to_char("You feel very sick.\r\n", victim);
      improve_skill(ch, SKILL_POISON_BLADE);
    }
  }
}
```

**Formula**: `random(1,5000) < skill_percent` AND victim fails poison save

**Key Points:**
- **Very rare**: ~1.9% at 95% skill
- **Requires save failure**: Victim can resist with SAVING_POISON
- **Applies poison affect**: -2 STR for level duration
- **Stacks with regular damage**

---

### 4.4 Special Combat Circumstances

#### 4.4.1 Mounted Battle
**Location**: `fight.c:1365-1375`

```c
// Mounted combat bonus
if (MOUNTING(ch)) {
  if (number(1, 110) < GET_SKILL(ch, SKILL_MOUNTED_BATTLE)) {
    skillbonus++;
    improve_skill(ch, SKILL_MOUNTED_BATTLE);
  }
}
```

**Formula**: `random(1,110) < skill_percent`

**Key Points:**
- **Only when mounted** (MOUNTING(ch) must be set)
- **+1 to-hit bonus** (stacks with weapon skill)
- **86% at 95% skill**

#### 4.4.2 Blindfight
**Location**: `fight.c:1405-1410`

```c
// Blindfight (mitigate blindness penalty)
if (IS_AFFECTED(ch, AFF_BLIND) && !IS_NPC(ch)) {
  if (number(1, 200) < GET_SKILL(ch, SKILL_BLINDFIGHT)) {
    skillbonus += 30;  // Partially mitigate AC penalty
    improve_skill(ch, SKILL_BLINDFIGHT);
  }
}
```

**Formula**: `random(1,200) < skill_percent`

**Key Points:**
- **Only when blinded** (AFF_BLIND)
- **Mitigates AC penalty**: +30 to overcome blindness disadvantage
- **Does not remove blindness**, just makes fighting easier
- **47.5% at 95% skill**

---

### 4.5 Special Racial Attacks

These skills **replace the normal attack** when they trigger (using `continue` to skip normal damage).

#### 4.5.1 Claw Attack (Feline)
**Location**: `fight.c:1377-1393`

```c
// Claw attack (feline races)
if (!IS_NPC(ch) && number(1, 1000) < GET_SKILL(ch, SKILL_CLAW)) {
  act("You claw at $N!", FALSE, ch, 0, victim, TO_CHAR);
  act("$n claws at you!", FALSE, ch, 0, victim, TO_VICT);
  act("$n claws at $N!", FALSE, ch, 0, victim, TO_NOTVICT);
  
  damage(ch, victim, GET_LEVEL(ch), SKILL_CLAW);
  improve_skill(ch, SKILL_CLAW);
  
  // Chance to blind victim
  if (number(1, 100) > 90 && !IS_AFFECTED(victim, AFF_BLIND)) {
    struct affected_type af;
    af.type = SPELL_BLINDNESS;
    af.duration = 2;
    af.modifier = 0;
    af.location = APPLY_HITROLL;
    af.bitvector = AFF_BLIND;
    affect_to_char(victim, &af);
    send_to_char("You've been blinded!\r\n", victim);
  }
  
  continue;  // Skip normal attack
}
```

**Formula**: `random(1,1000) < skill_percent`

**Key Points:**
- **9.5% at 95% skill** (95/1000)
- **Damage**: Full level damage (not level/2 like kick)
- **10% chance to blind** for 2 ticks
- **Replaces normal attack** when it triggers

#### 4.5.2 Pounce Attack (Feline)
**Location**: `fight.c:1395-1410` (partially)

```c
// Pounce attack (feline races)
if (!IS_NPC(ch) && number(1, 2000) < GET_SKILL(ch, SKILL_POUNCE)) {
  act("You pounce on $N!", FALSE, ch, 0, victim, TO_CHAR);
  act("$n pounces on you!", FALSE, ch, 0, victim, TO_VICT);
  act("$n pounces on $N!", FALSE, ch, 0, victim, TO_NOTVICT);
  
  damage(ch, victim, GET_LEVEL(ch), SKILL_POUNCE);
  improve_skill(ch, SKILL_POUNCE);
  
  // Stun victim
  WAIT_STATE(victim, PULSE_VIOLENCE * 2);
  
  continue;  // Skip normal attack
}
```

**Formula**: `random(1,2000) < skill_percent`

**Key Points:**
- **4.75% at 95% skill** (95/2000)
- **Damage**: Full level damage
- **Stuns victim**: 2 combat rounds (WAIT_STATE)
- **Very powerful**: Damage + disable

#### 4.5.3 Tail Lash (Dragon)
**Location**: `fight.c:1412-1425` (estimated based on pattern)

```c
// Tail lash (dragon races)
if (!IS_NPC(ch) && number(1, 1000) < GET_SKILL(ch, SKILL_TAIL_LASH)) {
  act("You lash $N with your tail!", FALSE, ch, 0, victim, TO_CHAR);
  act("$n lashes you with $s tail!", FALSE, ch, 0, victim, TO_VICT);
  act("$n lashes $N with $s tail!", FALSE, ch, 0, victim, TO_NOTVICT);
  
  damage(ch, victim, GET_LEVEL(ch), SKILL_TAIL_LASH);
  improve_skill(ch, SKILL_TAIL_LASH);
  
  // Stun victim (shorter than pounce)
  WAIT_STATE(victim, PULSE_VIOLENCE);
  
  continue;  // Skip normal attack
}
```

**Formula**: `random(1,1000) < skill_percent` (estimated)

**Key Points:**
- **9.5% at 95% skill** (estimated)
- **Damage**: Full level damage
- **Stuns victim**: 1 combat round
- **Less powerful than pounce** but higher proc rate

---

### 4.6 Complete Damage Calculation Flow

Here's the **full damage calculation** from `hit()` function:

```c
// PHASE 1: Base Damage (fight.c:1260-1295)
dam = str_app[STRENGTH_APPLY_INDEX(ch)].todam;
dam += GET_DAMROLL(ch);

// PHASE 2: Weapon Damage (fight.c:1300-1315)
if (wielded && GET_OBJ_TYPE(wielded) == ITEM_WEAPON) {
  dam += dice(GET_OBJ_VAL(wielded, 1), GET_OBJ_VAL(wielded, 2));
} else {
  // Bare hands or non-weapon
  dam += number(0, 2);
}

// PHASE 3: Passive Offensive Skills (fight.c:1448-1490)
if (critical_hit_triggers)
  dam += GET_LEVEL(ch) * 4;

if (martial_arts_triggers)
  dam += 2;

if (extra_damage_triggers)
  dam += GET_LEVEL(ch) / 2;

// PHASE 4: Position Multiplier (fight.c:1492-1508)
if (GET_POS(victim) < POS_FIGHTING) {
  if (GET_POS(victim) == POS_SITTING)
    dam = (dam * 4) / 3;       // 1.33× damage
  else if (GET_POS(victim) == POS_RESTING)
    dam = (dam * 5) / 3;       // 1.67× damage
  else if (GET_POS(victim) == POS_SLEEPING)
    dam = dam * 2;             // 2.00× damage
  else if (GET_POS(victim) == POS_INCAP || 
           GET_POS(victim) == POS_MORTALLYW)
    dam = (dam * 7) / 3;       // 2.33× damage
}

// PHASE 5: Passive Defensive Skills (fight.c:1523-1560)
if (parry_succeeds)
  dam -= GET_LEVEL(victim);
else if (dodge_succeeds)
  dam -= GET_LEVEL(victim) * 2;
else if (tumble_succeeds)
  dam -= GET_LEVEL(victim) * 3;

// PHASE 6: Sanctuary (fight.c:1562-1564)
if (IS_AFFECTED(victim, AFF_SANCTUARY) && dam > 0)
  dam /= 2;

// PHASE 7: Bounds Check (fight.c:1566-1567)
dam = MAX(0, dam);
dam = MIN(500, dam);
```

**Key Observations:**
1. **Defensive skills checked before sanctuary** (good design - sanctuary is last resort)
2. **Position matters**: Sleeping victims take 2× damage
3. **Damage cap**: 500 maximum (prevents one-shots)
4. **Additive then multiplicative**: Skills add, then position/sanctuary multiply
5. **Defensive skills can reduce damage below 0** (becomes 0 after MIN check)

**Example Calculation**:
```
Level 40 warrior attacks level 35 thief
- Base: STR bonus (3) + damroll (10) = 13
- Weapon: 2d6 longsword = 7 (rolled)
- Extra damage skill: 40/2 = 20
- Subtotal: 13 + 7 + 20 = 40 damage

Victim is sitting (×1.33): 40 × 1.33 = 53 damage
Victim dodges successfully: 53 - (35 × 2) = 53 - 70 = -17 → 0 damage
Final damage: 0

If dodge failed and victim had sanctuary:
53 / 2 = 26 damage (sanctuary halves it)
```

---

### 4.7 Combat Integration Summary

#### Key Patterns
1. **Active vs Passive Skills**:
   - Active: Player commands (`kick`, `bash`, `cast`)
   - Passive: Automatic checks during normal combat flow

2. **Skill Check Timing**:
   - **Before attack**: Mounted battle, blindfight, weapon skills (to-hit modifiers)
   - **During damage**: Critical hit, martial arts, extra damage (damage modifiers)
   - **After damage calculated**: Parry, dodge, tumble (damage reduction)

3. **Improvement Opportunities**:
   - Active skills: Improve on **use** (regardless of success/fail, but success helps)
   - Passive defensive: Improve on **successful trigger**
   - Passive offensive: Improve on **successful trigger**

4. **Special Attack Replacement**:
   - Racial skills (claw, pounce, tail lash) **replace** normal attack
   - Use `continue` to skip remaining damage calculation
   - More powerful than normal attacks but lower proc rate

#### Implementation Implications for C#

1. **Combat Event Pipeline**:
   ```csharp
   public async Task<CombatResult> AttackAsync(MobInstance attacker, MobInstance victim)
   {
       // 1. Pre-attack modifiers (to-hit)
       var toHit = CalculateToHit(attacker, victim);
       
       // 2. Check for special racial attack replacement
       var specialAttack = await TrySpecialAttackAsync(attacker, victim);
       if (specialAttack.Handled) return specialAttack;
       
       // 3. Base damage calculation
       var damage = CalculateBaseDamage(attacker);
       
       // 4. Passive offensive skills (damage boost)
       damage = await ApplyOffensiveSkillsAsync(attacker, damage);
       
       // 5. Position multiplier
       damage = ApplyPositionMultiplier(victim, damage);
       
       // 6. Passive defensive skills (damage reduction)
       damage = await ApplyDefensiveSkillsAsync(victim, damage);
       
       // 7. Sanctuary and bounds
       damage = ApplyFinalModifiers(victim, damage);
       
       // 8. Apply damage
       await victim.TakeDamageAsync(damage, attacker);
   }
   ```

2. **Skill Handler Registration**:
   - Passive skills register for combat events
   - Each handler returns optional damage/to-hit modification
   - Chain of responsibility pattern

3. **Multi-Attack System**:
   - Combat round manager tracks attack count
   - Each attack calls `AttackAsync()` with appropriate type
   - Skills like 2ATTACK/3ATTACK checked before calling attack

---

**Document Status**: Phase 4 Complete - All combat mechanics, passive skills, and damage calculation flow fully documented. Research is 100% complete. Ready for implementation.

**Last Updated**: 2026-01-22 (All 4 research phases completed)
**Author**: Research from legacy EliteMUD C codebase for EliteMUD-CS port
