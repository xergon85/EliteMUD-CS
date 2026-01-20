# Legacy EliteMUD Character Creation Flow

## Character Creation States (from structs.h:469-490)

The legacy system uses the following connection states:

1. `CON_NME` (1) - "By what name..?" - Name entry
2. `CON_NMECNF` (2) - "Did I get that right, x?" - Name confirmation
3. `CON_PWDGET` (4) - "Give me a password for x" - Password entry (new account)
4. `CON_PWDCNF` (5) - "Please retype password:" - Password confirmation
5. `CON_QSEX` (6) - "Sex?" - Gender selection
6. `CON_QRACE` (18) - "Race?" - Race selection
7. `CON_QCLASS` (10) - "Class?" - Class selection
8. `CON_RMOTD` (7) - "PRESS RETURN after MOTD" - Show MOTD
9. `CON_SLCT` (8) - "Your choice: (main menu)" - Main menu
10. `CON_PLYNG` (0) - Playing - Enter game

## Flow Sequence

```
1. Enter Name
2. Confirm Name
3. If NEW player:
   a. Enter Password
   b. Confirm Password
   c. Select Sex (M/F)
   d. Select Race (a-m, 13 races)
   e. Select Class (dynamic based on race)
   f. init_char() - Initialize character with base stats
   g. save_char() - Save to disk
   h. Show MOTD
   i. Main Menu
   j. Enter game

4. If EXISTING player:
   a. Enter Password
   b. Show MOTD
   c. Main Menu
   d. Load character from disk
   e. Enter game
```

## Race Selection (13 playable races)

From `display_races()` in interpreter.c:

```
[a] Human       [b] Troll       [c] Halfling
[d] Dwarf       [e] Gnome       [f] Elf
[g] Half-elf    [h] Fairy       [i] Minotaur
[j] Ratman      [k] Drow        [l] Lizardman
[m] Draconian
```

**Features:**
- User can enter capital letter (A-M) to get help/info about that race
- After race selection, display classes available for that race

## Class Selection (20+ classes)

From `display_classes()` in interpreter.c:

**Base Classes (always shown if race allows):**
- [a] Magic-user
- [b] Cleric
- [c] Thief
- [d] Warrior

**Advanced Classes (race-dependent):**
- [e] Psionicist
- [f] Monk
- [g] Bard
- [h] Knight
- [i] Wizard
- [j] Druid
- [k] Assassin
- [l] Ranger
- [m] Illusionist
- [n] Paladin
- [o] Mariner
- [p] Cavalier
- [s] Ninja

**Multi-class options (dual-class):**
- [u] Warrior/Thief
- [v] Warrior/Cleric
- [w] Warrior/Magic-user
- [x] Thief/Cleric
- [y] Thief/Magic-user
- [z] Cleric/Magic-user

**Multi-class options (triple-class):**
- [1] Warrior/Thief/Magic-user
- [2] Warrior/Cleric/Magic-user

**Features:**
- User can enter `-` to go back to race selection
- User can enter `*` to redisplay class list
- User can enter capital letter to get help about that class
- Classes shown are filtered by race (using `allowed_classes[]` bitmap)

## Sex Selection

From `CON_QSEX` case in interpreter.c:

- [M/m] Male
- [F/f] Female

No other options (no "Neutral" for players, only for MOBs)

## Initial Stats (from init_char() in db.c)

After class selection, `init_char()` is called:

```c
GET_STR(ch) = 11;
GET_INT(ch) = 11;
GET_WIS(ch) = 11;
GET_DEX(ch) = 11;
GET_CON(ch) = 11;
GET_CHA(ch) = 11;
```

**All stats start at 11 (base)**

Additional initialization:
- `hometown` = random(1-4)
- `height` = race_average ± variation based on sex
- `weight` = race_average ± variation based on sex
- `hit/mana/move` = calculated from MAX formulas (race/class dependent)
- `armor` = 100
- All skills = 0 (except implementors = 100)
- Conditions (hunger/thirst/drunk) = 24 (or -1 for immortals)

**Note:** The legacy system does NOT implement stat rolling or point-buy. All characters start with identical base stats (11 in all attributes). Race/class modifiers may apply elsewhere but are not in `init_char()`.

## Save Process

After character creation:
1. `create_entry(GET_NAME(d->character))` - Add to player index
2. `save_char(d->character, NOWHERE, 2)` - Save character file
3. Log: "[name] [host] new player."

## Multi-Character Support

**The legacy system does NOT support multiple characters per account.**
- One player name = one character
- No account system
- Player file is named after character name
- If player name exists, it loads that character (with password check)

## For C# Implementation

To adapt this to multi-character:

1. **Account Creation:**
   - Name → Username (account name)
   - Password → Account password
   - Create account in DB

2. **Character Selection Menu (NEW):**
   ```
   Your characters:
   1) [Character Name] (Level X [Class])
   2) [Character Name] (Level X [Class])
   3) Create new character
   4) Delete a character
   
   Choice:
   ```

3. **Character Creation Flow:**
   - If "Create new character" selected:
     - Enter character name
     - Confirm character name
     - Select sex
     - Select race
     - Select class
     - Roll/assign stats (optional - can keep base 11)
     - Save character to DB with AccountId link
   
4. **Loading Existing Character:**
   - Load character from DB by CharacterId
   - Restore inventory/equipment from DB
   - Place in saved room (or start room if first login)

## Key Differences for Multi-Character

- **Username vs Character Name:** Username is for account, character name is for character
- **Character List:** Need to query DB for all characters belonging to account
- **Character Limit:** Enforce max characters per account (e.g., 5-10)
- **Character Deletion:** Allow soft-delete with confirmation
- **Stat Persistence:** Save all character stats to DB, reload on character selection
