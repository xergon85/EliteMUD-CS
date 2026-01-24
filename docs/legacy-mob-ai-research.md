# Legacy Mob AI Research - EliteMUD

**Research Date:** January 24, 2026  
**Source:** `/Users/christofferisenberg/Dev/EliteMUD/src/mobact.c` and `fight.c`  
**Purpose:** Document legacy mob AI behavior for accurate C# reimplementation

---

## Overview

The legacy EliteMUD mob AI runs in `mobile_activity()` which is called periodically (likely every tick). Each mob in the world is processed in order through a loop that checks various flags and executes behaviors.

**Main Loop:** `mobact.c:118-272`

---

## 1. Aggro System

### MOB_AGGRESSIVE Flag
**Source:** `mobact.c:218-242`

**Behavior:**
- Only triggers when mob is AWAKE and NOT FIGHTING
- Scans all characters in the same room
- Attacks first visible non-NPC player found (unless player has NOHASSLE flag)
- Respects MOB_WIMPY flag (won't attack awake targets if wimpy)

**Alignment-Based Aggro:**
The aggressive system supports alignment-specific triggers:
- `MOB_AGGRESSIVE_EVIL` - Only attacks evil players (IS_EVIL)
- `MOB_AGGRESSIVE_GOOD` - Only attacks good players (IS_GOOD)  
- `MOB_AGGRESSIVE_NEUTRAL` - Only attacks neutral players (IS_NEUTRAL)
- If none of these flags are set, attacks ANY player

**Logic Flow:**
```c
if (IS_SET(ch->specials2.act, MOB_AGGRESSIVE)) {
  found = FALSE;
  for (tmp_ch = world[ch->in_room]->people; tmp_ch && !found; tmp_ch = tmp_ch->next_in_room) {
    if (!IS_NPC(tmp_ch) && CAN_SEE(ch, tmp_ch) && !PRF_FLAGGED(tmp_ch, PRF_NOHASSLE)) {
      if (!IS_SET(ch->specials2.act, MOB_WIMPY) || !AWAKE(tmp_ch)) {
        // Check alignment flags or default to attacking anyone
        if ((IS_SET(ch->specials2.act, MOB_AGGRESSIVE_EVIL) && IS_EVIL(tmp_ch)) || 
            (IS_SET(ch->specials2.act, MOB_AGGRESSIVE_GOOD) && IS_GOOD(tmp_ch)) || 
            (IS_SET(ch->specials2.act, MOB_AGGRESSIVE_NEUTRAL) && IS_NEUTRAL(tmp_ch)) || 
            (!IS_SET(ch->specials2.act, MOB_AGGRESSIVE_EVIL) && 
             !IS_SET(ch->specials2.act, MOB_AGGRESSIVE_NEUTRAL) && 
             !IS_SET(ch->specials2.act, MOB_AGGRESSIVE_GOOD))) {
          hit(ch, tmp_ch, 0);
          found = TRUE;
        }
      }
    }
  }
}
```

**Key Points:**
- Attacks first valid target found (not random)
- Stops searching after finding one target (`found = TRUE`)
- Requires visibility (`CAN_SEE(ch, tmp_ch)`)
- Only attacks players, never NPCs
- Won't attack immortals with NOHASSLE preference

---

## 2. Wandering/Roaming Behavior

### Random Movement
**Source:** `mobact.c:177-194`

**Conditions Required:**
1. Mob does NOT have `MOB_SENTINEL` flag
2. Mob position is `POS_STANDING`
3. Mob is NOT currently fighting
4. Mob is NOT tracking (trackdir is NULL)

**Algorithm:**
```c
door = number(0, 45);  // Random number 0-45
if (door < NUM_OF_DIRS && CAN_GO(ch, door) && 
    !IS_SET(world[EXIT(ch, door)->to_room]->room_flags, NO_MOB) && 
    !IS_SET(world[EXIT(ch, door)->to_room]->room_flags, DEATH)) {
  
  // Anti-bounce logic: prevent immediate backtrack
  if (ch->mob_specials.last_direction == door) {
    ch->mob_specials.last_direction = -1;  // Reset, don't move
  } else {
    // Check zone restriction
    if (!IS_SET(ch->specials2.act, MOB_STAY_ZONE)) {
      ch->mob_specials.last_direction = door;
      do_move(ch, "", ++door, 0);
    } else {
      // Only move if target room is in same zone
      if (world[EXIT(ch, door)->to_room]->zone == world[ch->in_room]->zone) {
        ch->mob_specials.last_direction = door;
        do_move(ch, "", ++door, 0);
      }
    }
  }
}
```

**Key Mechanics:**
- **Random chance:** ~13% chance to move each tick (6 directions out of 46 random values)
- **Anti-bounce:** Tracks `last_direction` to prevent ping-ponging between two rooms
- **Zone restriction:** `MOB_STAY_ZONE` flag keeps mob in its home zone
- **Room restrictions:** Won't enter NO_MOB or DEATH rooms
- **Safety check:** Only moves if exit exists (`CAN_GO`)

### Sentinel Return Home
**Source:** `mobact.c:195-199`

If a mob has `MOB_SENTINEL` flag but is NOT in its hometown:
```c
if (IS_SET(ch->specials2.act, MOB_SENTINEL) &&
    ch->player.hometown != IN_ROOM(ch) &&
    !FIGHTING(ch)) {
  perform_track(ch, ch->player.hometown, 100);
}
```

**Behavior:**
- Sentinel mobs track back to their `hometown` room if displaced
- Only happens when not fighting
- Uses pathfinding (`perform_track`) with max distance 100

---

## 3. Memory System

### MOB_MEMORY Flag
**Source:** `mobact.c:244-270`

Mobs with `MOB_MEMORY` flag remember players who attack them and seek revenge.

**When Memory is Added:**
**Source:** `fight.c:824-827`
```c
if (IS_NPC(victim) && IS_SET(victim->specials2.act, MOB_MEMORY) &&
    !IS_NPC(ch) && (GET_LEVEL(ch) < LEVEL_DEITY))
  remember(victim, ch);
```

Memory is added when:
- A player attacks a mob (victim is NPC with MOB_MEMORY)
- Attacker is a player (not another mob)
- Attacker is not a deity

**Memory Check (Every Tick):**
```c
if (IS_SET(ch->specials2.act, MOB_MEMORY) && ch->mob_specials.memory) {
  // Loop through all characters in the game
  for (vict = 0, tmp_ch = character_list, max = 1; 
       tmp_ch && !IS_NPC(tmp_ch);
       tmp_ch = tmp_ch->next, max++)
  {
    if (CAN_SEE(ch, tmp_ch)) {
      // Check if this player is in memory
      for (names = ch->mob_specials.memory; names; names = names->next)
        if (names->id == GET_IDNUM(tmp_ch))
          vict = tmp_ch;
      
      // If remembered player is in same room, attack them
      if (vict && IN_ROOM(vict) == IN_ROOM(ch)) {
        if (!IS_SET(world[ch->in_room]->room_flags, LAWFULL)) {
          act("'Hey!  You're the fiend that attacked me!!!', exclaims $n.", 
              FALSE, ch, 0, 0, TO_ROOM);
          hit(ch, vict, 0);
          return;
        } else {
          // In LAWFULL rooms, just follow instead of attacking
          do_follow(ch, GET_NAME(vict), 0, 0);
        }
      }
    }
  }
  
  // If remembered player is in different room, track them
  if (vict && IN_ROOM(vict) != IN_ROOM(ch) &&
      !IS_SET(ch->specials2.act, MOB_SENTINEL) &&
      GET_HIT(ch) + GET_LEVEL(ch) > GET_MAX_HIT(ch)) {
    annoy_hunted_victim(ch, vict, max);
    perform_track(ch, IN_ROOM(vict), GET_LEVEL(ch));
  }
}
```

**Behavior:**
1. **In Same Room:**
   - If in non-LAWFULL room: Attack immediately with revenge message
   - If in LAWFULL room: Follow the player instead (can't attack in safe zones)

2. **In Different Room:**
   - Only tracks if mob is NOT sentinel
   - Only tracks if mob is relatively healthy: `GET_HIT(ch) + GET_LEVEL(ch) > GET_MAX_HIT(ch)`
   - Uses pathfinding to hunt down the player
   - Randomly taunts the player via `annoy_hunted_victim()`

**Memory Management Functions:**
**Source:** `mobact.c:280-340`

```c
// Add player to memory (stores player ID)
void remember(struct char_data *ch, struct char_data *victim) {
  // Only mobs remember, only players are remembered
  if (!IS_NPC(ch) || IS_NPC(victim)) return;
  
  // Check if already remembered
  for (tmp = ch->mob_specials.memory; tmp && !present; tmp = tmp->next)
    if (tmp->id == GET_IDNUM(victim))
      present = TRUE;
  
  // Add to linked list if new
  if (!present) {
    CREATE(tmp, memory_rec, 1);
    tmp->next = ch->mob_specials.memory;
    tmp->id = GET_IDNUM(victim);
    ch->mob_specials.memory = tmp;
  }
}

// Remove player from memory
void forget(struct char_data *ch, struct char_data *victim) {
  // Find and remove from linked list
}

// Clear all memory
void clearMemory(struct char_data *ch) {
  // Free entire linked list
}
```

**When Memory is Cleared:**
**Source:** `fight.c:1066`
```c
forget(ch, victim);  // Called when victim dies
```

**Taunt Messages:**
**Source:** `mobact.c:343-380`

When hunting a remembered player, mob randomly yells taunts:
- "Where are you, little {name}?"
- "Come out from hiding, {name}, you little weasel."
- "You cannot run forever, {name}."
- "Come out here and play, {name}. You'll enjoy it."
- "Does it feel good to be hunted down like a pig, {name}?"
- "I am coming for you, {name}."

---

## 4. Assist System (MOB_HELPER)

### Helper Flag Behavior
**Source:** `fight.c:1575-1600`

When a mob enters combat, it triggers assistance from nearby mobs with `MOB_HELPER` flag.

**Trigger:** When `IS_NPC(ch)` enters combat (inside `hit()` function after combat starts)

**Logic:**
```c
if (IS_NPC(ch)) { /* MOB_HELPER */
  for (k = world[ch->in_room]->people; k; k = k->next_in_room) {
    if (!FIGHTING(ch)) return; /* if target disappears */
    
    if (!IS_NPC(k)) continue;  /* check only mobs in the room */
    
    if ((k != ch) &&                      /* not the same mob */
        MOB_FLAGGED(k, MOB_HELPER) &&     /* has Helper Flag */
        !FIGHTING(k) &&                   /* not already fighting */
        (GET_MOB_WAIT(k) < 1))            /* no wait state */
    {
      // If helper has a master (charmed/following), assist the master
      if (k->master) {
        if (FIGHTING(k->master) &&
            (IN_ROOM(k) == IN_ROOM(FIGHTING(k->master))))
          hit(k, FIGHTING(k->master), TYPE_UNDEFINED);
      } 
      // If no master, assist mobs with similar alignment (within 750 points)
      else {
        if (abs(GET_ALIGNMENT(ch) - GET_ALIGNMENT(k)) <= 750 &&
            (IN_ROOM(k) == IN_ROOM(FIGHTING(ch))))
          hit(k, FIGHTING(ch), TYPE_UNDEFINED);
      }
    }
  }
}
```

**Conditions for Assistance:**
1. Helper must be a mob in same room
2. Helper must have `MOB_HELPER` flag
3. Helper must NOT be fighting already
4. Helper must NOT be in WAIT_STATE (mob cooldown)
5. Helper is not the mob that started combat

**Who They Assist:**
- **If charmed/following:** Assist their master by attacking master's opponent
- **If independent:** Assist mobs with similar alignment (within 750 alignment points)

**Key Points:**
- Alignment check: `abs(GET_ALIGNMENT(ch) - GET_ALIGNMENT(k)) <= 750`
  - Good mobs (350-1000) help other good mobs
  - Evil mobs (-1000 to -350) help other evil mobs
  - Neutral mobs help both good and evil within range
- Charmed mobs prioritize their master's fights over alignment
- Only assists in same room (no cross-room assistance)

---

## 5. Scavenger Behavior

### MOB_SCAVENGER Flag
**Source:** `mobact.c:145-163`

**Behavior:**
- Only when mob is AWAKE and NOT FIGHTING
- 1 in 11 chance per tick (~9% chance)
- Picks up most valuable object in room that mob can carry
- Uses `MOB_CAN_GET_OBJ(ch, obj)` macro for validation

```c
if (IS_SET(ch->specials2.act, MOB_SCAVENGER)) {
  if (world[ch->in_room]->contents && !number(0, 10)) {
    for (max = 1, best_obj = 0, obj = world[ch->in_room]->contents; 
         obj; obj = obj->next_content) {
      if (MOB_CAN_GET_OBJ(ch, obj)) {
        if (obj->obj_flags.cost > max) {
          best_obj = obj;
          max = obj->obj_flags.cost;
        }
      }
    }
    
    if (best_obj) {
      obj_from_room(best_obj);
      obj_to_char(best_obj, ch);
      act("$n gets $p.", FALSE, ch, best_obj, 0, TO_ROOM);
    }
  }
}
```

---

## 6. Additional Behaviors

### Tracking System
**Source:** `mobact.c:166-176`

Mobs can follow a `trackdir` stack (pathfinding):
```c
if (ch->trackdir && !FIGHTING(ch)) {
  if (!AWAKE(ch)) do_wake(ch, "", 0, 0);
  if (GET_POS(ch) < POS_STANDING) do_stand(ch, "", 0, 0);
  
  if ((ch->trackdir->room == IN_ROOM(ch)) && 
      !IS_SET(world[EXIT(ch, (int)(ch->trackdir->dir))->to_room]->room_flags, NO_MOB)) {
    do_move(ch, "", stack_pop(&ch->trackdir) + 1, 0);
  } else {
    free_stack(ch->trackdir);
    ch->trackdir = NULL;
  }
}
```

Used by:
- Sentinel mobs returning home
- Mobs with memory hunting players

### Mob Action Strings
**Source:** `mobact.c:123-126`

Mobs can have scripted action strings (`mobaction` field):
```c
if (ch->mobaction) {
  get_next_mob_command(ch, cmd);
  exec_mob_command(ch, cmd, 0);
}
```

Supports:
- Semicolon-delimited command sequences
- Percent-chance commands: `%50 say hello` (50% chance)
- Special commands starting with `#`

### Special Procedures
**Source:** `mobact.c:129-139`

Mobs with `MOB_SPEC` flag call special C functions:
```c
if (IS_SET(ch->specials2.act, MOB_SPEC) && !no_specials) {
  if (!mob_index[ch->nr].func) {
    log("Mob has MOB_SPEC but no function");
    REMOVE_BIT(ch->specials2.act, MOB_SPEC);
  } else {
    if ((*mob_index[ch->nr].func)(ch, ch, NULL, 0, ""))
      continue;  // Skip rest of AI if special proc handled
  }
}
```

---

## 7. MOB Flags Summary

| Flag | Hex/Bit | Behavior |
|------|---------|----------|
| `MOB_SENTINEL` | 1 << 1 | Mob stays in place, returns to hometown if moved |
| `MOB_SCAVENGER` | 1 << 2 | Picks up valuable objects from room |
| `MOB_AGGRESSIVE` | 1 << 5 | Attacks players on sight |
| `MOB_STAY_ZONE` | 1 << 6 | Wandering limited to home zone |
| `MOB_WIMPY` | 1 << 7 | Flees when injured; won't attack awake targets if aggressive |
| `MOB_AGGRESSIVE_EVIL` | 1 << 8 | Only attacks evil players (requires MOB_AGGRESSIVE) |
| `MOB_AGGRESSIVE_GOOD` | 1 << 9 | Only attacks good players (requires MOB_AGGRESSIVE) |
| `MOB_AGGRESSIVE_NEUTRAL` | 1 << 10 | Only attacks neutral players (requires MOB_AGGRESSIVE) |
| `MOB_MEMORY` | 1 << 11 | Remembers attackers and hunts them down |
| `MOB_HELPER` | 1 << 12 | Assists other mobs in combat (alignment or master-based) |

---

## 8. Processing Order (mobile_activity)

**Order of operations per tick:**

1. **Mob Action Strings** (scripted commands)
2. **Special Procedures** (MOB_SPEC)
3. **Mob Programs** (mprog_random_trigger)
4. **If AWAKE and NOT FIGHTING:**
   - a. **Scavenger** behavior
   - b. **Tracking** (follow trackdir stack)
   - c. **Random Movement** (if not sentinel)
   - d. **Sentinel Return Home** (if displaced)
   - e. **Mob Programs** (ACT_PROG triggers)
   - f. **Aggressive** behavior
   - g. **Memory** behavior (revenge/hunting)

**Key Insight:** Aggressive and Memory checks happen AFTER movement, so a mob can wander into a room and immediately attack.

---

## 9. Integration with Combat System

### Memory Addition
- When player attacks mob with MOB_MEMORY, `remember(victim, ch)` is called in `hit()`
- Memory persists until player dies or memory is cleared

### Assist Triggering
- When mob enters combat, it scans room for MOB_HELPER mobs
- Helpers immediately join combat if conditions met
- Creates chain reaction: helpers can trigger more helpers

### Mob Target Switching
**Source:** `fight.c:830-834`

Mobs can switch targets mid-combat:
```c
if (IS_NPC(victim) && ch != victim->specials.fighting) {
  i = GET_LEVEL(ch) - GET_LEVEL(victim->specials.fighting);
  if (!number(0, 20 - MAX(10, MIN(15, i))))
    victim->specials.fighting = ch;
}
```

Level difference affects chance of switch:
- Higher level attacker = more likely to pull aggro
- Adds tactical unpredictability to mob combat

---

## Implementation Notes for C#

### Data Structures Needed

1. **MobInstance additions:**
   ```csharp
   public int LastDirection { get; set; } = -1;  // Anti-bounce tracking
   public List<long> Memory { get; set; } = new();  // Player IDs remembered
   public int Hometown { get; set; }  // Sentinel return location
   public Queue<int>? TrackingPath { get; set; }  // Pathfinding stack
   ```

2. **MobDefinition additions:**
   ```csharp
   public MobFlags Flags { get; set; }
   
   [Flags]
   public enum MobFlags {
       Sentinel = 1 << 1,
       Scavenger = 1 << 2,
       Aggressive = 1 << 5,
       StayZone = 1 << 6,
       Wimpy = 1 << 7,
       AggressiveEvil = 1 << 8,
       AggressiveGood = 1 << 9,
       AggressiveNeutral = 1 << 10,
       Memory = 1 << 11,
       Helper = 1 << 12
   }
   ```

### Service Design

Create `MobAiService` that runs on game tick:
```csharp
public class MobAiService {
    public void ProcessMobAi(MobInstance mob, IWorldState world) {
        // 1. Check if awake and not fighting
        // 2. Process scavenger
        // 3. Process tracking
        // 4. Process wandering
        // 5. Process aggressive
        // 6. Process memory
    }
    
    public void ProcessMobAssist(MobInstance aggressor, IWorldState world) {
        // Called when mob enters combat
        // Scan room for helpers
    }
}
```

### Timing
- Run in `GameTickService` alongside combat tick (PULSE_VIOLENCE = 2 seconds)
- Or create separate `PULSE_MOBILE` for more/less frequent updates

### Testing Strategy
1. Unit tests for each behavior in isolation
2. Integration tests for behavior combinations
3. Test flag precedence (e.g., Sentinel + Memory)
4. Test edge cases (empty rooms, dead targets, etc.)

---

## Conclusion

The legacy mob AI is surprisingly sophisticated with multiple interacting systems:
- **Aggro** makes mobs dangerous to low-level players
- **Memory** creates persistent revenge mechanics
- **Helper** enables mob cooperation and ambushes
- **Wandering** makes world feel alive
- **Scavenger** prevents item clutter and creates "cleaner" mobs

All behaviors respect game state (position, fighting, visibility) and work together to create emergent gameplay.
