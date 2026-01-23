# EliteMUD Races Reference

Comprehensive documentation of all playable races in EliteMUD, extracted from legacy codebase.

**Source Files:**
- `/Users/christofferisenberg/Dev/elitemud/src/constants.c` (lines 115-180)
- `/Users/christofferisenberg/Dev/elitemud/src/structs.h` (lines 285-312)
- `/Users/christofferisenberg/Dev/elitemud/src/act.wizard.c` (lines 2200-2320)

---

## Race List (25 Total)

| ID | Name       | Title Display | Title Color |
|----|------------|---------------|-------------|
| 0  | god        | Divine        | #r (red)    |
| 1  | human      | Human         | #w (white)  |
| 2  | elf        | Elven         | #b (blue)   |
| 3  | half-elf   | Half-elven    | #c (cyan)   |
| 4  | dwarf      | Dwarven       | #R (bold red) |
| 5  | gnome      | Gnome         | #g (green)  |
| 6  | halfling   | Halfling      | #Y (bold yellow) |
| 7  | half-troll | Half-troll    | #y (yellow) |
| 8  | half-orc   | Half-orc      | #M (bold magenta) |
| 9  | half-ogre  | Half-ogre     | #m (magenta) |
| 10 | duck       | Duck          | #Y (bold yellow) |
| 11 | fairy      | Fairy         | #R (bold red) |
| 12 | minotaur   | Minotaur      | #R (bold red) |
| 13 | ratman     | Ratman        | #M (bold magenta) |
| 14 | drow       | Drow          | #R (bold red) |
| 15 | lizard     | Lizard        | #G (bold green) |
| 16 | vampire    | Vampire       | #m (magenta) |
| 17 | troll      | Troll         | #y (yellow) |
| 18 | draconian  | Draconian     | #G (bold green) |
| 19 | avatar     | Avatar        | #r (red)    |
| 20 | werewolf   | Werewolf      | #m (magenta) |
| 21 | demon      | Demon         | #w (white)  |
| 22 | dragon     | Dragon        | #G (bold green) |
| 23 | feline     | Feline        | #G (bold green) |
| 24 | angel      | Angel         | #w (white)  |

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
