# EliteMUD C# Rewrite Roadmap

## Goals
- Preserve gameplay parity with the legacy C codebase.
- Target .NET 10 on Linux with classic Telnet compatibility.
- Introduce Lua scripting for extensibility and safe iteration.
- Use SQLite as the canonical data store with legacy adapters.

## Phase 1: Core Loop + World Skeleton
**Milestone:** Minimal playable loop
- Telnet session handling and input pipeline
- Login/character creation
- `look`, `say`, `who`, directional movement
- Bootstrap world with 2 rooms and exits
- Lua hooks: `OnEnterRoom`, `OnLook`, `OnSay`

## Phase 2: Data Layer + Legacy Adapters
**Milestone:** Legacy rooms loadable in C#
- SQLite schema v1 (rooms, exits, mobs, objects, scripts, players)
- Import pipeline: legacy `wld/zon/obj/mob` → SQLite
- Runtime loads from SQLite by default
- Preserve raw legacy IDs for parity

## Phase 3: Character System
**Milestone:** Playable persistent characters
- Stats, classes, races
- Inventory/equipment slots
- Affects/status (buffs/debuffs)
- Save/load players to SQLite
- Permission system for immortals

## Phase 4: Combat + Skills
**Milestone:** Core combat parity
- Combat loop, damage rolls, death handling
- Skills/spells framework
- Resource costs, cooldowns, resist tables
- Lua hook points for combat events

## Phase 5: NPCs + AI
**Milestone:** Mobs behave like legacy
- Mob prototypes + spawn/reset logic
- Aggro, wandering, basic AI ticks
- Legacy mob programs mapped to Lua triggers

## Phase 6: World Systems
**Milestone:** Social and world features
- Shops, mail, boards, clans, quests
- Special procedures/triggers
- Admin commands, logging, moderation

## Phase 7: Tools + Extensibility
**Milestone:** Modern content workflow
- Live reload for scripts
- Admin tools to edit world data
- Export to new data format (post-migration)

## Legacy Module Mapping (C → C# Targets)
- `comm.c` → Server networking + session handling
- `interpreter.c` → Command routing and permissions
- `db.c` → World loading + persistence layer
- `structs.h` → Domain model + enums
- `fight.c`, `magic.c` → Combat/spell services
- `mobact.c`, `mobcmd.c` → AI + scripting hooks
- `boards.c`, `mail.c`, `clan.c` → Feature services

## Next Execution Checklist
1) Finalize SQLite schema v1 + repositories
2) Implement legacy room/exit loader
3) Persist player records with login flow
4) Add combat service skeleton
5) Expand Lua hooks for triggers
