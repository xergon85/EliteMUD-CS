# EliteMUD C# Rewrite Roadmap

## Goals
- Preserve gameplay parity with the legacy C codebase.
- Target .NET 10 on Linux with classic Telnet compatibility.
- Introduce Lua scripting for extensibility and safe iteration.
- Store world content (rooms, mobs, objects, zones, scripts) as versioned files.
- Use SQLite for runtime/player data (players, mail, logs).

## Current Status (As of Jan 19, 2026)

## Known Issues / Bugs

### ✅ FIXED: Container Contents Not Persisting to Database (Jan 25, 2026)
- **Impact:** Players lost all items placed inside containers (bags, chests) when logging out
- **Severity:** HIGH - Major gameplay blocker for inventory management
- **Root Cause:** Database schema only stored `ObjectDefinitionId` and `Quantity` for inventory items
  - No tracking of containment relationships (which items are inside which containers)
  - No tracking of object state (IsClosed, IsLocked)
- **Solution Implemented:**
  - Added `ContainerId` (nullable int) to `CharacterInventoryItem` - tracks which container holds the item (null = top-level inventory)
  - Added `ObjectState` (JSON string) to store IsClosed, IsLocked runtime state
  - Added `SequenceOrder` (int) to preserve item order when loading
  - Self-referencing relationship: items can point to their container via ContainerId
  - Updated CharacterMapper save/load logic with recursive container handling
- **Files Modified:**
  - `CharacterInventoryItem.cs` - Added ContainerId, ObjectState, SequenceOrder fields
  - `EliteMudDbContext.cs` - Configured self-referencing relationship
  - `CharacterMapper.cs` - Recursive save/load for container hierarchy
  - Migration: `20260125105420_AddContainerPersistence.cs`
- **Features Working:**
  - Container contents persist across logout/login
  - Nested containers supported (bag in bag with items)
  - Container state persists (closed/locked status)
  - Item order preserved (newest first)
- **Next:** Needs in-game testing to verify edge cases
- **Estimated Effort:** COMPLETE (1 session - schema, migration, save/load, build verification)

### ✅ FIXED: Equipped Container Contents Not Persisting (Jan 25, 2026)
- **Impact:** Players lost all items stored in equipped containers (e.g., girdles, bags worn as equipment) when logging out
- **Severity:** CRITICAL - Data loss bug affecting equipped container items
- **Root Cause:** Equipment persistence only stored `ObjectDefinitionId` (blueprint ID)
  - When loading, created fresh empty instance from definition
  - Container contents and state (IsClosed, IsLocked) were not persisted
- **Solution Implemented:**
  - Added `ItemData` (nullable JSON string) column to `CharacterEquipmentItem` entity
  - Save logic serializes full item tree using `InventoryItemDto` format (same as inventory)
  - Load logic deserializes from JSON with fallback to legacy behavior (backwards compatible)
  - Reuses existing `SaveInventoryItemRecursive()` and `LoadInventoryItemRecursive()` helpers
- **Files Modified:**
  - `CharacterEquipmentItem.cs` - Added ItemData column with XML documentation
  - `CharacterMapper.cs` - Updated save/load logic (lines ~230-265, ~409-431)
  - Migration: `20260125120339_AddEquipmentItemData.cs`
  - Test: `CharacterMapperPersistenceTests.cs` - Added comprehensive equipped container test
- **Features Working:**
  - Equipped containers persist contents across logout/login
  - Container state persists (IsClosed, IsLocked)
  - Nested containers inside equipped containers supported
  - Backwards compatible with old saves (null ItemData → legacy behavior)
- **Commit:** `b3b998b` - "Fix: Persist equipped container contents across logout/login"
- **Test Coverage:** All 227 tests passing (226 existing + 1 new equipped container persistence test)

### ✅ FIXED Mob AI Position Bug
- ✅ **Mob attacks don't set player to Fighting position** - FIXED (Jan 25, 2026)
  - Impact: Standing players stayed in Position.Standing when attacked by aggressive mobs
  - Root cause: Incorrect comparison `Position < Fighting` (Standing=8 > Fighting=7 in enum)
  - Legacy behavior: Victims should be set to Position.Fighting when combat starts
  - Fix: Changed condition to `Position > Fighting` in MobAiService
    - Now correctly sets Standing/Sitting/Resting players to Fighting when attacked
    - Preserves Fighting position if already fighting
    - Preserves worse positions (Dead/Mortally/Incap/Stunned)
  - Affected: ProcessAggressive() and ProcessMemory() in MobAiService
  - All 174 tests passing (154 existing + 20 new mob AI tests)

### Position/State Bugs
- ✅ **`look` command works while sleeping** - FIXED (Jan 25, 2026)
  - Impact: Players could see room details while asleep
  - Legacy behavior: Look requires Position.Resting or higher (act.informative.c:661-667)
  - Fix: Added position checks in LookCommandHandler
    - Position < Sleeping (Stunned/Incap/Mortally/Dead) → "You can't see anything but stars!"
    - Position == Sleeping → "You can't see anything, you're sleeping!"
    - Position >= Resting → Look works normally
  - Note: Other info commands (examine, inventory, equipment) don't have position restrictions in legacy

- ✅ **Players can get stuck sitting/sleeping while fighting** - FIXED (Jan 25, 2026)
  - Impact: Player ended up in Position.Sitting while FightingConnectionId was set, making them unable to act
  - Root cause: Victim didn't auto-stand when attacked/damaged
  - Legacy behavior: Getting hit forces victim to Position.Standing (fight.c update_pos())
  - Fix: Updated CombatCalculator.UpdatePosition() to auto-stand victims in Sleeping/Resting/Sitting positions
  - Details: Position.Fighting and Position.Standing are preserved, but Sleeping/Resting/Sitting → Standing on damage

### Persistence Bugs
- ✅ **Player position/room persistence** - Working as intended (Jan 25, 2026)
  - Players spawn correctly at their saved location
  - Position and RoomId are properly persisted to database

### ✅ Phase 1: COMPLETE
- ✅ Telnet session handling and input pipeline
- ✅ Basic login (name entry only, no creation flow)
- ✅ Commands: `look`, `say`, `who`, directional movement, `quit`, `zreset`
- ✅ Full legacy world loaded: 7069 rooms, 2545 mobs, 2364 objects, 114 zones
- ✅ Lua scripting: `OnEnterRoom`, `OnLook`, `OnSay` hooks working

### ✅ Phase 2: COMPLETE
- ✅ File-based content schema v1 (rooms, exits, mobs, objects, scripts, zones)
- ✅ Import pipeline: legacy CircleMUD/ROM → JSON zone files
- ✅ Zone-grouped content loading (114 zone files)
- ✅ Runtime loads world content from files
- ✅ Legacy IDs preserved
- ✅ Mob equipment system (20 slots: Light, Head, Body, Wield, etc.)
- ✅ Zone reset system (LoadMob, LoadObject, EquipMob with spawn chances)

### ✅ Phase 3: COMPLETE (Character System)
**Milestone:** Playable persistent characters

#### 3.0 Messaging System - COMPLETE ✅
- ✅ ActMessage service - room broadcast messaging
- ✅ Substitution code parser ($n, $N, $o, $e, $m, $s, etc.)
- ✅ Pronoun resolution helpers (he/she/it based on sex)
- ✅ Multi-audience broadcasting (ToChar, ToVict, ToRoom, ToNotVict)
- ✅ Visibility checks (can target see actor?)
- ✅ Message capitalization and formatting
- ✅ Support for both PlayerState and MobInstance actors/victims

#### 3.1 Player Inventory & Object Interaction - COMPLETE ✅
- ✅ Show objects in room description (look command) - already implemented
- ✅ `examine` command - inspect objects/mobs - already implemented (✅ supports indexed targeting: `examine 2.corpse`)
- ✅ Player inventory system - PlayerState inventory fully wired
- ✅ `get` / `take` commands - pick up objects from room with ActMessage (✅ supports indexed targeting: `get 2.sword`, `get all all.corpse`)
- ✅ `drop` command - drop objects to room with ActMessage (✅ supports indexed targeting: `drop 2.sword`)
- ✅ `inventory` / `i` command - list carried items - already implemented
- ❌ `give` command - transfer objects to other players - deferred (⚠️ **needs indexed targeting**: `give 2.sword guard`, `give sword 2.guard`)
- ❌ Object weight and carry capacity limits - TODO

#### 3.2 Player Equipment System - COMPLETE ✅
- ✅ Extend PlayerState with Equipment dictionary (copy from MobInstance)
- ✅ `equipment` / `eq` command - show worn/wielded items
- ✅ `wear` command - equip wearable items (✅ supports indexed targeting: `wear 2.helmet`)
- ✅ `wear all` command - equip all wearable items in inventory
- ✅ `remove` command - unequip items to inventory (✅ supports indexed targeting: `remove 2.ring`)
- ✅ `wield` command - equip weapons (✅ supports indexed targeting: `wield 2.sword`)
- ✅ `hold` command - hold items/weapons/lights (✅ supports indexed targeting: `hold 2.torch`, `hold 2.sword`)
- ✅ Hold command accepts weapons with Wield flag (legacy: wear_bitvectors[HOLD] = ITEM_HOLD | ITEM_WIELD)
- ✅ Two-handed weapon blocking (can't hold while wielding two-handed weapon)
- ✅ Equipment slot validation (can't wear two body armors, etc.)
- ✅ ActMessage integration for all equipment commands (wear/wield/hold/remove)
- ✅ Proper "already equipped" messages showing currently equipped item ("You're already wielding $p.")
- ❌ Item level/class/race restrictions - prevent equipping items above level or wrong class - **TODO**
- ✅ Item stat bonuses storage - equipment affects stored and displayed in `stat` command - **COMPLETE**
- ✅ Equipment vitals bonuses - MaxHit, MaxMana, MaxMove applied to effective stats - **COMPLETE**
- ✅ Equipment combat bonuses - AC, Hitroll, Damroll applied in combat - **COMPLETE**
- ✅ Equipment saving throw bonuses - SavingPhysical/Mental/Magic/Poison applied - **COMPLETE**
- ✅ Apply equipment attribute bonuses to formulas - STR/DEX/INT/WIS/CON/CHA affect combat calculations - **COMPLETE**
  - CombatCalculator should use effective stats (base + equipment + spell) instead of base stats
  - Strength affects tohit and todam (str_app tables)
  - Dexterity affects AC and dodge chance
  - Intelligence affects spell damage and mana costs
  - Wisdom affects spell success and saving throws
  - Constitution affects HP regeneration rate
- ❌ Item affects - apply buffs/debuffs from equipped items (e.g., DETECT_INVIS, INVISIBLE) - **TODO**
- ❌ Cursed items - can't remove once equipped - **TODO**
- ❌ Item durability - equipment degrades over time - **TODO**
- ❌ Item repair system - repair damaged equipment - **TODO**

#### 3.3 Character Stats & Resources - COMPLETE ✅
- ✅ PlayerState has: HP, MaxHP, Mana, MaxMana, Movement, MaxMovement
- ✅ PlayerState has: Level, Experience, Gold, BankGold
- ✅ PlayerState has: Str, Dex, Con, Int, Wis, Cha
- ✅ PlayerState has: Race, Class, Alignment, Sex
- ✅ `score` command - display full character sheet
- ❌ Race system - define race modifiers, stat bonuses, size, skill bonuses - **TODO (currently just strings)**
- ❌ Class system - define class tables with HP/mana/move gains per level, skill/spell availability - **TODO (currently just strings)**
- ❌ Stat caps by race/class - max attribute values based on race and class - **TODO**
- ❌ Class-specific skill/spell lists - what skills/spells each class can learn - **TODO**
- ❌ Level up system - automatic leveling when XP threshold reached - **TODO**
- ❌ XP table/formula - experience required per level - **TODO**
- ❌ Stat gains on level up - HP/mana/movement increases - **TODO**
- ❌ Level up notifications and messages - **TODO**
- ❌ `stat` command - show detailed character stats (self), immortals can check others - **TODO**
- ✅ HP/mana/movement regeneration tick system - **COMPLETE** (RegenerationService with legacy formulas)
- ❌ Room flags for regen bonuses (REGEN_HP, REGEN_MANA, REGEN_MOVE) - see legacy room flags - **TODO**
- ✅ Position system (standing/sitting/resting/sleeping/fighting) - **COMPLETE**
- ✅ `sleep` command - enter sleep position for faster regen - **COMPLETE**
- ✅ `rest` command - enter rest position for moderate regen - **COMPLETE**
- ✅ `sit` command - enter sitting position - **COMPLETE**
- ✅ `wake` command - wake from sleep - **COMPLETE**
- ✅ `stand` command - stand up from sitting/resting/sleeping - **COMPLETE**
- ✅ Position-based regen rates (sleeping > resting > sitting > standing) - **COMPLETE** (legacy formulas: sleeping +4, resting +3, sitting +2, standing +1)
- ❌ Hunger/thirst system - track fullness and thirst levels - **TODO**
- ❌ `drink` / `eat` commands for consumables - **TODO** (see Phase 6.2 for full implementation)
- ❌ Hunger/thirst messages and warnings - **TODO**
- ❌ Hunger/thirst effects on regen and combat - **TODO**
- ❌ Death from starvation/dehydration - **TODO**
- ❌ Drunk status effects - **TODO**
- ❌ Poison status effects - **TODO**

#### 3.4 Persistence Layer (SQLite) - COMPLETE ✅
- ✅ Database schema with EF Core (Account + Character entities)
- ✅ Account table (username, password hash, last login)
- ✅ Character table (name, stats, vitals, location, resources, metadata)
- ✅ CharacterInventoryItem table (character inventory persistence) - **stores full item tree as JSON**
- ✅ CharacterEquipmentItem table (character equipment persistence) - **stores full item tree as JSON with contents** (Jan 25, 2026)
- ✅ Repository pattern (IAccountRepository, ICharacterRepository)
- ✅ PasswordService with BCrypt hashing
- ✅ CharacterMapper (Entity ↔ PlayerState conversion with IWorldState)
- ✅ Player save on quit (automatic via TelnetServer finally block)
- ✅ Player load on login (loads selected character from DB, recreates object instances)
- ✅ Database auto-migration on startup
- ✅ `save` command - manual character save
- ✅ Auto-save timer (every 5 minutes via GameTickService)
- ❌ Mail table schema - **DEFERRED**
- ❌ Board table schema - **DEFERRED**
- ❌ Logs table schema - **DEFERRED**

#### 3.5 Character Creation Flow - COMPLETE ✅
- ✅ Account creation with password confirmation
- ✅ Multi-character support (up to 10 per account)
- ✅ Character selection menu (list/create/delete/quit)
- ✅ Sex selection menu (Male/Female/Neutral)
- ✅ Race selection menu (13 playable races)
- ✅ Class selection menu (20+ classes with race-based restrictions)
- ✅ Character name validation and confirmation
- ✅ Character deletion with confirmation
- ✅ Starting stats (all 11, matching legacy behavior)
- ❌ Stat rolling / point allocation - **Using fixed stats**
- ❌ Starting equipment by class - **TODO**
- ❌ Starting location by race/class - **Hardcoded to room 1**

#### 3.6 Security & Authentication - COMPLETE ✅
- ✅ BCrypt password hashing
- ✅ Per-session password attempt limit (3 attempts → disconnect)
- ✅ IP-based rate limiting (3 failed attempts → 15 min ban)
- ✅ IpBanService with auto-expiring bans
- ✅ Failed attempt tracking per IP address
- ✅ Ban status checking before connection acceptance
- ❌ Password recovery system - **Documented in FUTURE_IMPROVEMENTS.md**
- ❌ Email registration (optional) - **TODO**
- ❌ Permission/immortal level system - **TODO**

#### 3.7 Idle & Connection Management - PLANNED
- ❌ Idle timeout - disconnect users after X minutes of inactivity - **TODO**
- ❌ `idle` command - show player idle time - **TODO**
- ❌ Link-dead state handling (disconnected but still in-game) - **TODO**
- ❌ Reconnection to link-dead characters - **TODO**
- ❌ Auto-quit on extended link-death - **TODO**

#### 3.8 Site Ban System - PLANNED
- ❌ Site-wide bans by IP or hostname pattern - **TODO** (legacy: ban.c)
- ❌ `ban` command - add site ban (admin) - **TODO**
- ❌ `unban` command - remove site ban (admin) - **TODO**
- ❌ Ban list persistence in database - **TODO**
- ❌ New character creation blocking vs play blocking - **TODO**

### 🔄 Phase 4: IN PROGRESS (Combat + Skills)
**Milestone:** Core combat parity

#### 4.1 Basic Combat System - COMPLETE ✅
- ✅ Add fighting state to PlayerState and MobInstance - DONE
- ✅ `kill` / `hit` command - initiate combat (✅ supports indexed targeting: `kill 2.soldier`)
- ✅ Combat loop / turn system - DONE
- ✅ Damage calculation (dice rolls using weapon stats) - DONE
- ✅ Hitroll / damroll / armor class calculations - DONE
- ✅ `flee` command - escape from combat - DONE
- ✅ Death handling for players - DONE (corpse creation, respawn, XP loss)
- ✅ Death handling for mobs - DONE (corpse creation with equipment transfer)
- ✅ Corpse creation with loot - DONE (player/mob corpses with contents)
- ✅ `wimpy` command - auto-flee at low HP threshold - DONE

#### 4.2 Combat Commands
- ✅ `kick` - unarmed attack (COMPLETE)
- ✅ `bash` - shield bash attack (COMPLETE)
- ✅ `rescue` - take aggro from group member (COMPLETE - PvE only for now)
- ❌ `consider` - estimate mob difficulty
- ❌ `assist` - join combat alongside ally

#### 4.6 Advanced Combat Mechanics - PLANNED
- ❌ Combat rounds counter (track round number) - **TODO**
- ❌ First strike bonus for initiator - **TODO**
- ❌ Sneak attack from hidden state - **TODO**
- ❌ Multi-attack system (dual-wield, extra attacks) - **TODO**
- ❌ Attack types (slash, pierce, bludgeon) vs armor types - **TODO**
- ❌ Critical hits and fumbles - **TODO**
- ❌ Weapon proficiencies - **TODO**
- ❌ Combat stance system (offensive/defensive/berserker) - **TODO**

#### 4.3 Skills & Spells Framework - COMPLETE ✅
**Status:** Data-driven Lua formula system fully implemented and merged to main (Jan 23, 2026)

**Completed Infrastructure:**
- ✅ FormulaEvaluator - Thread-safe Lua formula engine
- ✅ SkillMetadataRegistry - Fast lookups by ID, name, or alias
- ✅ SkillRegistry - Auto-discovery of skills via reflection
- ✅ ISkillHandler interface for active skills
- ✅ IPassiveSkillHandler interface for passive skills
- ✅ All skills use Lua formulas from `content/skills/skills.json`
- ✅ Formula fallback to hardcoded logic if JSON missing
- ✅ Skill proficiency tracking (0-100%)
- ✅ Skill improvement on use
- ✅ Skill persistence (database JSON)
- ✅ WAIT_STATE system (action cooldowns)
- ✅ Skillgain cooldown system (60 seconds between improvements)

**Implemented Skills (7 skills - 6 core + 1 utility):**
- ✅ `kick` - Basic combat skill with formula-driven damage
- ✅ `bash` - Shield bash with knockdown effect
- ✅ `backstab` - Rogue sneak attack with level-based multiplier
- ✅ `rescue` - Tank skill to redirect combat (PvE only)
- ✅ `dodge` - Passive damage reduction
- ✅ `parry` - Passive block with weapon
- ✅ `track` - Pathfinding utility skill (shows direction to target mob)

**Track Skill Implementation (COMPLETE ✅ - Jan 25, 2026):**
- ✅ TrackSkill class with metadata from skills.json
- ✅ TrackSkillExecutor with PathfindingService integration
- ✅ Skill check based on proficiency (random 1-101 vs skill%)
- ✅ Max distance calculation: min(50 + skillPercent/2 + level, 200)
- ✅ Case-insensitive partial name search for mobs
- ✅ Failure modes: "Track whom?", "right here!", "can't find a trail", "too faint to follow", "lose the trail"
- ✅ Success: "You sense a trail [direction]"
- ✅ Skill improvement on successful tracking
- ✅ WAIT_STATE: 1 round (2 seconds)
- ✅ 8 comprehensive unit tests in TrackSkillExecutorTests.cs
- ✅ Auto-registered via ISkillExecutor interface (no manual registration needed)
- ✅ Ranger class has track skill (ID 330, max 95% proficiency, difficulty 8)

**Future Skill Expansion:**
- ❌ `disarm` - Remove opponent's weapon
- ❌ `trip` - Knock opponent down
- ❌ `circle` - Circle around to backstab again
- ❌ `tumble` - Passive dodge improvement
- ❌ `riposte` - Counter-attack on successful parry
- ❌ `berserk` - Warrior rage mode
- ❌ Additional legacy skills from original MUD

**Spell System - COMPLETE ✅**
**Status:** Data-driven spell system fully implemented (Jan 23, 2026)

**Completed Infrastructure:**
- ✅ Spell metadata schema in `content/spells/spells.json`
- ✅ `ISpellHandler` interface
- ✅ `cast` command
- ✅ Mana cost system with formula support
- ✅ Spell success/failure rolls (INT/WIS based)
- ✅ SpellRegistry - Auto-discovery via reflection
- ✅ Spell proficiency tracking (0-100%)
- ✅ Spell persistence (database JSON)
- ✅ WAIT_STATE system for spells
- ✅ Saving throw system (Physical, Mental, Magic, Poison)

**Implemented Spells (8 working spells):**
- ✅ **Damage spells:** Magic Missile, Lightning Bolt, Burning Hands
- ✅ **Healing spells:** Cure Light Wounds, Cure Serious Wounds
- ✅ **Buff spells:** Armor (AC bonus), Bless (hitroll bonus + extra damage vs evil)

**Affect/Buff System - COMPLETE ✅**
- ✅ Active affects tracking (PlayerState.ActiveAffects)
- ✅ Multi-affect support (multiple affects per spell)
- ✅ Timed affects with duration tracking
- ✅ Affect expiration system (GameTickService)
- ✅ `affects` command - display active buffs/debuffs
- ✅ Equipment stat bonuses (STR, DEX, INT, WIS, CON, CHA)
- ✅ Equipment affect bonuses (MaxHit, MaxMana, MaxMove, Armor, Hitroll, Damroll)
- ✅ Equipment saving throw bonuses (SavingPhysical, SavingMental, SavingMagic, SavingPoison)
- ✅ `stat` command - display detailed character stats with equipment bonuses

**Weapon Effects System - COMPLETE ✅**
- ✅ BLESS weapon: +3d6 damage vs evil targets (alignment ≤ -350)
- ✅ EVIL weapon: +3d6 damage vs good targets (alignment ≥ 350)
- ✅ FLAME weapon: +3d6 fire damage (victim saves vs Physical to negate)
- ✅ Integrated into all combat paths (kill, backstab, kick, bash, combat tick)

**Future Spell Expansion:**
- ❌ Debuff spells (Curse, Weaken, Slow, Poison)
- ❌ Utility spells (Detect Magic, Invisibility, Teleport)
- ❌ `practice` command - train skills/spells with guildmaster
- ❌ Area effect spells (Fireball, Earthquake)
- ❌ Summoning spells
- ❌ Charm/control spells

#### 4.4 Alignment System - COMPLETE ✅
**Status:** Legacy-accurate alignment dynamics (Jan 24, 2026)

- ✅ Alignment shift formula from legacy fight.c:445-460
- ✅ Convergent formula: shift = (-victim_alignment - killer_alignment) / 16
- ✅ Integration into all combat paths (5 kill locations)
- ✅ Player feedback messages for significant shifts (|shift| ≥ 50)
- ✅ Alignment display in score command (9 descriptive states)
- ✅ Comprehensive test coverage (17 unit tests)
- ✅ Weapon alignment effects (BLESS/EVIL weapons)

#### 4.5 Combat Events & Hooks
- ❌ Lua hook: OnCombatStart
- ❌ Lua hook: OnDamage
- ❌ Lua hook: OnDeath
- ❌ Lua hook: OnKill
- ❌ Combat logging and messaging

### 🔄 Phase 5: NPCs + AI (IN PROGRESS)
**Milestone:** Mobs behave like legacy

#### 5.1 Mob AI Foundation - COMPLETE ✅
**Status:** Core AI behaviors fully implemented and tested (Jan 25, 2026)

**Completed & Tested:**
- ✅ **Mob tick system** - ProcessMobTick() processes AI every 2 seconds (PULSE_VIOLENCE)
- ✅ **Aggressive behavior** - Mobs attack players on sight
  - ✅ Basic AGGRESSIVE flag (attack anyone)
  - ✅ AGGRESSIVE_EVIL flag (attack evil players, alignment ≤ -350)
  - ✅ AGGRESSIVE_GOOD flag (attack good players, alignment ≥ 350)
  - ✅ AGGRESSIVE_NEUTRAL flag (attack neutral players)
  - ✅ WIMPY flag (won't attack awake players)
  - ✅ Respects fighting state (won't switch targets mid-combat)
- ✅ **Wandering behavior** - Random movement with anti-bounce logic
  - ✅ ~13% chance to move per tick (6/46 dice roll)
  - ✅ Anti-bounce: won't immediately reverse direction
  - ✅ STAY_ZONE flag prevents leaving zone
  - ✅ Respects NO_MOB room flag
  - ✅ Respects DEATH room flag
  - ✅ Only wanders when Position.Standing
- ✅ **Memory system** - Mobs remember and hunt attackers
  - ✅ MEMORY flag enables player tracking
  - ✅ RememberPlayer() / ForgetPlayer() API
  - ✅ Attacks remembered players in same room
  - ✅ Respects LAWFUL room flag (won't attack in lawful rooms)
  - ✅ Tracks victim across rooms with pathfinding
- ✅ **Assist/Helper system** - Mobs help allies in combat
  - ✅ HELPER flag enables ally assistance
  - ✅ Assists mobs with similar alignment (within 750 points)
  - ✅ Won't assist if already fighting
  - ✅ ProcessAssist() called when combat starts
- ✅ **Scavenger behavior** - Picks up valuable items (COMPLETE)
  - ✅ ProcessScavenger() with 9% chance per tick
  - ✅ Picks up most valuable item in room
  - ✅ Stores in mob inventory (MobInstance.InventoryObjectIds)
  - ✅ WorldState.TakeObjectForMob() transfers objects
  - ✅ 5 comprehensive tests
  - ❌ TODO: ActMessage for scavenge action (low priority)
  - ❌ TODO: MOB_CAN_GET_OBJ check for weight limits (future)
- ✅ **Sentinel return home** - Mobs return to spawn location (COMPLETE)
  - ✅ ProcessSentinelReturnHome() uses pathfinding
  - ✅ SENTINEL flag defined and parsed
  - ✅ Hometown tracking in MobInstance
  - ✅ Uses PathfindingService with max distance 100
- ✅ **Tracking/Pathfinding** - Full BFS pathfinding implementation (COMPLETE)
  - ✅ PathfindingService with BFS algorithm
  - ✅ TrackingPath queue in MobInstance
  - ✅ ProcessTracking() consumes path and moves mob
  - ✅ Memory victim tracking across rooms
  - ✅ Respects NO_MOB and DEATH room flags
  - ✅ Zone-aware pathfinding (optional)
  - ✅ Max distance parameter
  - ✅ 17 comprehensive pathfinding tests
- ✅ **Mob inventory system** - COMPLETE
  - ✅ MobInstance.InventoryObjectIds list
  - ✅ AddToInventory() / RemoveFromInventory() methods
  - ✅ Used by scavenger and GiveMob zone reset
- ✅ **Position handling** - Skips AI when not awake or in invalid room
- ✅ **Comprehensive test coverage** - 24 mob AI tests + 17 pathfinding tests + 8 track skill tests

**Test Coverage (49 tests total):**
- **MobAiServiceTests.cs (24 tests):**
  - 8 aggressive behavior tests (basic, evil, good, neutral, wimpy, fighting state)
  - 3 memory/hunting tests (attack remembered, lawful room, unremembered)
  - 4 wandering tests (movement, position check, NO_MOB flag, STAY_ZONE flag)
  - 3 helper/assist tests (basic assist, alignment check, already fighting)
  - 5 scavenger tests (picks valuable, random chance, empty room, stores inventory, transfers from room)
  - 2 position/state tests (not awake, invalid room)
- **PathfindingServiceTests.cs (17 tests):**
  - 5 basic pathfinding tests (linear path, same room, no path, invalid rooms)
  - 2 complex graph tests (multiple paths, cyclic graph)
  - 2 NO_MOB flag tests (respect vs ignore)
  - 1 DEATH flag test (skips death rooms)
  - 2 zone restriction tests (stay in zone, allow crossing)
  - 2 max distance tests (exceeds, within)
  - 3 GetNextDirection tests (correct direction, no path, same room)
- **TrackSkillExecutorTests.cs (8 tests):**
  - No argument error
  - No skill proficiency error
  - Target not found error
  - Target in same room error
  - Successful track shows direction
  - Applies WAIT_STATE correctly
  - Case-insensitive search
  - Partial name matching

**Not Started:**
- ❌ Mob Programs (legacy triggers) - Phase 5.2
- ❌ GiveMob zone reset (requires picking item from inventory)
- ❌ ActMessage integration for scavenger (low priority)

#### 5.2 Mob Programs (Legacy Triggers)
- ❌ Map legacy mob programs to Lua scripts
- ❌ OnGreet trigger - mob reacts to player entry
- ❌ OnGive trigger - mob reacts to receiving item
- ❌ OnSpeech trigger - mob reacts to keywords
- ❌ OnDeath trigger - special death behavior
- ❌ OnFight trigger - special combat behavior
- ❌ OnHitPercent trigger - health-based triggers

#### 5.3 Zone Reset Completion
- ✅ LoadMob - spawn mobs (DONE)
- ✅ LoadObject - spawn objects (DONE)
- ✅ EquipMob - equip items on mobs (DONE)
- ❌ GiveMob - give items to mobs (requires mob inventory)
- ❌ PutObject - put objects in containers (STUBBED)
- ❌ DoorState - set door open/closed/locked
- ❌ RemoveObject - remove objects from world

#### 5.4 Advanced Mob Features
- ❌ Shopkeeper mobs
- ❌ Guildmaster mobs (trainers)
- ❌ Quest-giving mobs
- ❌ Sentinel/guard behavior
- ❌ Scavenger behavior (pick up items)

#### 5.5 Pathfinding & Navigation - PLANNED
- ❌ Shortest path algorithm for mobs - **TODO** (legacy: graph.c)
- ❌ Track system - mobs hunt players across rooms - **TODO**
- ❌ Guard behavior - mobs prevent passage - **TODO**
- ❌ Patrol routes for mobs - **TODO**

### ⏳ Phase 6: World Systems (PLANNED)
**Milestone:** Social and world features

#### 6.1 Container System
- ❌ Container object storage (objects inside containers)
- ❌ `put` command - put item in container (⚠️ **needs indexed targeting**: `put 2.sword bag`, `put sword 2.bag`)
- ❌ `get <item> from <container>` - retrieve from container (✅ already supports indexed targeting)
- ❌ `open` / `close` commands - container state (⚠️ **needs indexed targeting**: `open 2.chest`)
- ❌ `lock` / `unlock` commands - keyed containers (⚠️ **needs indexed targeting**: `unlock 2.chest`)
- ❌ Weight limits for containers
- ❌ Corpse containers (loot corpses) - ✅ already implemented

#### 6.2 Consumable & Utility Items
- ❌ `eat` command - consume food items (⚠️ **needs indexed targeting**: `eat 2.bread`)
- ❌ `drink` command - drink from containers (⚠️ **needs indexed targeting**: `drink 2.waterskin`)
- ❌ `quaff` command - drink potions (⚠️ **needs indexed targeting**: `quaff 2.potion`)
- ❌ `recite` command - use scrolls (⚠️ **needs indexed targeting**: `recite 2.scroll`)
- ❌ `use` command - activate items (⚠️ **needs indexed targeting**: `use 2.wand`)
- ❌ `fill` command - fill containers from fountains (⚠️ **needs indexed targeting**: `fill 2.waterskin fountain`)
- ❌ `pour` command - pour liquid from container (⚠️ **needs indexed targeting**: `pour 2.flask`)
- ❌ `sacrifice` / `junk` command - destroy items for exp/gold (⚠️ **needs indexed targeting**: `sacrifice 2.corpse`)
- ❌ Food/drink consumption effects (hunger/thirst)
- ❌ Potion/scroll/wand effects and charges

#### 6.3 Door System
- ❌ Door state tracking (open/closed/locked)
- ❌ `open` / `close` door commands (⚠️ **needs indexed targeting**: `open 2.door`, `open 2.chest`, `close 2.gate`)
- ❌ `lock` / `unlock` door commands (⚠️ **needs indexed targeting**: `lock 2.chest`, `unlock 2.door`)
- ❌ `pick` command - lockpicking (⚠️ **needs indexed targeting**: `pick 2.lock`)
- ❌ `bash` door command - force doors open (⚠️ **needs indexed targeting**: `bash 2.door`)
- ❌ Hidden doors / secret exits
- ❌ DoorState zone reset implementation

#### 6.4 Shop System
- ❌ Shop definition in zone files
- ❌ `list` command - show shop inventory
- ❌ `buy` command - purchase items (⚠️ **needs indexed targeting**: `buy 2.sword`)
- ❌ `sell` command - sell items to shop (⚠️ **needs indexed targeting**: `sell 2.helmet`)
- ❌ `value` command - appraise item value (⚠️ **needs indexed targeting**: `value 2.ring`)
- ❌ Shop inventory restocking
- ❌ Haggling / charisma price modifiers

#### 6.5 Communication Commands
- ✅ `tell` command - private messaging - COMPLETE (Jan 25, 2026)
- ✅ `reply` command - respond to last tell sender - COMPLETE (Jan 25, 2026)
- ✅ `shout` / `yell` command - zone-wide broadcast (costs 10 movement) - COMPLETE (Jan 25, 2026)
- ✅ `gossip` command - global chat channel - COMPLETE (Jan 25, 2026)
- ✅ `emote` / `me` command - roleplay actions - COMPLETE (Jan 25, 2026)
- ❌ `pose` command - set room presence
- ❌ `auction` command - auction channel
- ❌ Ignore list system
- ❌ Channel history system (tell/gossip/shout history)
- **Commit:** `c443f71` - "Add communication commands: tell, reply, emote, gossip, shout"

#### 6.6 Social Systems
- ❌ `follow` command - follow another player (⚠️ **needs indexed targeting** for mobs: `follow 2.guard`)
- ❌ `group` command - form/manage groups
- ❌ `split` command - divide gold among group
- ❌ Experience sharing in groups
- ❌ Group combat coordination
- ❌ `gtell` command - group chat

#### 6.7 Clan System
- ❌ Clan definition in database
- ❌ Clan membership tracking
- ❌ Clan ranks/hierarchy
- ❌ `ctell` command - clan chat
- ❌ Clan halls / private areas
- ❌ Clan banks / shared storage

#### 6.8 Board & Mail System
- ❌ Bulletin board object type
- ❌ `look board` - see board description
- ❌ `read <number>` - read board message by number
- ❌ `write <subject>` - compose new message
- ❌ `remove <number>` - delete own posts
- ❌ Board message list display
- ❌ Board persistence in SQLite
- ❌ `mail` command - send persistent mail
- ❌ Mail retrieval at post offices
- ❌ Mail storage in SQLite
- ❌ Mail notification on login

#### 6.9 Quest System
- ❌ Quest definition format
- ❌ Quest tracking in PlayerState
- ❌ Quest objectives (kill, fetch, explore)
- ❌ Quest rewards (exp, gold, items)
- ❌ `quest` command - view active quests
- ❌ Quest completion triggers

#### 6.10 Admin Commands
- ❌ `goto` - teleport to room
- ❌ `transfer` - summon player/mob
- ❌ `load` - spawn mob/object
- ❌ `purge` - remove mob/object
- ❌ `set` - modify player/mob stats
- ❌ `advance` - change player level
- ❌ `shutdown` - graceful server shutdown
- ❌ `wizhelp` - list immortal commands
- ❌ Immortal visibility toggle
- ❌ Command logging for auditing

#### 6.11 History System - PLANNED
- ❌ Command history buffer (scroll through previous commands) - **TODO** (legacy: history.c)
- ❌ `history` command - show recent command history - **TODO**
- ❌ Arrow key support for command recall (up/down) - **TODO**
- ❌ History persistence per session - **TODO**

#### 6.12 Ignore System - PLANNED
- ❌ `ignore` command - block messages from specific players - **TODO** (legacy: ignore.c)
- ❌ `unignore` command - remove player from ignore list - **TODO**
- ❌ Ignored player list persistence - **TODO**
- ❌ Apply to: tell, say, shout, gossip, emote - **TODO**

#### 6.13 Social Commands - PLANNED
- ❌ Social command system (smile, laugh, cry, hug, etc.) - **TODO** (legacy: act.social.c)
- ❌ Load socials from data file - **TODO**
- ❌ Self-target vs other-target messaging - **TODO**
- ❌ Room broadcast for socials - **TODO**
- ❌ Custom social editor (OLC) - **TODO**

#### 6.14 Casino/Games System - PLANNED
- ❌ Casino room flags - **TODO** (legacy: casino.c, gen_cards.c)
- ❌ Card game mechanics (poker, blackjack) - **TODO**
- ❌ Dice games - **TODO**
- ❌ Gambling with gold - **TODO**
- ❌ House odds and payout tables - **TODO**

#### 6.15 Special Areas/Quests - PLANNED
- ❌ Castle zone with special mechanics - **TODO** (legacy: castle.c)
- ❌ Quest-specific mob programs - **TODO**
- ❌ Special zone reset logic - **TODO**
- ❌ Legacy special procedures mapping - **TODO**

#### 6.16 Player Commands (Information & Utility) - PLANNED
**High Priority Commands:**
- ❌ `time` - show in-game time and real time - **TODO**
- ❌ `weather` - show current weather in zone - **TODO**
- ❌ `title` - set custom title - **TODO**
- ❌ `prompt` - customize combat/movement prompt - **TODO**
- ❌ `brief` / `compact` / `noshout` - toggle display modes - **TODO**
- ❌ `clear` - clear screen - **TODO**
- ❌ `color` - toggle ANSI color mode - **TODO**
- ❌ `afk` - mark as away from keyboard - **TODO**
- ❌ `reply` - reply to last tell - **TODO**
- ❌ `visible` - break invisibility/hide - **TODO**

**Information Commands:**
- ❌ `areas` / `zones` - list all zones with level ranges - **TODO**
- ❌ `help` - help file system - **TODO**
- ❌ `commands` - list available commands - **TODO**
- ❌ `spells` - list known spells - **TODO**
- ❌ `gen` - show generation stats (total kills, deaths, etc.) - **TODO**
- ❌ `where` - show nearby players in zone - **TODO**
- ❌ `track` - find path to target - **TODO**
- ❌ `scan` - look in all directions - **TODO**

**Advanced Commands:**
- ❌ `alias` - create command aliases - **TODO**
- ❌ `trigger` - create simple triggers - **TODO**
- ❌ `config` - show/set configuration options - **TODO**

### ⏳ Phase 7: Tools + Extensibility (PLANNED)
**Milestone:** Modern content workflow

#### 7.1 Live Scripting
- ❌ Hot reload Lua scripts without restart
- ❌ Script error reporting to immortals
- ❌ In-game script editor for immortals
- ❌ Script debugging commands

#### 7.2 Online Building (OLC)
- ❌ `redit` - edit rooms in-game
- ❌ `medit` - edit mobs in-game
- ❌ `oedit` - edit objects in-game
- ❌ `zedit` - edit zone resets in-game
- ❌ Save changes to zone files
- ❌ Undo/redo for building

#### 7.3 Analytics & Monitoring
- ❌ Player activity logging
- ❌ Combat statistics
- ❌ Economy tracking (gold flow)
- ❌ Performance metrics
- ❌ Crash recovery logs

#### 7.4 Content Export
- ❌ Export to new content format (post-migration)
- ❌ Validation tools for content integrity
- ❌ Content backup/restore utilities

### ⏳ Phase 8: Performance & Optimization (PLANNED)
**Milestone:** Production-ready performance

- ❌ Object pooling for frequently created instances
- ❌ Combat tick optimization (reduce allocations)
- ❌ Room cache system (hot rooms stay in memory)
- ❌ Player index for fast lookups by name
- ❌ Benchmark suite for critical paths
- ❌ Memory profiling and leak detection
- ❌ Connection pooling optimizations
- ❌ Zone reset performance tuning

### ⏳ Phase 9: Testing & Quality (PLANNED)
**Milestone:** Comprehensive test coverage

- ❌ Integration test suite for commands
- ❌ Combat simulation tests
- ❌ Load testing (100+ concurrent players)
- ❌ Zone content validation tests
- ❌ Regression test suite
- ❌ CI/CD pipeline setup
- ❌ Automated smoke tests
- ❌ Performance regression tests

### ⏳ Phase 10: Documentation (PLANNED)
**Milestone:** Complete documentation suite

- ❌ Player guide (basic commands, getting started)
- ❌ Builder guide (zone creation, OLC usage)
- ❌ Immortal guide (admin commands, permissions)
- ❌ Lua scripting guide (hooks, API reference)
- ❌ Content schema documentation (comprehensive)
- ❌ Architecture documentation (layer diagrams)
- ❌ Deployment guide (Linux setup, systemd)
- ❌ Troubleshooting guide


## Legacy Module Mapping (C → C# Targets)
- `comm.c` → ✅ Server networking + session handling (DONE)
- `interpreter.c` → ✅ Command routing (DONE) + ❌ permissions (TODO)
- `db.c` → ✅ World loading (DONE) + ✅ persistence layer (DONE)
- `structs.h` → ✅ Domain model + enums (DONE)
- `fight.c`, `magic.c` → ✅ Combat services (DONE) + 🔄 Spells (IN PROGRESS)
- `mobact.c`, `mobcmd.c` → ❌ AI + scripting hooks (TODO)
- `boards.c`, `mail.c`, `clan.c` → ❌ Feature services (TODO)
- `act.social.c` → ❌ Social commands (TODO - Phase 6.13)
- `casino.c`, `gen_cards.c` → ❌ Casino/games system (TODO - Phase 6.14)
- `castle.c` → ❌ Special areas (TODO - Phase 6.15)
- `history.c` → ❌ Command history (TODO - Phase 6.11)
- `ignore.c` → ❌ Ignore system (TODO - Phase 6.12)
- `graph.c` → ❌ Pathfinding (TODO - Phase 5.5)
- `ban.c` → ❌ Site ban system (TODO - Phase 3.8)

## UI/UX Improvements

### Wait-State Messages
- ❌ **TODO:** Revisit wait-state/action cooldown messaging
  - Current: Shows technical message when trying to act during WAIT_STATE
  - Issue: User finds the message annoying/unclear
  - Proposed: Review legacy messages and improve player feedback
  - File: `src/EliteMud.Application/Skills/` (all skill executors)
  - Related: WAIT_STATE system tracks cooldowns in PlayerState

### Stat Display Cleanup
- ❌ **TODO:** Remove redundant base stat display in parentheses when showing modifiers
  - Current: `Dex: [13 (11+2)]` shows both effective (13) and breakdown (11+2)
  - Issue: The effective value 13 is already shown, the parenthetical breakdown is redundant
  - Proposed: `Dex: [11+2]` or `Dex: [11 Eq:+2]` (show only the breakdown when modifiers exist)
  - Alternative: `Dex: [13]` with separate modifier line or hover info
  - Affects: Str, Int, Wis, Dex, Con, Cha display in stat command
  - File: `src/EliteMud.Application/Commands/Stat/StatHandler.cs` FormatStatWithModifier method

## Implementation Status Summary

### ✅ COMPLETE (Production Ready)
- Telnet server with multi-player sessions
- Command routing and parsing with indexed targeting (e.g., `get 2.sword`, `kill 2.guard`)
- Zone-grouped content loading (114 zones, 7069 rooms, 2545 mobs, 2364 objects)
- Legacy CircleMUD/ROM content import pipeline with proper text trimming
- Mob spawning, object spawning, mob equipment via zone resets
- Lua scripting engine with OnLook, OnEnterRoom, OnSay hooks
- Basic commands: look, say, who, movement, quit, zreset, examine, search
- Connection registry and player tracking
- **Phase 3 (Character System) - COMPLETE:**
  - ActMessage service with substitution codes ($n, $N, $o, $e, $m, $s, etc.)
  - SQLite persistence with EF Core (Account + Character + Inventory + Equipment)
  - Multi-character account system (up to 10 chars per account)
  - Full character creation flow (race, class, sex selection)
  - BCrypt password authentication with IP-based rate limiting
  - Character save/load system (auto-save on quit, manual `save` command, auto-save timer)
  - Character stats and vitals (HP, mana, movement, attributes)
  - Player inventory system (`get`, `drop`, `inventory` commands)
  - Player equipment system (`wear`, `remove`, `wield`, `hold`, `equipment` commands)
  - Score command (full character sheet display)
  - Object instance persistence (stores definition IDs, recreates instances on load)
- **Phase 4.1 (Basic Combat) - COMPLETE:**
  - Combat loop with 2-second tick system (PULSE_VIOLENCE)
  - Damage calculation with weapon dice rolls
  - Hitroll/damroll/AC calculations
  - `kill` command with indexed targeting
  - `flee` command with directional escape
  - `wimpy` command for auto-flee threshold
  - Player death (corpse creation, XP loss, respawn at recall point)
  - Mob death (corpse creation with equipment transfer)
  - Combat messaging with ActMessage integration

### ✅ RECENTLY COMPLETED (Jan 25, 2026 - Session 8)

#### Session 8: Communication Commands System - COMPLETE ✅
- ✅ **Core Communication Commands Implemented - COMPLETE**
  - ✅ `tell <player> <message>` - private player-to-player messaging (blue color #b)
  - ✅ `reply <message>` - respond to last tell sender (tracks LastTellSender)
  - ✅ `emote <action>` / `me <action>` - roleplay actions broadcast to room
  - ✅ `gossip <message>` - global chat channel (bright yellow #Y)
  - ✅ `shout <message>` / `yell <message>` - zone-wide broadcast (cyan #c, costs 10 movement)
  - ✅ Added LastTellSender to PlayerState for reply tracking
  - ✅ All commands support legacy color codes matching original EliteMUD
  - ✅ Message routing: tell=targeted, gossip=global, shout=zone-wide, emote=room
  - ✅ Command handlers: TellHandler, ReplyHandler, EmoteHandler, GossipHandler, ShoutHandler
  - ✅ Result types: TellResult, ReplyResult, EmoteResult, GossipResult, ShoutResult
  - ✅ All 227 tests passing (no new tests yet - manual testing required)
  - ✅ Commit: `c443f71` - "Add communication commands: tell, reply, emote, gossip, shout"
- **Legacy Compliance:**
  - ✅ Color codes match legacy: #b (blue tell), #Y (yellow gossip), #c (cyan yell)
  - ✅ Message formats match legacy: "Name tells you '...'", "Name gossips, '...'"
  - ✅ Shout costs movement points (10) like legacy holler_move_cost
  - ✅ Reply tracks last sender like legacy GET_LAST_TELL(ch)
  - ✅ Emote uses "$n action" format like legacy act.wizard.c
- **Not Yet Implemented:**
  - ❌ Channel history (tell/gossip/shout history display)
  - ❌ Ignore list system
  - ❌ NOTELL/NOGOSSIP preferences
  - ❌ Racial speech additions (grunt, hiss, etc.) for gossip/shout
  - ❌ SILENT room flag blocking communication
  - ❌ Writing flag preventing message receipt
  - ❌ Idle timer warning for tell recipients

### ✅ RECENTLY COMPLETED (Jan 25, 2026 - Session 7)

#### Session 7: Equipment Container Persistence & 'All' Targeting - COMPLETE ✅
- ✅ **Fixed Critical Equipped Container Persistence Bug - COMPLETE**
  - ✅ Issue: Equipped containers (girdles, bags worn as equipment) lost contents on logout/login
  - ✅ Root cause: Equipment only stored ObjectDefinitionId, created fresh empty instances on load
  - ✅ Solution: Added `ItemData` JSON column to CharacterEquipmentItem entity
  - ✅ Unified persistence: Both inventory and equipment now use `InventoryItemDto` JSON format
  - ✅ Backwards compatible: Null ItemData falls back to legacy behavior
  - ✅ Full item tree serialization: Containers + contents + state (IsClosed, IsLocked)
  - ✅ Migration: `20260125120339_AddEquipmentItemData` (additive, non-destructive)
  - ✅ Test: `SaveAndLoad_EquippedContainerWithContents_PersistsCorrectly` validates fix
  - ✅ All 227 tests passing (226 existing + 1 new)
  - ✅ Commit: `b3b998b`
- ✅ **Comprehensive 'All' Targeting Support - COMPLETE**
  - ✅ Enhanced GET command: `get all.wheat container`, `get all.wheat all.corpse`
  - ✅ Enhanced DROP command: `drop all`, `drop all.sword`
  - ✅ Enhanced PUT command: `put all bag`, `put all.wheat bag`
  - ✅ Enhanced REMOVE command: `remove all`, `remove all.ring`
  - ✅ Updated 5 command handlers with multi-object support
  - ✅ Result records include `List<ObjectDefinition>?` for batch operations
  - ✅ Commit: `4c0e492`

### ✅ RECENTLY COMPLETED (Jan 25, 2026 - Session 6)

#### Session 6: Mob AI Tests & Position Bugfix - COMPLETE ✅
- ✅ **Comprehensive Mob AI Test Coverage - COMPLETE**
  - ✅ Created MobAiServiceTests.cs with 20 integration tests
  - ✅ Tests cover all 5 core AI behaviors (aggressive, memory, wandering, helper, position)
  - ✅ Uses reflection to access WorldState internal collections for test setup
  - ✅ MockScriptEngine for Lua hook testing
  - ✅ All 174 tests passing (154 existing + 20 new)
- ✅ **Fixed Critical Position Bug in Mob Combat - COMPLETE**
  - ✅ Issue: Standing players stayed in Position.Standing when attacked by aggressive mobs
  - ✅ Root cause: Wrong comparison `if (player.Position < Position.Fighting)` 
    - Standing=8 > Fighting=7 in enum, so condition was false
  - ✅ Fix: Changed to `if (player.Position > Position.Fighting)` in MobAiService
  - ✅ Now correctly sets Standing/Sitting/Resting → Fighting when attacked
  - ✅ Preserves Fighting position if already fighting
  - ✅ Preserves worse positions (Dead/Mortally/Incap/Stunned)
  - ✅ Applied fix to both ProcessAggressive() and ProcessMemory()
- ✅ **Test Implementation Details**
  - ✅ Test wandering with random movement tracking (100 tick attempts)
  - ✅ Test alignment-specific aggro (evil, good, neutral)
  - ✅ Test memory system with player ID tracking
  - ✅ Test helper alignment difference calculations (≤750 threshold)
  - ✅ Test room flag checks (LAWFUL, NO_MOB)
  - ✅ Test zone restriction (STAY_ZONE flag)

### ✅ RECENTLY COMPLETED (Jan 24, 2026 - Sessions 4-5)

#### Session 5: Alignment Dynamics System - COMPLETE ✅
- ✅ **Legacy-Accurate Alignment Shift Formula - COMPLETE**
  - ✅ Researched legacy `change_alignment()` from fight.c:445-460
  - ✅ Implemented convergent formula: `shift = (-victim_alignment - killer_alignment) / 16`
  - ✅ "Move 1/16th of the way toward opposite of victim's alignment"
  - ✅ No level multipliers (victim level doesn't affect shift)
  - ✅ No PvP special handling (same formula for all kills)
  - ✅ Convergent behavior (shifts decrease as you approach target)
- ✅ **Integration into All Combat Paths - COMPLETE**
  - ✅ GameTickService.HandleMobDeath() - combat tick PvE kills
  - ✅ GameTickService.HandlePlayerDeath() - combat tick PvP kills
  - ✅ BackstabSkillExecutor - instant backstab kills
  - ✅ KickSkillExecutor - instant kick kills
  - ✅ BashSkillExecutor - instant bash kills
  - ✅ Player feedback: significant shifts (|shift| ≥ 50) show messages
- ✅ **Comprehensive Test Coverage - COMPLETE**
  - ✅ Created AlignmentShiftTests.cs with 17 unit tests
  - ✅ Tests cover: neutral vs evil/good/neutral, convergent behavior, level independence, PvP=PvE
  - ✅ All 126 tests passing (109 existing + 17 new), 0 errors
- ✅ **Alignment Display Already Present - VERIFIED**
  - ✅ ScoreHandler.GetAlignmentDescription() matches legacy exactly
  - ✅ 9 alignment descriptions from "horns showing" to "developed a halo"
  - ✅ Thresholds: Good ≥350, Evil ≤-350, Neutral -349 to +349

#### Session 4: Weapon Effects & Combat Enhancements - COMPLETE ✅
- ✅ **Weapon Special Effects - COMPLETE**
  - ✅ BLESS weapon: +3d6 damage vs evil targets (alignment ≤ -350)
  - ✅ EVIL weapon: +3d6 damage vs good targets (alignment ≥ 350)
  - ✅ FLAME weapon: +3d6 fire damage (victim saves vs Physical to negate)
  - ✅ Integrated into all combat paths (kill, backstab, kick, bash, combat tick)
  - ✅ Created test weapons: blessed holy sword, cursed evil dagger, flaming sword
- ✅ **Saving Throw System - COMPLETE**
  - ✅ Added SavingThrowType enum (Physical, Mental, Magic, Poison)
  - ✅ Implemented MakesSavingThrow() in CombatCalculator
  - ✅ Base save by level: 16 - (level/3), minimum 1
  - ✅ Equipment bonuses from SavingPhysical/Mental/Magic/Poison affects
  - ✅ d20 roll system: success if roll + bonus ≥ base_save
- ✅ **Mob Combat Equipment System - COMPLETE**
  - ✅ Added MobAttack and MobCombat records to MobDefinition
  - ✅ Parse mob Attacks array from content JSON (Type, DamageType, Chance, DamageDice)
  - ✅ Parse mob Combat stats from content JSON (Hitroll, Damroll)
  - ✅ Updated mob damage calculation to use attack dice + equipped weapons
  - ✅ Mobs add BOTH weapon dice AND natural attack dice (legacy: fight.c:1471)
  - ✅ Added GetMobEffectiveArmorClass/Hitroll/Damroll extension methods
  - ✅ Mob equipment bonuses now apply (AC, hitroll, damroll from equipped items)
  - ✅ Updated all tests to include empty Attacks and null Combat
- ✅ **Weapon Damage System - COMPLETE**
  - ✅ Updated CombatCalculator.CalculateDamage() to use weapon dice (XdY from ObjectWeapon.DiceCount/DiceSides)
  - ✅ Legacy formula implemented: `str_todam + damroll + dice(weapon.DiceCount, weapon.DiceSides)` (fight.c:1464)
  - ✅ Bare hands fallback: `str_todam + damroll + random(0,2)` (fight.c:1458)
  - ✅ Updated all combat attack paths: PvP, PvE, kick, backstab, GameTickService
  - ✅ Marked CalculateBareDamage() as obsolete in favor of CalculateDamage(weaponDetails)
  - ✅ Added test greatsword (ID 99913): 5d8 damage + 5 hitroll + 10 damroll
- ✅ **Repository Cleanup - COMPLETE**
  - ✅ Removed duplicate imported/ and output/ directories (~37MB, 1.4M lines)
  - ✅ Added to .gitignore to prevent re-commit
  - ✅ Established content/ as single source of truth

### ✅ RECENTLY COMPLETED (Jan 23, 2026 - Session 3)
- ✅ **Data-Driven Skill System with Lua Formulas - COMPLETE**
  - ✅ Created FormulaEvaluator (thread-safe Lua formula engine with 20 comprehensive tests)
  - ✅ All 6 skills now use Lua formulas from `content/skills/skills.json`
  - ✅ Active skills: kick, bash, backstab, rescue use formula-driven damage/success calculations
  - ✅ Passive skills: dodge, parry use formula-driven activation and damage reduction
  - ✅ Centralized all skills in EliteMud.Application/Skills/ (moved DodgeSkill & ParrySkill from Game layer)
  - ✅ Changed skill methods from static to instance for FormulaEvaluator access
  - ✅ All formulas have fallback to hardcoded logic if missing from JSON
  - ✅ Added sleep/wake command aliases (sl, wa)
  - ✅ Added hot reload documentation to SkillMetadataRegistry
  - ✅ All 109 tests passing, 0 warnings, 0 errors
  - ✅ Merged to main branch and pushed to GitHub

### ✅ RECENTLY COMPLETED (Jan 23, 2026 - Session 2)
- ✅ **Additional Combat Skills - COMPLETE**
  - ✅ Bash skill (shield attack, knocks victim sitting, 10 damage, 2 rounds WAIT_STATE)
  - ✅ Backstab skill (surprise attack with 1x-5x damage multiplier, requires unsuspecting victim, 3 rounds WAIT_STATE)
  - ✅ Parry passive skill (block attacks with shield, reduces damage by level, harder to trigger than dodge)
  - ✅ Rescue skill (redirect combat from ally to rescuer, PvE only for now, 2 rounds WAIT_STATE)
  - ✅ Integrated parry into CombatCalculator damage pipeline (checked after dodge fails)
  - ✅ Fixed setskill command to support all new skills (backstab, bs alias, rescue)
  - ✅ Resolved nullable reference warnings in CombatCalculator
  - ✅ All 70 tests passing, 0 warnings, 0 errors
  - ✅ Committed: 4 new active skills + 1 passive skill with full legacy mechanics

### ✅ RECENTLY COMPLETED (Jan 23, 2026 - Session 1)
- ✅ **Skills & Spells POC - COMPLETE & VALIDATED**
  - ✅ Active skill system (kick command with damage calculation)
  - ✅ Passive skill system (dodge with damage reduction)
  - ✅ Skill proficiency tracking (Dictionary<SkillType, byte> 0-100%)
  - ✅ Skill improvement on successful use
  - ✅ Skill persistence (JSON in database Skills column)
  - ✅ Testing commands: `setskill`, `skills`, `setlevel`
  - ✅ PvP and PvE combat integration verified
  - ✅ Fixed dead player attacking bug (race condition in combat tick)
  - ✅ All manual test scenarios passed

### ✅ RECENTLY COMPLETED (Phase 4: Combat + Skills Framework)
- **Skills Framework Extraction:** COMPLETE (Jan 23, 2026)
- **WAIT_STATE System:** COMPLETE (Jan 23, 2026)
- **Skillgain Cooldown:** COMPLETE (Jan 23, 2026)
- **POC Status:** Complete and validated (see `docs/skills-poc-documentation.md`)
- **Framework Status:** ISkillHandler, IPassiveSkillHandler, SkillRegistry, ISkillExecutor all implemented
- **Next Phase:** Skill metadata schema, additional skills (bash, backstab, rescue)

### ❌ NOT STARTED
- Advanced combat skills (Phase 4.2)
- Spells and magic system (Phase 4.3)
- Combat Lua hooks (Phase 4.4)
- Mob AI and behaviors (Phase 5)
- Containers and doors (Phase 6.1, 6.3)
- Consumables (Phase 6.2)
- Shops, mail, boards, clans (Phase 6.4-6.8)
- Admin tools and OLC (Phase 7)

## Next Execution Checklist (Updated Jan 23, 2026 - Session 3)

### 🎯 IMMEDIATE NEXT STEPS (Spell System)
**Priority:** Implement spell system using proven Lua formula architecture

1. **Design Spell Metadata Schema** (Short-term) - NEXT
   - Create `content/spells/spells.json` format (similar to skills.json)
   - Define fields: id, name, aliases, type, manaCost, damage, healing, duration
   - Include Lua formulas: damageFormula, healingFormula, successFormula, durationFormula
   - Support multiple spell types: damage, healing, buff, debuff, utility
   - Add class restrictions and minimum levels

2. **Create ISpellHandler Interface** (Short-term)
   - Define: CanCast(), GetCannotCastMessage(), ManaCost, CastTime
   - Support target types: self, single enemy, single ally, area, room
   - Return SpellResult with success/failure, damage/healing, effects applied
   - Uses ICombatant (supports players and mobs)

3. **Implement SpellRegistry** (Short-term)
   - Auto-discover spell handlers via reflection
   - Register handlers with dependency injection
   - Load spell metadata from JSON at startup
   - Use FormulaEvaluator for formula evaluation (same as skills)

4. **Create Basic Damage Spells** (Medium-term)
   - Magic Missile (low damage, always hits)
   - Shocking Grasp (medium damage, touch attack)
   - Lightning Bolt (high damage, dex save for half)
   - Fireball (area damage, dex save for half)

5. **Create Basic Healing Spells** (Medium-term)
   - Cure Light Wounds (1d8 + level healing)
   - Cure Serious Wounds (2d8 + level healing)
   - Cure Critical Wounds (3d8 + level healing)
   - Heal (full HP restoration)

6. **Create Basic Buff Spells** (Medium-term)
   - Armor (increase AC)
   - Bless (increase hitroll)
   - Strength (increase STR stat)
   - Haste (extra attacks)

7. **Implement `cast` Command** (Medium-term)
   - Parse spell name and target
   - Check mana cost and availability
   - Check spell proficiency
   - Execute spell with FormulaEvaluator
   - Apply WAIT_STATE based on spell metadata
   - Integrate with combat system

8. **Add Spell Proficiency & Improvement** (Long-term)
   - Track spell proficiency like skills (0-100%)
   - Improve on successful casts
   - Formula variables include spellPercent
   - Store in database alongside skill proficiencies

### 📋 FOLLOW-UP FEATURES (After Mob AI Testing)

**Mob AI Next Steps:**
1. **Add Scavenger Tests** (Short-term)
   - Test item pickup behavior (9% chance per tick)
   - Test value-based selection (picks most valuable item)
   - Test room scanning and object detection
   - Requires: Mob inventory system implementation

2. **Add Sentinel/Return Home Tests** (Medium-term)
   - Test hometown tracking
   - Test return path calculation
   - Requires: Pathfinding implementation

3. **Add Tracking Tests** (Medium-term)
   - Test memory victim tracking across rooms
   - Test path calculation and following
   - Requires: Pathfinding implementation (graph.c port)

4. **Implement Mob Inventory System** (Medium-term)
   - Add Inventory property to MobInstance (like PlayerState)
   - Update GiveMob zone reset to populate inventory
   - Update scavenger to store picked items
   - Add mob loot drop on death (currently only equipment drops)

5. **Complete Pathfinding System** (Long-term)
   - Port graph.c pathfinding algorithm
   - Implement perform_track() for mob tracking
   - Test with sentinel return home and memory tracking
   - Add zone-aware pathfinding

### 📋 FOLLOW-UP FEATURES (After Spells)
9. **Expand Formula Variables** (Medium-term)
   - Add stat variables: strength, dexterity, intelligence, wisdom, constitution
   - Add gear variables: weaponDamage, armorClass, magicBonus
   - Add combat state: isBlind, isPoisoned, isHasted
   - Add environment: roomType, weather, timeOfDay

10. **Practice Command** (Long-term)
    - Find guildmaster mobs
    - List available skills/spells for class
    - Practice to improve proficiency (costs gold)
    - Class/level restrictions

11. **Hot Reload Command** (Long-term)
    - Implement `/sreload` admin command for skills
    - Implement `/splreload` admin command for spells
    - Thread-safe metadata/formula reloading
    - Error handling and rollback on invalid JSON

### 📋 DEFERRED (Future Phases)
- Spell system (similar to skills but with mana costs)
- Guild trainers and practice command
- Class-based skill restrictions
- Position-based skill requirements

### ✅ COMPLETED (Jan 22, 2026)
- ✅ **MAJOR FIX:** Fixed equipment persistence bug (was storing instance IDs, now stores definition IDs)
- ✅ Database migration: renamed ObjectId → ObjectDefinitionId in inventory/equipment tables
- ✅ CharacterMapper now uses IWorldState to convert between instance IDs and definition IDs
- ✅ Object instances are recreated on character load from definition IDs
- ✅ `save` command - manual character save
- ✅ Auto-save timer (every 5 minutes via GameTickService)
- ✅ **MAJOR FIX:** Fixed all linebreak issues - trimming at import, content load, and runtime display
- ✅ Re-imported legacy content with proper text trimming
- ✅ Added .Trim() to LegacyImportParser (tilde-terminated strings)
- ✅ Added .Trim() to ContentLoader (mobs, objects, rooms JSON loading)
- ✅ Added .Trim() to all display handlers (inventory, equipment, look, combat)
- ✅ Combat messages now display correctly ("You attack the gremlin!" on one line)
- ✅ Equipment display shows slot and item on same line
- ✅ Item lists no longer have extra blank lines between items

### ✅ COMPLETED (Jan 21, 2026)
- ✅ Combat system - kill, flee, wimpy commands (Phase 4.1 COMPLETE)
- ✅ Damage calculation and combat loop
- ✅ Death handling for players and mobs
- ✅ Corpse creation with loot transfer
- ✅ Indexed targeting for kill command (`kill 2.guard`)

### ✅ COMPLETED (Jan 20, 2026)
- ✅ ActMessage service - room broadcast messaging (Phase 3.0 COMPLETE)
- ✅ Substitution code parser ($n, $N, $o, $e, $m, $s, etc.)
- ✅ Pronoun resolution helpers (he/she/it based on sex)
- ✅ Multi-audience broadcasting (ToChar, ToVict, ToRoom, ToNotVict)
- ✅ Message capitalization and formatting
- ✅ Support for both PlayerState and MobInstance in messaging

### ✅ COMPLETED (Jan 19, 2026)
- ✅ SQLite database schema with EF Core
- ✅ Account and Character entities with full persistence
- ✅ Character creation flow (account → character → race → class → sex)
- ✅ BCrypt password authentication
- ✅ IP-based rate limiting and banning
- ✅ Character save/load on quit/login
- ✅ Multi-character support (up to 10 per account)
- ✅ CharacterMapper for Entity ↔ PlayerState conversion

### Immediate Priorities (Phase 3.1 - Object Interaction) - CURRENT FOCUS
1. **Show objects in look command** - extend LookHandler to display room objects
2. **Add examine command** - new command handler for inspecting objects/mobs
3. **Player inventory system** - wire up PlayerState Items collection with world state
4. **Implement get/drop commands** - object pickup and drop mechanics with act() messages
5. **Add inventory command** - display carried items
6. **Object weight tracking** - calculate total weight carried

### Short-term (Phase 3.2-3.3 - Equipment & Display)
7. **Player equipment system** - wire up existing PlayerState Equipment slots
8. **Implement wear/remove/wield commands** - equipment management
9. **Add equipment command** - show worn/wielded items
10. **Implement score command** - character sheet display
11. **Add stat command** - detailed stats display
12. **Auto-save timer** - periodic character saves (every 5 minutes)

### Medium-term (Phase 4 - Combat)
15. **Basic combat loop** - fighting state and turn system
16. **Damage calculation** - dice rolls and stat modifiers
17. **Combat commands** - kill, flee, bash, kick
18. **Death handling** - corpse creation and respawn
19. **Combat skills framework** - skill definitions and practice system

### Long-term (Phase 5-6 - World Systems)
20. **Mob AI tick system** - aggro, wandering, memory
21. **Container system** - object storage and put/get mechanics
22. **Shop system** - buy/sell commands and shop inventory
23. **Communication commands** - tell, shout, gossip, emote
24. **Group/clan systems** - party mechanics and clan chat
25. **Quest system** - quest tracking and rewards
