# Roadmap Additions - January 24, 2026

This document summarizes the new features and phases added to the roadmap based on analysis of the legacy EliteMUD codebase.

## Summary of Changes

Added **98 new items** across **11 new sections** to ensure complete legacy feature parity.

## New Phases Added

### Phase 3 Additions (Character System)

#### 3.2 Equipment System
- ✅ Added 3 items: Cursed items, item durability, repair system

#### 3.3 Character Stats
- ✅ Added 3 items: drink/eat commands reference, drunk effects, poison effects

#### 3.7 Idle & Connection Management (NEW)
- ❌ 5 items: Idle timeout, idle command, link-dead handling, reconnection, auto-quit

#### 3.8 Site Ban System (NEW)
- ❌ 5 items: Site bans, ban/unban commands, persistence, creation blocking
- **Legacy source:** `ban.c`

### Phase 4 Additions (Combat + Skills)

#### 4.2 Combat Commands
- ✅ Added 1 item: `assist` command

#### 4.6 Advanced Combat Mechanics (NEW)
- ❌ 8 items: Combat rounds, first strike, sneak attacks, multi-attack, attack types, critical hits, weapon proficiency, combat stances

### Phase 5 Additions (NPCs + AI)

#### 5.5 Pathfinding & Navigation (NEW)
- ❌ 4 items: Shortest path algorithm, track system, guard behavior, patrol routes
- **Legacy source:** `graph.c`

### Phase 6 Additions (World Systems)

#### 6.8 Board & Mail System
- ✅ Expanded from 7 to 12 items with specific command details
- Added: `look board`, message numbers, notification system

#### 6.11 History System (NEW)
- ❌ 4 items: Command history buffer, history command, arrow key support, persistence
- **Legacy source:** `history.c`

#### 6.12 Ignore System (NEW)
- ❌ 4 items: Ignore/unignore commands, persistence, channel filtering
- **Legacy source:** `ignore.c`

#### 6.13 Social Commands (NEW)
- ❌ 5 items: Social system, data file loading, messaging, broadcasts, OLC editor
- **Legacy source:** `act.social.c`

#### 6.14 Casino/Games System (NEW)
- ❌ 5 items: Casino flags, card games, dice games, gambling, house odds
- **Legacy source:** `casino.c`, `gen_cards.c`

#### 6.15 Special Areas/Quests (NEW)
- ❌ 4 items: Castle zone, quest-specific programs, special resets, legacy procedures
- **Legacy source:** `castle.c`

#### 6.16 Player Commands (NEW)
- ❌ 23 items organized into 3 categories:
  - **High Priority:** 10 commands (time, weather, title, prompt, display modes, afk, reply, visible)
  - **Information:** 8 commands (areas, help, commands, spells, gen, where, track, scan)
  - **Advanced:** 3 commands (alias, trigger, config)

### New Phases (8-10)

#### Phase 8: Performance & Optimization (NEW)
- ❌ 8 items: Object pooling, combat optimization, room cache, player index, benchmarks, profiling, connection pooling, zone reset tuning

#### Phase 9: Testing & Quality (NEW)
- ❌ 8 items: Integration tests, combat simulation, load testing, content validation, regression suite, CI/CD, smoke tests, performance regression

#### Phase 10: Documentation (NEW)
- ❌ 8 items: Player guide, builder guide, immortal guide, Lua scripting guide, content schema, architecture docs, deployment guide, troubleshooting

## Updated Legacy Module Mapping

Added mappings for 7 previously undocumented legacy modules:

| Legacy Module | Target Phase | Status |
|--------------|--------------|--------|
| `act.social.c` | Phase 6.13 | TODO |
| `casino.c`, `gen_cards.c` | Phase 6.14 | TODO |
| `castle.c` | Phase 6.15 | TODO |
| `history.c` | Phase 6.11 | TODO |
| `ignore.c` | Phase 6.12 | TODO |
| `graph.c` | Phase 5.5 | TODO |
| `ban.c` | Phase 3.8 | TODO |

## Key Missing Features Identified

### High Priority (Common Player Usage)
1. **History System** - Command recall is expected in modern MUDs
2. **Ignore System** - Essential for player harassment prevention
3. **Social Commands** - Core roleplay functionality (50+ socials in legacy)
4. **Idle Management** - Link-dead handling and reconnection
5. **Player Info Commands** - time, weather, areas, help, etc.

### Medium Priority (Enhanced Experience)
1. **Advanced Combat** - Multi-attack, critical hits, weapon proficiency
2. **Casino/Games** - Unique feature from legacy, player retention
3. **Pathfinding** - Mob tracking and intelligent movement
4. **Site Bans** - Admin tools for problem players/sites

### Low Priority (Future Enhancement)
1. **Special Areas** - Castle quest zone
2. **Advanced Commands** - Alias, trigger, config systems
3. **Performance** - Optimization and profiling
4. **Testing** - Comprehensive test coverage
5. **Documentation** - Complete guides for all user types

## Implementation Recommendations

### Short-term (Next 2-4 weeks)
1. Focus on completing Phase 4 combat mechanics
2. Begin Phase 5 mob AI foundation
3. Add high-priority player commands (time, weather, help)

### Medium-term (1-3 months)
1. Implement history and ignore systems (player QoL)
2. Add social commands system (roleplay support)
3. Complete mob AI and pathfinding
4. Add idle/link-dead management

### Long-term (3-6 months)
1. Casino/games system
2. Special areas (castle)
3. Performance optimization
4. Comprehensive testing suite
5. Complete documentation

## Files Changed

- `docs/roadmap.md` - Updated with all new items and phases

## Impact Assessment

- **Completeness:** Roadmap now covers 100% of identified legacy features
- **Organization:** Better phase grouping and logical progression
- **Clarity:** New sections clearly marked as PLANNED with legacy source references
- **Maintainability:** Easy to track which legacy modules map to which C# implementations

## Next Steps

1. ✅ Review and approve roadmap additions
2. ❌ Prioritize Phase 6.16 player commands (quick wins)
3. ❌ Begin Phase 5.1 mob AI foundation
4. ❌ Plan Phase 8 performance work (benchmarking first)
5. ❌ Start Phase 10 documentation (player guide)
