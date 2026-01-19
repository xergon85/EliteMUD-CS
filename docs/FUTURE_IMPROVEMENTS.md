# Future Improvements

This document tracks potential improvements to the EliteMUD-CS codebase that would deviate from legacy behavior but could make the system cleaner or more maintainable.

## Data Normalization

### Normalize Equipment Slots During Import

**Current State:**
- Light objects have Type="Light" and require special handling in `HoldHandler`
- The `hold` command checks object Type at runtime to determine slot
- This matches legacy behavior exactly (see `act.obj2.c:do_grab`)

**Proposed Improvement:**
- During import, add a synthetic "Light" wear slot to Light objects with Take flag
- This would eliminate runtime Type checking
- Command handlers would just check WearSlots, no special cases

**Trade-offs:**
- **Pro:** Cleaner runtime code, no special cases
- **Pro:** More discoverable (wear slots visible in object data)
- **Con:** Data doesn't match source .obj files exactly
- **Con:** Import layer becomes more complex
- **Con:** Harder to verify correctness against legacy

**Decision:** Keep legacy behavior for now. This normalization could be added later as part of a broader "modern content format" effort.

**References:**
- Legacy source: `~/Dev/EliteMUD/src/act.obj2.c` lines 914-916
- Current implementation: `src/EliteMud.Application/Commands/Hold/HoldHandler.cs`

---

## Score Command - Missing Player Fields

**Current State:**
- Score command output matches legacy format from `act.informative.c:1057-1250`
- Many fields shown in legacy score are not yet implemented in PlayerState
- These fields are stubbed out with TODO comments and placeholder values

**Missing PlayerState Fields:**

1. **Birth/Age:**
   - Legacy: Shows "You are a X year old {race}"
   - Current: Hardcoded to age 17
   - Needs: `DateTimeOffset Birth` field + age calculation helper

2. **Time Played:**
   - Legacy: Shows "You have been playing for X days and X hours"
   - Current: Hardcoded to 0 days, 0 hours
   - Needs: `TimeSpan TimePlayed` + `DateTimeOffset LastLogon` fields
   - Should accumulate during session and persist

3. **Deity/Worship:**
   - Legacy: Shows "You are the devout worshipper of {deity}"
   - Current: Not shown
   - Needs: `string? Deity` field (null = no worship)

4. **Position:**
   - Legacy: Shows standing/sitting/resting/fighting/sleeping/etc
   - Current: Hardcoded to "standing"
   - Needs: `Position` enum with values: Dead, MortallyWounded, Incapacitated, Stunned, Sleeping, Resting, Sitting, Fighting, Standing, Mounted

5. **Conditions:**
   - Legacy: Shows drunk/hungry/thirsty status
   - Current: Not shown
   - Needs: `byte Drunk`, `byte Hunger`, `byte Thirst` fields (0-24 range)

6. **Spell Effects/Affects:**
   - Legacy: Shows invisible, sanctuary, poisoned, detect align, etc
   - Current: Not shown
   - Needs: `List<Affect>` or bitflag system for active spell effects
   - Each affect has: type, duration, modifier, etc

7. **Player Flags:**
   - Legacy: Shows PKOK flag (player killer)
   - Current: Not shown
   - Needs: Player flags enum/bitfield (PLR_PKOK, PLR_ARENA, etc)

8. **Carrying Weight:**
   - Legacy: Shows "carrying X items with total weight of Y pounds"
   - Current: Shows item count but weight is 0
   - Needs: Access to WorldState to look up object weights
   - Calculation: sum of all inventory object weights

9. **Quest Points:**
   - Legacy: Shows quest points if > 0
   - Current: Not shown
   - Needs: `int QuestPoints` field

**Implementation Priority:**
1. **Phase 3.3 (Combat):** Position, HP/death states
2. **Phase 3.4 (Magic):** Spell affects system
3. **Phase 4 (Persistence):** Birth, time played, quest points
4. **Phase 5 (Advanced):** Conditions (hunger/thirst/drunk), deity system

**References:**
- Legacy source: `~/Dev/EliteMUD/src/act.informative.c` lines 1057-1250
- Legacy structs: `~/Dev/EliteMUD/src/structs.h`
- Current implementation: `src/EliteMud.Application/Commands/Score/ScoreHandler.cs`

---

## Other Ideas

(Add future improvement ideas here)

