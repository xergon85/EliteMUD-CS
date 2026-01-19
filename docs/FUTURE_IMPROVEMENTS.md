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

## Other Ideas

(Add future improvement ideas here)

