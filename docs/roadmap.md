# EliteMUD C# Rewrite Roadmap

## Goals
- Preserve gameplay parity with the legacy C codebase.
- Target .NET 10 on Linux with classic Telnet compatibility.
- Introduce Lua scripting for extensibility and safe iteration.
- Store world content (rooms, mobs, objects, zones, scripts) as versioned files.
- Use SQLite for runtime/player data (players, mail, logs).

## Current Status (As of Jan 19, 2026)

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
- ❌ Item stat bonuses - apply +STR, +HP, +AC, etc. from equipped items - **TODO**
- ❌ Item affects - apply buffs/debuffs from equipped items (e.g., DETECT_INVIS, INVISIBLE) - **TODO**
- ❌ Equipment stat calculation - recalculate total stats when equipping/removing - **TODO**

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
- ❌ Hunger/thirst messages and warnings - **TODO**
- ❌ Hunger/thirst effects on regen and combat - **TODO**
- ❌ Death from starvation/dehydration - **TODO**

#### 3.4 Persistence Layer (SQLite) - COMPLETE ✅
- ✅ Database schema with EF Core (Account + Character entities)
- ✅ Account table (username, password hash, last login)
- ✅ Character table (name, stats, vitals, location, resources, metadata)
- ✅ CharacterInventoryItem table (character inventory persistence) - **stores ObjectDefinitionId**
- ✅ CharacterEquipmentItem table (character equipment persistence) - **stores ObjectDefinitionId**
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
- ❌ Idle timeout - disconnect users after X minutes of inactivity - **TODO**
- ❌ Password recovery system - **Documented in FUTURE_IMPROVEMENTS.md**
- ❌ Email registration (optional) - **TODO**
- ❌ Permission/immortal level system - **TODO**
- ❌ Affects/buffs/debuffs system - **TODO**

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
- ✅ `kick` - unarmed attack (POC COMPLETE, ready for framework extraction)
- ❌ `bash` - shield bash attack
- ❌ `rescue` - take aggro from group member
- ❌ `consider` - estimate mob difficulty

#### 4.3 Skills & Spells Framework - POC COMPLETE ✅
**POC Status:** Fully validated and ready for framework extraction (see `docs/skills-poc-documentation.md`)

**POC Proven Working:**
- ✅ Active skill system (kick)
- ✅ Passive skill system (dodge)
- ✅ Skill proficiency tracking (0-100%)
- ✅ Skill improvement on use
- ✅ Skill persistence (database JSON)
- ✅ PvP and PvE integration

**Next Steps - Framework Extraction:**
1. ⏳ Create `ISkillHandler` interface from working kick implementation
2. ⏳ Create `IPassiveSkillHandler` interface from working dodge implementation
3. ⏳ Build `SkillRegistry` with auto-discovery via reflection
4. ⏳ Add dependency injection for skill handlers
5. ⏳ Refactor kick to implement `ISkillHandler`
6. ⏳ Refactor dodge to implement `IPassiveSkillHandler`

**Next Steps - Content-Driven System:**
1. ⏳ Design skill metadata JSON schema
2. ⏳ Move skill definitions to `content/skills.json`
3. ⏳ Add class-based skill caps and availability
4. ⏳ Implement WAIT_STATE system (action cooldowns)
5. ⏳ Implement skillgain cooldown system
6. ⏳ Add position-based skill restrictions

**Future - Expand Skill Set:**
- ❌ `bash` - shield bash skill
- ❌ `backstab` - rogue sneak attack
- ❌ `circle` - circle around to backstab again
- ❌ `parry` - passive block with weapon
- ❌ `tumble` - passive dodge improvement
- ❌ `disarm` - remove opponent's weapon
- ❌ `trip` - knock opponent down

**Spell System (Similar Architecture):**
- ❌ Spell casting framework
- ❌ `cast` command
- ❌ Mana cost system
- ❌ Spell success/failure rolls
- ❌ `practice` command - train skills/spells with guildmaster
- ❌ Cooldown system
- ❌ Resist tables (saves vs spell/paralysis/breath/etc.)

#### 4.4 Combat Events & Hooks
- ❌ Lua hook: OnCombatStart
- ❌ Lua hook: OnDamage
- ❌ Lua hook: OnDeath
- ❌ Lua hook: OnKill
- ❌ Combat logging and messaging

### ⏳ Phase 5: NPCs + AI (PLANNED)
**Milestone:** Mobs behave like legacy

#### 5.1 Mob AI Foundation
- ❌ Mob tick system (periodic AI updates)
- ❌ Aggro system - mobs attack on sight
- ❌ Wandering/roaming behavior
- ❌ Memory system - mobs remember attackers
- ❌ Assist system - mobs help each other
- ❌ Mob inventory system (for GiveMob zone reset)

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
- ❌ `tell` command - private messaging
- ❌ `shout` command - zone-wide broadcast
- ❌ `gossip` command - global chat channel
- ❌ `emote` / `me` command - roleplay actions
- ❌ `pose` command - set room presence
- ❌ `auction` command - auction channel
- ❌ Ignore list system

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
- ❌ `read` command - read board messages
- ❌ `write` command - post to board
- ❌ `remove` command - delete own posts
- ❌ `mail` command - send persistent mail
- ❌ Mail retrieval at post offices
- ❌ Mail storage in SQLite

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


## Legacy Module Mapping (C → C# Targets)
- `comm.c` → ✅ Server networking + session handling (DONE)
- `interpreter.c` → ✅ Command routing (DONE) + ❌ permissions (TODO)
- `db.c` → ✅ World loading (DONE) + ✅ persistence layer (DONE)
- `structs.h` → ✅ Domain model + enums (DONE)
- `fight.c`, `magic.c` → 🔄 Combat services (IN PROGRESS - basic combat DONE, spells TODO)
- `mobact.c`, `mobcmd.c` → ❌ AI + scripting hooks (TODO)
- `boards.c`, `mail.c`, `clan.c` → ❌ Feature services (TODO)

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

### ✅ RECENTLY COMPLETED (Jan 23, 2026)
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

## Next Execution Checklist (Updated Jan 23, 2026)

### ✅ RECENTLY COMPLETED (Jan 23, 2026)
- ✅ **Skills & Spells POC - COMPLETE & VALIDATED**
  - ✅ Active skill: kick (combat skill with damage calculation)
  - ✅ Passive skill: dodge (automatic damage reduction)
  - ✅ Skill proficiency tracking and improvement
  - ✅ Skill persistence (JSON in database)
  - ✅ Testing commands: setskill, skills, setlevel
  - ✅ Fixed dead player attacking bug (combat tick race condition)
  - ✅ All 4 manual test scenarios passed
  - ✅ POC fully documented (see `docs/skills-poc-documentation.md`)

### 🎯 IMMEDIATE NEXT STEPS (Skills System Completion)
**Priority:** Complete skills system infrastructure

1. ✅ **Create ISkillHandler Interface** (Short-term) - DONE
   - ✅ Extracted interface for active skills (kick, bash, backstab)
   - ✅ Define: CanUse(), GetCannotUseMessage(), Name, Description, MinimumLevel
   - ✅ Uses ICombatant (supports players and mobs)

2. ✅ **Create IPassiveSkillHandler Interface** (Short-term) - DONE
   - ✅ Extracted interface for passive skills (dodge, parry, riposte)
   - ✅ Define: CanActivate(), TryActivate() returning PassiveSkillResult
   - ✅ Support automatic triggering in combat flow

3. ✅ **Extract POC Skills to Framework** (Short-term) - DONE
   - ✅ Created KickSkill implementing ISkillHandler
   - ✅ Created DodgeSkill implementing IPassiveSkillHandler
   - ✅ Removed inline logic from CombatCalculator

4. ✅ **Build SkillRegistry** (Short-term) - DONE
   - ✅ Auto-discover skill handlers via reflection (ISkillHandler + IPassiveSkillHandler)
   - ✅ Register handlers with dependency injection (SkillRegistry singleton)
   - ✅ Skills injected into CombatCalculator and other consumers
   - ✅ ISkillExecutor auto-discovery with SkillCommandHandler wrapping
   - ⚠️ **TODO: Handle session takeover on reconnect** (preserve fight state, wait states, skill cooldowns)

5. ✅ **Implement WAIT_STATE System** (Medium-term) - COMPLETE
   - ✅ Added WaitState property to PlayerState (PlayerState.WaitState:230)
   - ✅ Prevent actions while waiting (CommandRouter checks CanAct():74-80)
   - ✅ Integrated with combat tick (GameTickService decrements each tick:116)
   - ✅ Block skill/command usage during wait state (exempts informational commands)
   - ✅ Implementation: WorldModels.cs:334-345, CombatCalculator.cs:7-30

6. **Design Skill Metadata Schema** (Medium-term) - NEXT
   - Create `content/skills.json` format
   - Define: name, type, damage, cooldown, level requirements, wait_state
   - Include class restrictions and skill caps

7. ✅ **Implement Skillgain Cooldown** (Medium-term) - COMPLETE
   - ✅ Added LastSkillgainTime tracking (Dictionary<SkillType, DateTime>)
   - ✅ 60-second cooldown between improvements for same skill
   - ✅ Stored in database as JSON (Character.LastSkillgainTimes)
   - ✅ Checked in PlayerState.TryImproveSkill() before allowing improvement
   - ✅ Player notified when skill improves: "Your skill - kick - just improved!"
   - ✅ Implementation: WorldModels.cs:312-361, CombatConstants:33-36
   - ✅ Tests: SkillSystemTests.cs (2 new tests for cooldown verification)

8. **Add More Skills** (Long-term)
   - bash (shield attack)
   - backstab (rogue skill)
   - parry (passive defense)
   - rescue (tank skill)
   - disarm, trip, circle, etc.

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
