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

## Authentication & Security Improvements

### Password Recovery System

**Current State:**
- Account system with BCrypt password hashing implemented
- IP-based rate limiting (3 attempts → 15 minute ban)
- Per-session password attempt limit (3 attempts → disconnect)
- No password recovery mechanism

**Missing Features:**
1. **Email-based Password Reset:**
   - Needs: `string? Email` field in Account entity
   - Email verification on account creation
   - Secure token generation for reset links
   - Email service integration (SMTP or third-party service)
   - Reset token expiration (e.g., 1 hour)

2. **Security Questions:**
   - Alternative to email for MUD context
   - Needs: `List<SecurityQuestion>` table (AccountId, Question, AnswerHash)
   - Store hashed answers (not plaintext)
   - Allow recovery via answering questions

3. **Admin Password Reset:**
   - Admin command to reset user passwords
   - Generates temporary password
   - Forces password change on next login
   - Audit log of admin password resets

4. **Account Lockout:**
   - Currently only IP bans exist
   - Consider: Lock account after X failed attempts (separate from IP ban)
   - Unlock via password recovery or admin intervention

**Implementation Considerations:**
- MUDs typically don't require email, may not fit classic MUD feel
- Security questions may be more appropriate for this genre
- Could implement both and let users choose
- Need to decide: required vs optional email on account creation

**Priority:** Medium - Current system works but users locked out have no recovery path

**References:**
- Current authentication: `src/EliteMud.Application/Session/Authentication/AuthenticationHandler.cs`
- Account entity: `src/EliteMud.Data/Entities/Account.cs`
- IP ban service: `src/EliteMud.Application/Session/IpBanService.cs`

---

## Room Messaging System (act() Function)

**Current State:**
- Commands send messages only to the acting player
- No broadcast messaging to other players in the room
- No target-specific messaging
- Messages are simple strings without substitution

**Legacy System:**
The legacy codebase uses the `act()` function (in `comm.c:1737`) for rich, context-aware messaging:

**Message Types (via type parameter):**
- `TO_CHAR` - Send to the actor only
- `TO_VICT` - Send to the target/victim only
- `TO_ROOM` - Send to everyone in the room (including actor and victim)
- `TO_NOTVICT` - Send to everyone except actor and victim

**String Substitution Codes:**
The `perform_act()` function (in `comm.c:1625`) handles special `$` codes for dynamic substitution:

**Character references:**
- `$n` - Actor's name (seen by others: "Bob", seen by self: "you")
- `$N` - Victim's name
- `$e` - he/she/it (actor)
- `$E` - he/she/it (victim)
- `$m` - him/her/it (actor)
- `$M` - him/her/it (victim)
- `$s` - his/her/its (actor)
- `$S` - his/her/its (victim)

**Object references:**
- `$o` - Object's name
- `$O` - Victim object's name
- `$p` - Object's short description
- `$P` - Victim object's short description
- `$a` - a/an for object
- `$A` - a/an for victim object

**Other:**
- `$t` / `$T` - Text string passed as parameter
- `$-` - Ignore all codes until `$+`
- `$+` - Resume processing codes

**Example Usage (from act.obj1.c:371-372):**
```c
// Getting an object from the room
act("You get$T.", FALSE, ch, 0, (void*) buffer, TO_CHAR);
act("$n gets$T.", TRUE, ch, 0, (void*) buffer, TO_ROOM);
```

**What player sees:** "You get a steel longsword."  
**What room sees:** "Bob gets a steel longsword."

**Example with target (act.obj1.c:385-387):**
```c
// Giving an object to another player
act("You give$t to $N.", FALSE, ch, (void*) buffer, vict, TO_CHAR);
act("$n gives$t to you.", FALSE, ch, (void*) buffer, vict, TO_VICT);
act("$n gives$t to $N.", TRUE, ch, (void*) buffer, vict, TO_NOTVICT);
```

**What giver sees:** "You give a steel longsword to Alice."  
**What receiver sees:** "Bob gives a steel longsword to you."  
**What room sees:** "Bob gives a steel longsword to Alice."

**Implementation Plan:**
1. Create `ActMessage` service in Application layer
2. Implement substitution code parser (`$n`, `$N`, `$o`, etc.)
3. Add pronoun resolution (he/she/it based on sex)
4. Implement visibility checks (can target see actor?)
5. Add broadcast methods:
   - `SendToChar()` - TO_CHAR
   - `SendToVict()` - TO_VICT
   - `SendToRoom()` - TO_ROOM
   - `SendToNotVict()` - TO_NOTVICT
6. Update all command handlers to use act() instead of direct SendLineAsync()

**Priority:** High - Required for Phase 3.1 (Object Interaction)  
Every get/drop/wear/remove command needs proper room messaging

**References:**
- Legacy `act()`: `~/Dev/EliteMUD/src/comm.c` lines 1737-1782
- Legacy `perform_act()`: `~/Dev/EliteMUD/src/comm.c` lines 1625-1734
- Usage examples: `~/Dev/EliteMUD/src/act.obj1.c` lines 350-420

---

## Other Ideas

(Add future improvement ideas here)

