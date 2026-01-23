# Manual Test Plan: PvP Death Bug Fix

## Bug Description
Dead players were attacking in the next combat tick after dying from kick.

## Test Scenario 1: Kick Kills Player During Combat

### Setup
1. Start server: `dotnet run --project src/EliteMud.Server`
2. Open two telnet sessions:
   - Session A: Create character "PlayerA"
   - Session B: Create character "PlayerB"
3. Both players should be in the same room (Temple of Midgaard, room 3001)

### Test Steps
1. **Session A**: `setlevel 10` (attacker)
2. **Session A**: `setskill kick 95` (ensure kick hits)
3. **Session B**: `setlevel 1` (victim - low HP to die quickly)
4. **Session A**: `kick PlayerB` (initiate combat)
5. **Session A**: `kick` (attack again)
6. **Session A**: `kick` (repeat until PlayerB dies)

### Expected Results
- ✅ "PlayerB is DEAD!!" message appears
- ✅ Combat stops immediately for both players
- ✅ **NO "PlayerB attacks you!" message after death**
- ✅ PlayerB respawns at starting room
- ✅ PlayerB can move/type commands normally
- ✅ PlayerA can move/type commands normally

### BEFORE Fix
```
Your kick hits PlayerB [35]
PlayerB is DEAD!!
PlayerB attacks you!        <-- BUG: Dead player attacks!
PlayerB misses you with their hit.
```

### AFTER Fix
```
Your kick hits PlayerB [35]
PlayerB is DEAD!!
You have slain PlayerB! (+100 exp)
[No further attacks from PlayerB]
```

## Test Scenario 2: PvP Death During Normal Combat Tick

### Setup
Same as Scenario 1

### Test Steps
1. **Session A**: `setlevel 5`
2. **Session A**: `setskill kick 95`
3. **Session B**: `setlevel 3`
4. **Session A**: `kill PlayerB` (start auto-combat)
5. Wait for combat ticks to process until one player dies

### Expected Results
- ✅ Combat proceeds normally
- ✅ When a player dies, combat stops for both
- ✅ Death messages appear
- ✅ Dead player respawns
- ✅ No attacks from dead player

## Test Scenario 3: Mob Death from Kick

### Test Steps
1. **Session A**: `setlevel 10`
2. **Session A**: `setskill kick 95`
3. **Session A**: Find a low-level mob (e.g., in Midgaard)
4. **Session A**: `kick <mob>` repeatedly until mob dies

### Expected Results
- ✅ Mob dies normally
- ✅ Corpse created
- ✅ Combat stops
- ✅ No attacks from dead mob

## Test Scenario 4: Player Death from Mob

### Test Steps
1. **Session A**: `setlevel 1` (low HP)
2. **Session A**: Find a strong mob
3. **Session A**: `kill <mob>`
4. Wait for mob to kill player

### Expected Results
- ✅ Player dies from mob attacks
- ✅ Death messages appear
- ✅ Player respawns at starting room
- ✅ Mob stops attacking

## Build and Run Tests

```bash
# Build
dotnet build EliteMUD.sln

# Run tests
dotnet test EliteMUD.sln

# Start server for manual testing
dotnet run --project src/EliteMud.Server
```

## Test Results Log

Date: January 23, 2026
Tester: User

| Scenario | Pass/Fail | Notes |
|----------|-----------|-------|
| Scenario 1: Kick kills in combat | ✅ PASS | No attacks from dead player after death |
| Scenario 2: Normal combat death | ✅ PASS | Combat stops cleanly when player dies |
| Scenario 3: Mob death from kick | ✅ PASS | Mob death handled correctly |
| Scenario 4: Player death from mob | ✅ PASS | Player death and respawn working |

**Result**: All test scenarios passed. Bug fix verified working correctly.

## Additional Notes

### Code Changes Summary
- Added position/fighting checks at start of `ProcessPlayerVsPlayerAttack`
- Added position/fighting checks at start of `ProcessPlayerVsMobAttack`
- These guards prevent dead/disconnected players from attacking

### Files Modified
- `src/EliteMud.Server/GameTickService.cs` (lines 261-278, 347-357)
