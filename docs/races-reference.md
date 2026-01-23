# EliteMUD Races Reference

Comprehensive documentation of all playable races in EliteMUD, extracted from legacy codebase.

**Source Files:**
- `/Users/christofferisenberg/Dev/elitemud/src/constants.c` (lines 115-180) - Race tables and stat maxes
- `/Users/christofferisenberg/Dev/elitemud/src/structs.h` (lines 285-312) - Race constants
- `/Users/christofferisenberg/Dev/elitemud/src/act.wizard.c` (lines 2200-2320) - Racial abilities
- `/Users/christofferisenberg/Dev/elitemud/src/interpreter.c` (lines 1364-1368, 1891-1905) - Character creation races

---

## Race List (25 Total)

### Races Available at Character Creation (13 Races)

These races can be selected during character creation via the race selection menu:

| ID | Name       | Menu | Title Display | Title Color |
|----|------------|------|---------------|-------------|
| 1  | human      | [a]  | Human         | #w (white)  |
| 17 | troll      | [b]  | Troll         | #y (yellow) |
| 6  | halfling   | [c]  | Halfling      | #Y (bold yellow) |
| 4  | dwarf      | [d]  | Dwarven       | #R (bold red) |
| 5  | gnome      | [e]  | Gnome         | #g (green)  |
| 2  | elf        | [f]  | Elven         | #b (blue)   |
| 3  | half-elf   | [g]  | Half-elven    | #c (cyan)   |
| 11 | fairy      | [h]  | Fairy         | #R (bold red) |
| 12 | minotaur   | [i]  | Minotaur      | #R (bold red) |
| 13 | ratman     | [j]  | Ratman        | #M (bold magenta) |
| 14 | drow       | [k]  | Drow          | #R (bold red) |
| 15 | lizard     | [l]  | Lizard        | #G (bold green) |
| 18 | draconian  | [m]  | Draconian     | #G (bold green) |

**Source:** `interpreter.c` lines 1364-1368, 1891-1905

### Special Races (12 Races - Not Available at Creation)

These races cannot be selected at character creation and are typically granted by immortals or through special game mechanics:

| ID | Name       | Title Display | Title Color | Notes |
|----|------------|---------------|-------------|-------|
| 0  | god        | Divine        | #r (red)    | Admin only |
| 7  | half-troll | Half-troll    | #y (yellow) | Special unlock |
| 8  | half-orc   | Half-orc      | #M (bold magenta) | Special unlock |
| 9  | half-ogre  | Half-ogre     | #m (magenta) | Special unlock |
| 10 | duck       | Duck          | #Y (bold yellow) | Special unlock |
| 16 | vampire    | Vampire       | #m (magenta) | Special unlock |
| 19 | avatar     | Avatar        | #r (red)    | Special unlock |
| 20 | werewolf   | Werewolf      | #m (magenta) | Special unlock |
| 21 | demon      | Demon         | #w (white)  | Special unlock |
| 22 | dragon     | Dragon        | #G (bold green) | Special unlock |
| 23 | feline     | Feline        | #G (bold green) | Special unlock |
| 24 | angel      | Angel         | #w (white)  | Special unlock |

**How to Obtain:**
- Immortal command: `set <player> race <racename>` (act.wizard.c:4257)
- Quest rewards (specifics not documented in base code)
- Special game events (specifics not documented in base code)

**Note:** In legacy EliteMUD, these special races were often granted as rewards for difficult quests, remort milestones, or special achievements. The exact unlock conditions may have been server-specific.

---

## Stat Maximums by Race

| Race       | STR | CON | DEX | INT | WIS | CHA |
|------------|-----|-----|-----|-----|-----|-----|
| god        | 25  | 25  | 25  | 25  | 25  | 25  |
| human      | 18  | 18  | 18  | 18  | 18  | 18  |
| elf        | 18  | 17  | 19  | 18  | 18  | 18  |
| half-elf   | 18  | 18  | 18  | 18  | 18  | 18  |
| dwarf      | 20  | 20  | 16  | 16  | 18  | 18  |
| gnome      | 16  | 18  | 20  | 20  | 18  | 18  |
| halfling   | 16  | 18  | 20  | 18  | 18  | 18  |
| half-troll | 20  | 20  | 16  | 14  | 14  | 12  |
| half-orc   | 19  | 18  | 16  | 16  | 16  | 16  |
| half-ogre  | 18  | 18  | 18  | 18  | 18  | 18  |
| duck       | 16  | 18  | 20  | 16  | 16  | 16  |
| fairy      | 15  | 16  | 20  | 20  | 18  | 18  |
| minotaur   | 20  | 20  | 18  | 16  | 16  | 16  |
| ratman     | 16  | 18  | 20  | 16  | 18  | 16  |
| drow       | 18  | 18  | 18  | 18  | 18  | 16  |
| lizard     | 20  | 18  | 18  | 16  | 18  | 16  |
| vampire    | 21  | 21  | 21  | 21  | 21  | 21  |
| troll      | 20  | 20  | 16  | 9   | 9   | 12  |
| draconian  | 20  | 19  | 18  | 16  | 15  | 16  |
| avatar     | 23  | 23  | 23  | 23  | 23  | 23  |
| werewolf   | 20  | 20  | 19  | 17  | 16  | 16  |
| demon      | 24  | 24  | 24  | 24  | 24  | 24  |
| dragon     | 21  | 21  | 14  | 20  | 20  | 14  |
| feline     | 18  | 19  | 24  | 16  | 18  | 19  |
| angel      | 20  | 20  | 20  | 20  | 20  | 20  |

---

## Innate Racial Abilities

### Infravision (AFF_INFRARED)
**Can see in the dark**

Races with innate infravision:
- Elf
- Half-elf
- Drow
- Ratman
- Draconian
- Vampire
- Troll
- Werewolf
- Dragon
- Angel

### Regeneration (AFF_REGENERATION)
**Faster HP recovery**

Races with innate regeneration:
- Troll
- Werewolf

### Cat Eyes / Light (AFF_LIGHT)
**Enhanced vision in darkness**

Races with cat eyes:
- Dwarf
- Gnome
- Demon

### Sanctuary (AFF_SANCTUARY)
**Damage reduction**

Races with innate sanctuary:
- Fairy

### Detect Alignment (AFF_DETECT_ALIGN)
**Can see good/evil auras**

Races with detect alignment:
- Demon
- Angel

### Armor Bonus (APPLY_AC)
**Natural armor class bonus**

Races with armor bonus:
- Dragon (-20 AC innate)

### Bless Bonus (APPLY_HITROLL, APPLY_SAVING_MAGIC)
**Combat and saving throw bonuses**

Races with bless bonus:
- Avatar (+2 hitroll, +5 saving magic)

### Skills

**Dragon:**
- Tail Lash (75% innate)

---

## Starting Stats

All characters start with base stats of 11 in all attributes:
- STR: 11
- INT: 11
- WIS: 11
- DEX: 11
- CON: 11
- CHA: 11

The stat maximums (shown above) determine how high each stat can be raised through training or magic.

---

## Height and Weight

Each race has average height and weight values (gender-adjusted):

**Males:**
- Weight: average ± 20 lbs
- Height: average ± 30 inches

**Females:**
- Weight: average - 30 to average + 10 lbs
- Height: average - 40 to average + 20 inches

*(Exact values would need to be extracted from `height_average[]` and `weight_average[]` arrays)*

---

## Remort System

### Remort Benefits

**Remort 4+:**
- Characters with 4 or more remorts automatically receive **maximum stats for their race**
- Stats are set to race maximums (from the stat caps table above)
- Warriors, Cavaliers, Knights, and Rangers also get STR/ADD = 100
- Hunger and thirst can be toggled off

**Source:** `act.wizard.c` lines 2129-2140

**Example:**
- Human with 4 remorts: All stats set to 18 (human max)
- Troll with 4 remorts: STR=20, CON=20, DEX=16, INT=9, WIS=9, CHA=12
- Avatar with 4 remorts: All stats set to 23 (avatar max)

### Remort and Race Changes

Remorts do NOT automatically unlock special races. Race changes are handled separately by:
1. Immortal commands (`set <player> race <racename>`)
2. Quest rewards or special events
3. Server-specific unlock mechanics

---

## Implementation Notes

### Affect System
Racial abilities are implemented as permanent affects (DURATION_INNATE) added to the character during initialization or respec.

### Code Location
Character initialization applies racial abilities in `do_respec_mob()` at line ~2200 in `act.wizard.c`.

### Display in Commands
- `stat` command shows racial abilities as "Magic: (innate) ability_name"
- Innate abilities persist through death and resurrection
- Racial abilities cannot be dispelled

---

## Future Considerations

1. **Racial Stat Modifiers at Creation**: Legacy appears to use flat 11 base, but may apply modifiers during character rolling
2. **Race-Class Restrictions**: May exist but not documented here
3. **Racial Experience Penalties**: Some D&D-based MUDs apply XP penalties to powerful races
4. **Racial Languages**: Not yet researched
5. **Racial Size Categories**: May affect combat, equipment, etc.

---

**Research Date:** January 23, 2026
**Legacy Source:** /Users/christofferisenberg/Dev/elitemud/
