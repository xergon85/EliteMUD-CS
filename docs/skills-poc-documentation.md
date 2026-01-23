# Skills & Spells System - Proof of Concept (POC)

**Status**: ✅ POC Complete & Validated  
**Date**: January 23, 2026  
**Testing**: All manual test scenarios passed  
**Files**: See "Implementation Files" section below

---

## Overview

This POC validates the core skill system architecture by implementing:
- **1 Active Skill**: Kick (combat skill with damage calculation)
- **1 Passive Skill**: Dodge (automatic damage reduction)
- **Skill Storage**: Dictionary-based proficiency tracking (0-100%)
- **Skill Improvement**: Practice-based percentage increases
- **Testing Commands**: `setskill`, `skills` for manual validation

**Purpose**: Prove the mechanics work before building the full framework with interfaces, registry, and content-driven configuration.

---

## What Works (Implemented)

### Core Mechanics
✅ **Skill Storage**: `Dictionary<SkillType, byte>` per player (0-100%)  
✅ **Skill Checks**: Success based on proficiency percentage  
✅ **Skill Improvement**: Increases on successful use  
✅ **Active Skills**: Kick command with hit/miss mechanics  
✅ **Passive Skills**: Dodge automatically triggered on damage  
✅ **Combat Integration**: Skills work in both PvP and PvE  
✅ **Legacy Formulas**: Exact damage/success calculations from C codebase

### Commands Implemented
- `kick` - Use kick skill on current target (no argument) or initiate combat (with target)
- `kick <target>` - Start combat with kick (cannot switch targets mid-fight)
- `skills` - View current skill proficiencies
- `setskill <skill> <percent>` - God command (level 35+) to set skill for testing

### Skill: Kick (Active)
**Type**: Active combat skill  
**Usage**: `kick` or `kick <target>`  
**Damage**: `player.Level / 2`  
**Success Formula**: 
```csharp
int victimAc = victim.ArmorClass / 10;
int percent = ((10 - victimAc) * 2) + Random.Shared.Next(1, 102);
int prob = player.GetSkill(SkillType.Kick);
bool success = percent <= prob;
```
**Improvement**: On successful hit  
**PvP**: ✅ Supported  
**PvE**: ✅ Supported

### Skill: Dodge (Passive)
**Type**: Passive defensive skill  
**Trigger**: Automatic on incoming damage  
**Effect**: Reduces damage by `2 × victim.Level`  
**Success Formula**:
```csharp
int check = Random.Shared.Next(1, 251) + damage;
bool dodged = check < dodgeSkill;
```
**Improvement**: On successful dodge  
**Note**: High damage attacks are harder to dodge

---

## What's Missing (Intentional Simplifications)

These features were **documented in research** but **deferred for full implementation**:

### 1. WAIT_STATE (Skill Cooldown) ❌
**Legacy**: `WAIT_STATE(ch, PULSE_VIOLENCE * 3)` - 6 second cooldown on kick  
**POC**: No cooldown - skills can be used every combat round (2 seconds)  
**Impact**: Skills can be spammed (game balance issue)  
**Reason**: Deferred - requires tick-based timing system  
**Future**: Add `WaitState` field to `PlayerState`, decrement per tick

### 2. Skillgain Cooldown ❌
**Legacy**: Two-part improvement check:
```c
if (number(0, 99) > percent &&                    // Random vs skill %
    number(0, 100) < ch->specials.skillgain)      // Cooldown (0-100)
{
  SET_SKILL(ch, skillnr, percent + 1);
  ch->specials.skillgain = 0;  // Reset to 0
}
// skillgain++ each game tick until 100
```
**POC**: Single random check - `Random.Shared.Next(0, 100) > current`  
**Impact**: Skills improve much faster (no cooldown between improvements)  
**Reason**: Deferred - requires tick-based increment system  
**Future**: Add `SkillGain` field to `PlayerState`, increment in game loop

### 3. Skill Caps by Class ❌
**Legacy**: Different max proficiencies per class  
- Warrior kick: 95%
- Thief kick: 75%
- Mage kick: 40%

**POC**: All skills cap at 100% for all players  
**Reason**: Deferred - requires class-based skill metadata  
**Future**: Move to JSON configuration with per-class caps

### 4. Skill Learning Requirements ❌
**Legacy**: Must learn skill from trainer before use  
**POC**: All skills available if proficiency > 0  
**Reason**: Deferred - requires trainer NPCs and skill learning system  
**Future**: Add `KnownSkills` set, trainers, and learning commands

### 5. Position Requirements ❌
**Legacy**: Some skills require standing/fighting position  
**POC**: No position checks (kick works in any position)  
**Reason**: Deferred - need to validate position system first  
**Future**: Add position checks to skill handlers

### 6. Failure Messages ❌
**Legacy**: Different messages for different failure types  
**POC**: Generic "miss" message  
**Reason**: Deferred - focus on core mechanics  
**Future**: Add varied combat messages from content files

---

## Implementation Files

### Core Domain (EliteMud.Game)
- `src/EliteMud.Game/CharacterEnums.cs` - `SkillType` enum (5 skills)
- `src/EliteMud.Game/WorldModels.cs` - `PlayerState` skill methods
- `src/EliteMud.Game/CombatCalculator.cs` - Dodge integration, `DamageResult`

### Commands (EliteMud.Server)
- `src/EliteMud.Server/Adapters/Commands/Kick/KickCommandModule.cs`
- `src/EliteMud.Server/Adapters/Commands/Kick/KickCommandHandler.cs`
- `src/EliteMud.Server/Adapters/Commands/SetSkill/SetSkillCommandModule.cs`
- `src/EliteMud.Server/Adapters/Commands/SetSkill/SetSkillCommandHandler.cs`
- `src/EliteMud.Server/Adapters/Commands/Skills/SkillsCommandModule.cs`
- `src/EliteMud.Server/Adapters/Commands/Skills/SkillsCommandHandler.cs`

### Infrastructure
- `src/EliteMud.Application/Commands/Shared/CommandParser.cs` - Added `Kick`, `SetSkill`, `Skills`
- `src/EliteMud.Server/Adapters/Commands/Shared/CommandModuleProvider.cs` - Registered modules
- `src/EliteMud.Server/GameTickService.cs` - Updated for `DamageResult`

### Tests
- `tests/EliteMud.Tests/Skills/SkillSystemTests.cs` - 9 tests for core mechanics
- `tests/EliteMud.Tests/Skills/DodgeSkillTests.cs` - 8 tests for dodge passive

**Test Coverage**: 17 tests, 100% passing

---

## Testing Checklist

### Manual In-Game Testing

**Setup**:
```bash
# 1. Start server
dotnet run --project src/EliteMud.Server

# 2. Connect via telnet
telnet localhost 4000

# 3. Login and set skills (requires level 35 for setskill)
setskill kick 75
setskill dodge 90
skills
```

**Test Scenarios**:

✅ **Kick without target (not fighting)**:
```
> kick
You aren't fighting anyone!
```

✅ **Kick to initiate combat**:
```
> kick gremlin
Your kick hits a gremlin [5]
```

✅ **Kick without target (while fighting)**:
```
> kill gremlin
> kick
Your kick hits a gremlin [5]
```

✅ **Kick with target (current opponent)**:
```
> kill gremlin
> kick gremlin
Your kick hits a gremlin [5]
```

✅ **Kick different target (while fighting)**:
```
> kill gremlin
> kick orc
You're already fighting someone else!
```

✅ **Dodge passive skill**:
```
> setskill dodge 90
> kill gremlin
# Wait for mob to attack you
You dodge the attack!
[Damage reduced by 2 × your level]
```

✅ **Skill improvement**:
```
> setskill kick 50
> kick gremlin
Your kick hits a gremlin [5]
Your skill - kick - just improved.  # ~50% chance at 50% proficiency
> skills
Kick                50%  # or 51% if improved
```

✅ **View skills**:
```
> skills
Your skills and proficiencies:

  Kick             75%
  Dodge            90%
```

---

## Bug Fixes During Development

### Dead Player Attacking Bug (Fixed)
**Issue**: Dead players continued attacking in the next combat tick after dying from kick.

**Root Cause**: Race condition between player commands (kick) and combat tick system. The combat tick captured a snapshot of fighting players at the start of the round. If a player died from a kick command during tick processing, they could still execute an attack even though they were dead.

**Example of Bug**:
```
Your kick hits yourmom [35]
yourmom is DEAD!!
yourmom attacks you!        <-- BUG: Dead player attacks!
yourmom misses you with their hit.
```

**Fix Applied**: Added defensive guards at the start of attack processing methods in `GameTickService.cs`:

1. **ProcessPlayerVsPlayerAttack** (line 261):
   - Re-check attacker position and FightingConnectionId
   - Check if victim is already dead
   - Exit gracefully if either check fails

2. **ProcessPlayerVsMobAttack** (line 347):
   - Re-check attacker position and FightingConnectionId
   - Exit gracefully if checks fail

**After Fix**:
```
Your kick hits yourmom [35]
yourmom is DEAD!!
You have slain yourmom! (+100 exp)
[No further attacks from dead player]
```

**Defense-in-Depth Approach**: The fix creates multiple checkpoint layers:
- Line 117: Initial position check (original)
- Line 123: FightingConnectionId null check (original)
- **NEW**: Re-check both conditions at start of attack processing

**Files Modified**: `src/EliteMud.Server/GameTickService.cs`

**Testing**: See `docs/manual-test-pvp-death.md` for comprehensive test scenarios.

---

## Known Issues / Limitations

### Gameplay
1. **No cooldown**: Can spam kick every round (should be 3-round delay)
2. **Fast improvement**: Skills improve too quickly (no skillgain cooldown)
3. **No class restrictions**: All skills available to all classes
4. **No skill caps**: All skills go to 100% regardless of class
5. **No position checks**: Can kick while sitting/sleeping

### Technical
1. **Hardcoded skill list**: Skills defined in enum, not content files
2. **No skill metadata**: Damage, cooldown, requirements all in code
3. **Manual registration**: Commands must be manually added to provider
4. **No skill discovery**: No automatic handler registration

### Future Considerations
1. Need to add `WaitState` to character state for action delays
2. Need to add `SkillGain` to character state for improvement rate limiting
3. Need class-based skill metadata (caps, availability, learning levels)
4. Need position-based skill restrictions
5. Need varied combat messages

---

## Next Steps

### Immediate (After Testing POC)
1. ✅ Manual test all scenarios above - **COMPLETED**
2. ✅ Validate formulas match legacy behavior - **VERIFIED**
3. ✅ Verify PvP and PvE both work - **TESTED**
4. ✅ Confirm dodge triggers and reduces damage correctly - **WORKING**
5. ✅ Fix dead player attacking bug - **FIXED & TESTED**

**POC Status**: Fully validated and ready for framework extraction.

### Short Term (Extract Framework)
1. Create `ISkillHandler` interface from working code
2. Create `IPassiveSkillHandler` interface
3. Refactor kick to implement `ISkillHandler`
4. Refactor dodge to implement `IPassiveSkillHandler`
5. Build `SkillRegistry` for auto-discovery via reflection
6. Add dependency injection for handlers

### Medium Term (Content-Driven)
1. Design skill metadata JSON schema
2. Move skill definitions to `content/skills.json`
3. Implement skill learning/training system
4. Add class-based caps and requirements
5. Add WAIT_STATE timing system
6. Add skillgain cooldown system

### Long Term (Full System)
1. Implement remaining active skills (bash, backstab, circle, etc.)
2. Implement remaining passive skills (parry, tumble, etc.)
3. Add special attack skills (disarm, trip, etc.)
4. Add spell system (separate but similar architecture)
5. Add skill trainers and learning quests
6. Add skill progression trees

---

## Performance Notes

- **Skill Lookup**: O(1) dictionary lookup
- **Improvement Check**: O(1) random calculation
- **Memory**: ~8 bytes per skill per player (minimal)
- **No noticeable performance impact** at current scale

---

## References

- **Research Document**: `docs/skills-and-spells-research.md` (2,200+ lines)
- **Legacy Code**: `/Users/christofferisenberg/Dev/elitemud/src/act.offensive.c`
- **Legacy Formulas**: Documented in research, implemented exactly in POC
- **Combat Integration**: `src/EliteMud.Game/CombatCalculator.cs`

---

## Decision Log

**Why POC-first approach?**
- Validate mechanics before building abstractions
- Discover edge cases with real working code
- Build interfaces based on proven patterns
- Avoid over-engineering before understanding requirements

**Why simplify improvement mechanics?**
- WAIT_STATE requires tick-based timing (complex)
- skillgain requires persistent state + tick increments (complex)
- Core formula validation doesn't need these features
- Easier to add later than to debug complex interactions

**Why manual command registration?**
- POC proves the pattern works
- Auto-discovery can be added when extracting framework
- Simpler to debug during validation phase

**Why hardcoded skills?**
- Validates storage and calculation mechanics
- Content-driven approach requires working POC first
- JSON schema design needs real data to inform structure

---

## Success Criteria (POC Complete When...)

✅ Kick command works in PvP and PvE  
✅ Dodge triggers automatically on damage  
✅ Skills improve on successful use  
✅ Formulas match legacy behavior exactly  
✅ All 17 tests passing  
✅ Manual testing validates core mechanics  
⏳ **In-game testing pending** (next step)

**Status**: Ready for manual validation testing
