# EliteMUD C# Rewrite — Roadmap

_Last reviewed: Oct 10, 2026 (against code at commit `3433792`)_

This is a ground-up rewrite of the legacy EliteMUD C codebase in C# / .NET 10.
This page answers three questions: **where are we, what's next, and what's left.**
Detailed history lives in `git log`; design notes live in the other files under `docs/`.

**Legend:** ✅ done · 🔶 partly done · ⬜ not started

---

## Goals

1. **Gameplay parity** with the legacy C codebase. When in doubt, match the original.
2. **Modern runtime:** .NET 10 on Linux, classic Telnet clients still work.
3. **Lua scripting** for safe, fast iteration on game logic and formulas.
4. **World content as versioned JSON files** (rooms, mobs, objects, zones, scripts).
5. **SQLite for runtime data only** (accounts, characters, mail, logs).

---

## At a Glance

| Area | Status | In one line |
|---|---|---|
| Server & connections | ✅ | Telnet sessions, login, graceful shutdown. Idle/link-dead handling missing. |
| World loading | ✅ | All 114 legacy zones load (7069 rooms, 2545 mobs, 2364 objects). |
| Zone resets | 🔶 | Mobs, objects and mob equipment spawn. Give/put/door/remove resets are stubs. |
| Accounts & characters | ✅ | Create, select, delete characters. Save/load everything, auto-save. |
| Items & equipment | ✅ | Get/drop/put, containers, wear/wield/hold, stat bonuses. No weight limits. |
| Character progression | 🔶 | Stats, regen and positions work. No levelling, race/class tables, hunger. |
| Combat | ✅ | Full combat loop, multi-attack, death/corpses, alignment shifts. |
| Skills | 🔶 | 11 skills, Lua formula driven. Many legacy skills still to port. |
| Spells & affects | 🔶 | 7 spells, buffs with durations. No debuffs, area or utility spells. |
| Mob AI | 🔶 | Aggro, wander, memory, helpers, scavenge, return-home, tracking. No mob programs. |
| Doors | 🔶 | Open/close/lock/unlock/bash, door states set on zone reset. No lockpicking or hidden doors. |
| Communication | 🔶 | say, tell, reply, gossip, shout, emote. No socials, ignore, history. |
| Groups & clans | ⬜ | Nothing yet. |
| Shops, consumables | ⬜ | Nothing yet. |
| Boards, mail, quests | ⬜ | Nothing yet. |
| Admin / immortal tools | 🔶 | `goto`, `load`, `zreset`, `set*` test commands. No permission levels. |
| Building (OLC) & live reload | ⬜ | Nothing yet. |
| Tests | 🔶 | ~229 tests (combat, AI, pathfinding, persistence). No integration/CI. |

---

## Up Next

The suggested order, aiming at a **playable loop from level 1 upwards**.

1. **Fix the open known issues** (below). They are small and silently break content.
2. **Finish zone resets:** `GiveMob`, `PutObject`, `RemoveObject`.
   Mobs need loot and chests need contents.
3. **Levelling:** XP table, level-up gains and messages. Without this, combat has no payoff.
   _In planning: [`docs/plans/levelling-and-classes.md`](plans/levelling-and-classes.md)_
4. **Race & class tables:** stat modifiers, HP/mana/move gains, and which skills/spells each class gets.
   _Covered by the same plan._
5. **Permission levels:** player vs immortal. Required before adding more admin commands.
6. **`help` and `commands`:** new players currently can't discover anything.
7. **Groups:** `follow`, `group`, `assist`, `gtell`, XP sharing.
8. **Shops:** `list`, `buy`, `sell`, `value`, plus restocking.
9. **Consumables & hunger/thirst:** `eat`, `drink`, `quaff`, `recite`, `fill`.

---

## Known Issues

### 🔴 Zone files with the wrong shape load "half way", silently
- **What happens:** Rooms, mobs and objects load, but the zone itself doesn't. `goto` works, `zreset <zone>` says "Zone not found." Nothing is logged.
- **Why:** The grouped format (`zones/zone_*.json`) needs a top-level `"Zone": { ... }` wrapper.
  Without it, `zoneData.Zone` is null and the zone is skipped (`src/EliteMud.Server/ContentLoader.cs:593`).
  There is also an older monolithic format (`content/zones/zones.json`).
- **Correct shape:**
  ```json
  {
    "Zone": {
      "Id": 999, "Name": "Zone Name", "TopRoomId": 99999,
      "Lifespan": 30, "ResetMode": "Normal",
      "RoomRange": { "Min": 99900, "Max": 99999 },
      "ResetCommands": [ ... ]
    },
    "Rooms": [ ... ], "Mobs": [ ... ], "Objects": [ ... ]
  }
  ```
- **To do:**
  - [ ] Log a warning when a zone file has rooms but no `Zone` block
  - [ ] Document the zone file format in `docs/content-schema.md`
  - [ ] Decide on one format and retire the other

### Needs in-game verification
- [ ] Container persistence edge cases (nested bags, closed/locked state, equipped containers). Unit tests pass, but this hasn't been play-tested thoroughly.
- [ ] Communication commands (`tell`, `reply`, `gossip`, `shout`, `emote`) have no automated tests yet.

---

## What's Done

### Server & Connections
- ✅ Telnet server with multiple concurrent sessions
- ✅ Attribute-based command routing with aliases
- ✅ Indexed and "all" targeting everywhere it matters: `get 2.sword`, `get all.wheat all.corpse`, `drop all`
- ✅ Legacy color codes (`#b`, `#Y`, …)
- ✅ Graceful shutdown on Ctrl+C (saves all players)

### World & Content
- ✅ Legacy CircleMUD/ROM importer → versioned JSON zone files (text trimmed properly)
- ✅ All 114 legacy zones loaded, legacy IDs preserved
- ✅ Room and mob flags parsed
- ✅ Zone resets: `LoadMob`, `LoadObject`, `EquipMob` with spawn chances
- ✅ Zone resets as legacy: mobs stay, objects are swept from rooms without players (except `NoSweep` rooms and
  objects), and a mob or object loads only when it's missing
- ✅ Lua hooks: `OnEnterRoom`, `OnLook`, `OnSay`

### Accounts, Characters & Saving
- ✅ Accounts with BCrypt passwords; up to 10 characters per account
- ✅ Character creation: name, sex, race (13), class (20+, race restricted)
- ✅ Login protection: 3 bad passwords → disconnect; 3 failures per IP → 15-minute ban
- ✅ SQLite via EF Core with auto-migration on startup
- ✅ Saves stats, location, skills, spells, inventory and equipment, including container contents and open/locked state
- ✅ Save on quit, `save` command, auto-save every 5 minutes

### Character Stats & Positions
- ✅ HP/mana/move, level, XP, gold, the six attributes, race, class, alignment
- ✅ `score`, `stat`, `affects`
- ✅ Regeneration using the legacy formulas and timings
- ✅ Positions: `stand`, `sit`, `rest`, `sleep`, `wake`. Position affects regen and what you can do (e.g. no `look` while asleep).
- ✅ `recall` for low-level players; death respawns at the Temple (3001)

### Items & Equipment
- ✅ `look`, `examine`, `search`, `get`, `drop`, `put`, `inventory`
- ✅ Containers: `open`, `close`, `lock`, `unlock`, get/put from containers, nested containers
- ✅ `equipment`, `wear` (incl. `wear all`), `wield`, `hold`, `remove`
- ✅ Slot validation, two-handed weapons, "already wearing" messages
- ✅ Equipment bonuses: attributes, max HP/mana/move, AC (legacy slot multipliers), hitroll, damroll, saving throws
- ✅ Weapon special effects: Bless, Evil, Flame

### Combat
- ✅ Combat loop on the 2-second legacy tick
- ✅ `kill`, `flee`, `wimpy`, `consider`
- ✅ Legacy damage formulas: weapon dice, strength bonuses, hitroll/damroll/AC
- ✅ Multi-attack: 2nd/3rd/4th attack and dual-wield (up to 5 attacks per round)
- ✅ Mobs use natural attack dice plus their weapons
- ✅ Saving throws (Physical, Mental, Magic, Poison)
- ✅ Player and mob death, corpses with loot, XP loss
- ✅ Legacy alignment shifts on kills
- ✅ Victims auto-stand when hit and are put into fighting position

### Skills & Spells
- ✅ Data-driven framework: metadata and Lua formulas in `content/skills/skills.json` and `content/spells/spells.json`
- ✅ Proficiency 0–100%, improvement on use with a 60-second cooldown, saved to the DB
- ✅ Action delays (WAIT_STATE)
- ✅ **Skills (11):** kick, bash, backstab, rescue, track, dodge, parry, second/third/fourth attack, dual-wield
- ✅ **Spells (7):** magic missile, lightning bolt, burning hands, cure light, cure serious, armor, bless
- ✅ Timed affects with expiry
- ✅ Test helpers: `setskill`, `setspell`, `setlevel`, `skills`

### Mob AI
- ✅ Aggressive (plus evil/good/neutral variants and wimpy)
- ✅ Wandering (anti-bounce, respects STAY_ZONE, NO_MOB, DEATH)
- ✅ Memory: hunts players who attacked it, respects LAWFUL rooms
- ✅ Helpers assist allies of similar alignment
- ✅ Scavengers pick up the most valuable item; mobs have inventories
- ✅ Sentinels walk back home
- ✅ BFS pathfinding (zone-aware, max distance), used for tracking and return-home

### Doors
- ✅ Door state per exit: `open`/`o`, `close`, `lock`, `unlock`
- ✅ `bash` doors open
- ✅ Zone resets set door states (`D`); closed exits that aren't doors are barriers; closed exits block players, flee, mobs and pathfinding

### Communication
- ✅ `say`, `tell`, `reply`, `gossip`/`gos`, `shout`/`yell` (costs 10 move), `emote`/`me`, `who`

### Admin
- ✅ `goto`, `load`, `zreset`
- ✅ Legacy world import as a CLI tool (`src/EliteMud.Cli`)

---

## What's Left

Grouped by area. Checkboxes make this usable as a working list.

### Server & Connections
- [ ] Idle timeout and `idle` display
- [ ] Link-dead state, reconnecting to a link-dead character, auto-quit after a while
- [ ] Site bans by IP/hostname (`ban`, `unban`, persisted; block new characters vs all play). Legacy: `ban.c`
- [ ] Password recovery / optional email (see `docs/FUTURE_IMPROVEMENTS.md`)
- [ ] Command history (legacy: `history.c`)

### World & Zone Resets
- [ ] `GiveMob`: put items in a mob's inventory (inventory exists, the reset is a stub)
- [ ] `PutObject`: put items in containers (stub)
- [ ] `RemoveObject` (stub)
- [ ] Room regen flags (REGEN_HP / MANA / MOVE)
- [ ] Hidden doors / secret exits, `pick` lockpicking
- [ ] Time of day and weather (`time`, `weather`)

### Character Progression
- [ ] XP table, automatic level-up, HP/mana/move gains, level-up messages
- [ ] Race table: stat modifiers, size, skill bonuses (currently just strings)
- [ ] Class table: per-level gains, skill/spell lists (currently just strings)
- [ ] Stat caps by race/class
- [ ] Stat rolling or point allocation (currently everyone starts with 11s)
- [ ] Starting equipment by class; starting location by race/class
- [ ] Hunger and thirst: tracking, warnings, effects on regen, starvation
- [ ] Drunk and poison status effects

### Items
- [ ] Weight and carry limits (players and containers)
- [ ] `give`
- [ ] Level/class/race restrictions on equipment
- [ ] Item affects (e.g. DETECT_INVIS, INVISIBLE from gear)
- [ ] Cursed items
- [ ] Durability and repair
- [ ] Consumables: `eat`, `drink`, `quaff`, `recite`, `use`, `fill`, `pour`, `sacrifice`/`junk`
- [ ] Potion/scroll/wand effects and charges

### Combat
- [ ] `assist`
- [ ] Attack types (slash/pierce/bludgeon) vs armor
- [ ] Critical hits and fumbles, weapon proficiencies
- [ ] First-strike bonus, sneak attack from hiding
- [ ] Combat stances
- [ ] Rescue in PvP
- [ ] Lua hooks: `OnCombatStart`, `OnDamage`, `OnDeath`, `OnKill`

### Skills & Spells
- [ ] More skills: disarm, trip, circle, tumble, riposte, berserk, plus the rest of the legacy list
- [ ] Debuffs: curse, weaken, slow, poison
- [ ] Utility: detect magic, invisibility, teleport
- [ ] Area spells (fireball, earthquake), summoning, charm
- [ ] `practice` at guildmasters
- [ ] More formula variables (attributes, gear, status, environment)
- [ ] Hot reload of skills/spells metadata

### Mobs
- [ ] Mob programs → Lua triggers: greet, give, speech, death, fight, HP%
- [ ] Shopkeepers, guildmasters, quest givers
- [ ] Guards that block passage; patrol routes
- [ ] Act messages for scavenging; carry limits for mobs

### Social & Communication
- [ ] Socials (smile, hug, …) loaded from data. Legacy: `act.social.c`
- [ ] `ignore` / `unignore` (persisted). Legacy: `ignore.c`
- [ ] Channel history, NOTELL/NOGOSSIP toggles, SILENT rooms
- [ ] `pose`, `auction`
- [ ] Groups: `follow`, `group`, `split`, `gtell`, XP sharing
- [ ] Clans: membership, ranks, `ctell`, halls, banks
- [ ] Boards and mail (SQLite-backed)

### Economy & Quests
- [ ] Shops: `list`, `buy`, `sell`, `value`, restocking, charisma pricing
- [ ] Quests: definitions, objectives, rewards, `quest`
- [ ] Casino games. Legacy: `casino.c`, `gen_cards.c`
- [ ] Special zones/procedures. Legacy: `castle.c` and other spec procs

### Player Utilities
- [ ] `help`, `commands`, `spells`, `areas`, `where`, `scan`
- [ ] `title`, `prompt`, `brief`/`compact`, `color`, `afk`, `visible`, `clear`
- [ ] `alias`, `config`

### Admin & Building
- [ ] Permission / immortal levels
- [ ] `transfer`, `purge`, `set`, `advance`, `shutdown`, `wizhelp`, wizinvis
- [ ] Command audit log
- [ ] OLC: `redit`, `medit`, `oedit`, `zedit`, saving back to zone files
- [ ] Lua hot reload and script error reporting to immortals

### Quality & Operations
- [ ] Integration tests for commands; combat simulations
- [ ] Content validation tests (would have caught the zone schema issue)
- [ ] CI pipeline
- [ ] Load testing (100+ players), benchmarks, allocation tuning
- [ ] Deployment guide (Linux, systemd)
- [ ] Player, builder, immortal and Lua scripting guides

---

## Polish Ideas

Small UX changes noted during play-testing.

- **Wait-state message:** the "you're busy" message feels technical and annoying. Compare it with the legacy wording. (`src/EliteMud.Application/Skills/`)
- **Stat display:** `Dex: [13 (11+2)]` repeats itself. Consider `Dex: [11+2]` or `Dex: [13]` with a separate breakdown. (`StatHandler.FormatStatWithModifier`)
- **Racial speech** on gossip/shout (grunt, hiss, …) as in legacy.

---

## Legacy Module Map

Where each legacy C file's behavior lives (or will live).

| Legacy file | Status | Notes |
|---|---|---|
| `comm.c` | ✅ | Server networking and sessions |
| `interpreter.c` | 🔶 | Command routing done; permission levels missing |
| `db.c` | ✅ | World loading and persistence |
| `structs.h` | ✅ | Domain models and enums |
| `fight.c` | ✅ | Combat, multi-attack, alignment |
| `magic.c`, `spells.c` | 🔶 | Framework done; most spells still to port |
| `mobact.c` | 🔶 | Core AI done; mob programs missing |
| `graph.c` | ✅ | BFS pathfinding, `track` |
| `act.social.c` | ⬜ | Socials |
| `boards.c`, `mail.c`, `clan.c` | ⬜ | Boards, mail, clans |
| `ban.c` | ⬜ | Site bans (IP rate-limit bans exist) |
| `history.c`, `ignore.c` | ⬜ | Command history, ignore list |
| `casino.c`, `gen_cards.c` | ⬜ | Casino games |
| `castle.c` | ⬜ | Special areas |

---

## Milestone History

A short summary. See `git log` for details.

| Date | Milestone |
|---|---|
| Jan 19, 2026 | SQLite persistence, accounts, character creation, login security |
| Jan 20, 2026 | ActMessage messaging system |
| Jan 21, 2026 | Combat: kill, flee, wimpy, death and corpses |
| Jan 22, 2026 | Equipment persistence fix, text trimming fix, auto-save, `consider` |
| Jan 23, 2026 | Skills & spells framework with Lua formulas; bash, backstab, rescue, parry; 5 spells, affects, `stat`, `who` |
| Jan 24, 2026 | Weapon dice and effects, saving throws, mob attacks, alignment shifts |
| Jan 25, 2026 | Mob AI, pathfinding and `track`, containers, doors, communication commands, `recall`, graceful shutdown |
| Jan 26, 2026 | Multi-attack and dual-wield |
| Aug 29, 2026 | Project README |
