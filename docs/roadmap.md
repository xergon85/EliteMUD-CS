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

### 🔄 Phase 3: IN PROGRESS (Character System)
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
- ✅ `examine` command - inspect objects/mobs - already implemented
- ✅ Player inventory system - PlayerState inventory fully wired
- ✅ `get` / `take` commands - pick up objects from room with ActMessage
- ✅ `drop` command - drop objects to room with ActMessage
- ✅ `inventory` / `i` command - list carried items - already implemented
- ❌ `give` command - transfer objects to other players - deferred (needs IWorldState.GiveObject)
- ❌ Object weight and carry capacity limits - TODO

#### 3.2 Player Equipment System (CURRENT - IN PROGRESS)
- ❌ Extend PlayerState with Equipment dictionary (copy from MobInstance)
- ❌ `equipment` / `eq` command - show worn/wielded items
- ❌ `wear` command - equip wearable items
- ❌ `remove` command - unequip items to inventory
- ❌ `wield` / `hold` commands - equip weapons/lights
- ❌ Equipment slot validation (can't wear two body armors, etc.)

#### 3.3 Character Stats & Resources (PARTIAL)
- ✅ PlayerState has: HP, MaxHP, Mana, MaxMana, Movement, MaxMovement
- ✅ PlayerState has: Level, Experience, Gold, BankGold
- ✅ PlayerState has: Str, Dex, Con, Int, Wis, Cha
- ✅ PlayerState has: Race, Class, Alignment, Sex
- ❌ `score` command - display full character sheet
- ❌ `stat` command - display detailed stats
- ❌ HP/mana/movement regeneration tick system

#### 3.4 Persistence Layer (SQLite) - MOSTLY COMPLETE ✅
- ✅ Database schema with EF Core (Account + Character entities)
- ✅ Account table (username, password hash, last login)
- ✅ Character table (name, stats, vitals, location, resources, metadata)
- ✅ CharacterInventoryItem table (character inventory persistence)
- ✅ CharacterEquipmentItem table (character equipment persistence)
- ✅ Repository pattern (IAccountRepository, ICharacterRepository)
- ✅ PasswordService with BCrypt hashing
- ✅ CharacterMapper (Entity ↔ PlayerState conversion)
- ✅ Player save on quit (automatic via TelnetServer finally block)
- ✅ Player load on login (loads selected character from DB)
- ✅ Database auto-migration on startup
- ❌ Auto-save timer (every 5 minutes) - **TODO**
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
- ❌ Affects/buffs/debuffs system - **TODO**

### ⏳ Phase 4: Combat + Skills (PLANNED)
**Milestone:** Core combat parity

#### 4.1 Basic Combat System
- ❌ Add fighting state to PlayerState and MobInstance
- ❌ `kill` / `hit` command - initiate combat
- ❌ Combat loop / turn system
- ❌ Damage calculation (dice rolls using weapon stats)
- ❌ Hitroll / damroll / armor class calculations
- ❌ `flee` command - escape from combat
- ❌ Death handling for players
- ❌ Death handling for mobs
- ❌ Corpse creation with loot

#### 4.2 Combat Commands
- ❌ `bash` - shield bash attack
- ❌ `kick` - unarmed attack
- ❌ `rescue` - take aggro from group member
- ❌ `consider` - estimate mob difficulty
- ❌ Auto-attack loop (continue fighting each round)

#### 4.3 Skills & Spells Framework
- ❌ Skill definition system
- ❌ Skill learning/practice
- ❌ `practice` command - train skills with guildmaster
- ❌ Spell casting framework
- ❌ `cast` command
- ❌ Mana cost system
- ❌ Spell success/failure rolls
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
- ❌ `put` command - put item in container
- ❌ `get <item> from <container>` - retrieve from container
- ❌ `open` / `close` commands - container state
- ❌ `lock` / `unlock` commands - keyed containers
- ❌ Weight limits for containers
- ❌ Corpse containers (loot corpses)

#### 6.2 Door System
- ❌ Door state tracking (open/closed/locked)
- ❌ `open` / `close` door commands
- ❌ `lock` / `unlock` door commands
- ❌ `pick` command - lockpicking
- ❌ `bash` door command - force doors open
- ❌ Hidden doors / secret exits
- ❌ DoorState zone reset implementation

#### 6.3 Shop System
- ❌ Shop definition in zone files
- ❌ `list` command - show shop inventory
- ❌ `buy` command - purchase items
- ❌ `sell` command - sell items to shop
- ❌ `value` command - appraise item value
- ❌ Shop inventory restocking
- ❌ Haggling / charisma price modifiers

#### 6.4 Communication Commands
- ❌ `tell` command - private messaging
- ❌ `shout` command - zone-wide broadcast
- ❌ `gossip` command - global chat channel
- ❌ `emote` / `me` command - roleplay actions
- ❌ `pose` command - set room presence
- ❌ `auction` command - auction channel
- ❌ Ignore list system

#### 6.5 Social Systems
- ❌ `follow` command - follow another player
- ❌ `group` command - form/manage groups
- ❌ `split` command - divide gold among group
- ❌ Experience sharing in groups
- ❌ Group combat coordination
- ❌ `gtell` command - group chat

#### 6.6 Clan System
- ❌ Clan definition in database
- ❌ Clan membership tracking
- ❌ Clan ranks/hierarchy
- ❌ `ctell` command - clan chat
- ❌ Clan halls / private areas
- ❌ Clan banks / shared storage

#### 6.7 Board & Mail System
- ❌ Bulletin board object type
- ❌ `read` command - read board messages
- ❌ `write` command - post to board
- ❌ `remove` command - delete own posts
- ❌ `mail` command - send persistent mail
- ❌ Mail retrieval at post offices
- ❌ Mail storage in SQLite

#### 6.8 Quest System
- ❌ Quest definition format
- ❌ Quest tracking in PlayerState
- ❌ Quest objectives (kill, fetch, explore)
- ❌ Quest rewards (exp, gold, items)
- ❌ `quest` command - view active quests
- ❌ Quest completion triggers

#### 6.9 Admin Commands
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
- `db.c` → ✅ World loading (DONE) + ❌ persistence layer (TODO)
- `structs.h` → ✅ Domain model + enums (DONE)
- `fight.c`, `magic.c` → ❌ Combat/spell services (TODO)
- `mobact.c`, `mobcmd.c` → ❌ AI + scripting hooks (TODO)
- `boards.c`, `mail.c`, `clan.c` → ❌ Feature services (TODO)

## Implementation Status Summary

### ✅ COMPLETE (Production Ready)
- Telnet server with multi-player sessions
- Command routing and parsing
- Zone-grouped content loading (114 zones, 7069 rooms, 2545 mobs, 2364 objects)
- Legacy CircleMUD/ROM content import pipeline
- Mob spawning, object spawning, mob equipment via zone resets
- Lua scripting engine with OnLook, OnEnterRoom, OnSay hooks
- Basic commands: look, say, who, movement, quit, zreset
- Connection registry and player tracking
- **SQLite persistence with EF Core (Account + Character tables)**
- **Multi-character account system (up to 10 chars per account)**
- **Full character creation flow (race, class, sex selection)**
- **BCrypt password authentication**
- **IP-based rate limiting and banning (15 min ban after 3 attempts)**
- **Character save/load on quit/login**
- **Character stats and vitals (HP, mana, movement, attributes)**

### 🔄 IN PROGRESS (Phase 3: Character System)
- **CURRENT:** Player inventory and object interaction (Phase 3.1)
- Player equipment system (Phase 3.2)
- Score/stat display commands (Phase 3.3)
- Auto-save timer (Phase 3.4)

### ❌ NOT STARTED
- Combat system (Phase 4)
- Skills and spells (Phase 4)
- Mob AI and behaviors (Phase 5)
- Containers and doors (Phase 6)
- Shops, mail, boards, clans (Phase 6)
- Admin tools and OLC (Phase 7)

## Next Execution Checklist (Updated Jan 20, 2026)

### ✅ RECENTLY COMPLETED (Jan 20, 2026)
- ✅ ActMessage service - room broadcast messaging (Phase 3.0 COMPLETE)
- ✅ Substitution code parser ($n, $N, $o, $e, $m, $s, etc.)
- ✅ Pronoun resolution helpers (he/she/it based on sex)
- ✅ Multi-audience broadcasting (ToChar, ToVict, ToRoom, ToNotVict)
- ✅ Message capitalization and formatting
- ✅ Support for both PlayerState and MobInstance in messaging

### ✅ COMPLETED EARLIER (Jan 19, 2026)
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
