# Objects Nothing in the World Creates

_Date: Oct 11, 2026 · Data: `content/objects/objects.json` and `zones/*.json` on branch `fix/legacy-import`_

**Question:** which objects can players never get, because nothing in the world creates them? This matters for the
planned reachability check (see `docs/plans/levelling-and-classes.md`): these objects must either be explained and
marked as intentional, or fixed.

**Method:** an object counts as created if any of these produce it:
- a zone reset (`O`, `G`, `E`, `P`)
- a shop inventory (`lib/world/shp/*.shp`)
- an `oload`/`mpoload` in a room, mob or quest program

Not covered:
- **special procedures in the C code**
- **saved room contents** (see "Not checked yet")
- **immortals loading items by hand**

## Update: code scan and live check (Oct 11, 2026)

A scan of the legacy C code and saved files, plus a check on the live server (`live-server-checklist-2026-10-11.md`),
explained 93 of the 462. **369 remain without any source.**

| Source | Explains | Notes |
|---|---|---|
| `L` portal reset command | 22 portals | Zone files place portals with `L <if> <room> <obj> <dest>` and set the key with `A`. Covers Rome's entrances, the library books, the Iron Mill's way in, the PK zone portal and the Ultima moongates. **Our server ignores `L` and `A`, so none of these portals exist in the rewrite yet.** |
| Quest shop (`questeq_shopkeeper`, spec_procs.c:1171, mob 3098) | 47 | Sells every object with the Quest flag and a price > 0 (built at boot by `boot_questeq`, db.c:650). That's 47, not the 53 estimated below: 99013, 99016, 99017, 99098 cost 0, 99099 is a portal, 99064 is sold. |
| Clan shop (`claneq_shopkeeper`, spec_procs.c:1317, mob 3097) | 24 | Exchanges clan gear for an item handed in; pairs come from `E` lines in `world/cln/*.cln`. Five clan items have exchange -1 and are never offered. |

**Settled by the live check:**
- **Samurai gear:** *the Kusari* and the samurai sword, dagger, mace, shield and cloak are unused content. The live mobs carry only what our zone resets load.
- **Redferne's Residence and the Amazeing Maze:** unfinished zones. Their keys (7900, 7901, 15400–15410) are unused.
- **Keys 3300, 3301, 1007:** personal items for old player or immortal homes.

**Not found anywhere:**
- **Random-drop mechanism:** none exists in the legacy C code.
- **Quest file loader:** none exists either.
- **Saved room contents** (`lib/roomobjs`): only one object, 99001. The files are plain text, not binary.

**Importer and server bugs found along the way** (all fixed on `fix/legacy-import`):
1. ✅ **`L`/`A` portal resets** weren't supported by the server (22 portals missing). Fixed in `a399f1e`, which also adds `enter` and `activate`.
2. ✅ **`R` reset was mis-mapped:** it means "shuffle a room's exits" in legacy, but was treated as "remove object" (89 commands, mostly mazes). Fixed in `2e1cf32`.
3. ✅ **Room 28703 was missing:** "The Guild Donation Room" was lost to a glued terminator (`0 -1 28701S` in 287.wld). Fixed in `1228c92`.
4. ✅ **Door type 2 was mis-imported:** it means "pick-proof door" in legacy, but was imported as raw flag bits (174 exits). Fixed in `1228c92`.

## Summary

- **No objects are missing from the import.** All 2,827 legacy objects are in `content/objects/objects.json`, which the
  server loads. (This corrects Finding 3 of `legacy-import-audit-2026-10-10.md`.)
- **462 objects are never created by the world.** Most of that is expected: EliteMUD had no quest system, and immortals
  handed out many items by hand.

| Group | Files | Count | Explanation | Status |
|---|---|---|---|---|
| Clan items | `2.obj`, `170.obj`, clan zones `275`–`289` | 90 | Clan gear, exchanged by an immortal | ✅ Intentional |
| Quest-point shop | `990.obj` (not `<OLD>`) | 53 | Bought with quest points earned in immortal-run quests | ✅ Intentional |
| Retired quest items | `990.obj` (`<OLD>`) | 16 | Old versions, several with a current replacement (99004 → 99055 *the Wyrmslayer*) | ✅ Intentional |
| Personal / immortal items | `1.obj`, `126.obj`, `0.obj` | 52 | E.g. *The Crown of the Elite Queen*, *IO's notebook*, *dr/hr ring +20+20* test rings | ✅ Intentional (to confirm) |
| Orphaned Ultima copy | `24.obj` | 69 | Duplicate of the Ultima items in `26.obj` (2600–2619 ↔ 2400–2419, same names and order). No `24.wld`/`.zon`/`.mob` exists, and nothing references it. The real set is fine: Ultima mobs carry the mantra keys and moonstones via zone resets, and they open the doors in "Ultima – upper levels". | Candidate for removal |
| Door keys | `154.obj`, `236.obj`, `79.obj`, `33.obj`, `10.obj` | 18 | Keys that doors require but nothing creates | ⚠️ **Needs a look** |
| Portals | various | 28 | Entrances such as *Circus Maximus entrance*, *the arena entrance*, *A PK zone entry portal*, *moongate*, library books | ⚠️ **Needs a look** |
| Samurai gear | `130.obj` | 6+ | *the Kusari*, *a samurai sword/dagger/mace/shield/cloak*. Possibly the "samurai tower" items with random loads. | ⚠️ **Needs a look** |
| Other | many files, 1–27 each | rest | E.g. *letter "a"*–*"z"* (`11.obj`), scrolls and talismans in `19.obj` | Unknown |

## Needs a look

**Door keys (18).** If nothing creates the key, the door can't be opened by players:

| Keys | Zone file | Note |
|---|---|---|
| 15400–15410 *a cast iron key* (11) | `154.obj` | |
| 23603 *An iron key*, 23604 *guard's key* | `236.obj` | |
| 7900 *a large key*, 7901 *a copper key* | `79.obj` | |
| 3300 *a metal key*, 3301 *a brass key* | `33.obj` | |
| 1007 *a purple rose* | `10.obj` | Clan Zone doors |

**Portals (28).** If a portal is a zone's only way in, that zone is unreachable. Several are in Rome (`120.obj`: arena,
Circus Maximus, theater, cheap seats) and the library (`29.obj` books). They may have been placed by immortals and
kept as saved room contents, or loaded by special procedures.

**Samurai gear (`130.obj`).** Possibly loaded at random on specific mobs. That would be a special procedure or a
mechanism we haven't found yet.

## Not checked yet

- **Saved room contents.** The original game saves the objects lying in "crash rooms" (rooms marked `C`) to
  `lib/roomobjs/room<vnum>.objs`. There are 14 such files, all clan halls (rooms 275xx–289xx), in a binary format
  (`Crash_load_rooms` in `objsave.c`). The importer doesn't read them. Clan items and portals may be in there.
- **Special procedures.** `spec_assign.c` / `spec_procs.c` can create objects in C code (e.g. random loads). Not scanned.
- **Quest files.** `lib/world/qst/*.qst` exist, but the legacy C source in this repo has no loader for them. Quests were
  run by immortals.

## Full list

All 462 objects, grouped by legacy file. `[door key]` marks keys that a door requires.

```

== 24.obj (69)
    2400  Mu  (Key)
    2401  Lum  (Key)
    2402  Beh  (Key)
    2403  Ohm  (Key)
    2404  Ahm  (Key)
    2405  Cah  (Key)
    2406  Ra  (Key)
    2407  Summ  (Key)
    2408  Moonstone  (Key)
    2409  Moonstone  (Key)
    2410  Moonstone  (Key)
    2411  Moonstone  (Key)
    2412  Moonstone  (Key)
    2413  Moonstone  (Key)
    2414  Moonstone  (Key)
    2415  Moonstone  (Key)
    2416  the Hourglass  (Key)
    2417  the shepherd's crook  (Weapon)
    2418  the doll  (Trash)
    2419  Katrina's sword  (Weapon)
    2420  a wrinkled scroll  (Scroll)
    2421  a magic dagger  (Weapon)
    2422  a robe  (Armor)
    2423  horseshoes  (Armor)
    2424  a mandolin  (Wand)
    2425  a cup  (Container)
    2426  some gold pieces  (Money)
    2427  the bottle  (Potion)
    2428  Iolo's bow  (Weapon)
    2429  a chicken  (Weapon)
    2430  the Infinity Bow  (Weapon)
    2431  a wooden spear  (Weapon)
    2432  a stone club  (Weapon)
    2433  a gavel  (Weapon)
    2434  a staff  (Weapon)
    2435  a sword  (Weapon)
    2436  a sword  (Weapon)
    2437  a bastard sword  (Weapon)
    2438  a hammer  (Weapon)
    2439  a sword  (Weapon)
    2440  the Amulet of Love  (Armor)
    2441  a shiny sword  (Weapon)
    2442  shiny armour  (Armor)
    2443  a rapier  (Weapon)
    2444  a boomerang  (Weapon)
    2445  a rusty sword  (Weapon)
    2446  a rusty shield  (Armor)
    2447  a magic cloak  (Armor)
    2448  an aura of power  (Armor)
    2449  a big red face  (Armor)
    2450  a spiked mace  (Weapon)
    2451  a completely black armour  (Armor)
    2452  an evil charm  (Armor)
    2453  an evil gem  (Staff)
    2454  Sentri's helmet  (Armor)
    2455  Sentri's sleeves  (Armor)
    2456  Sentri's girth  (Armor)
    2457  Sentri's two-handed sword  (Weapon)
    2458  Sentri's gloves  (Armor)
    2459  Sentri's boots  (Armor)
    2460  Sentri's leggings  (Armor)
    2461  Sentri's armour  (Armor)
    2462  a Fellowship medallion  (Armor)
    2463  The Glass Sword  (Weapon)
    2464  British's crown  (Armor)
    2465  British's sceptre  (Weapon)
    2466  British's amulet  (Armor)
    2467  a fountain  (RawFood)
    2468  a boat  (Fountain)

== 990.obj (68)
   99000  A long curved fang <OLD>  (Weapon)
   99001  a diamond tablet  (Scroll)
   99002  A pair of rollerblades  (Armor)
   99003  Magical Rune Tattoos <OLD>  (Armor)
   99004  the wyrmslayer <OLD>  (Weapon)
   99005  a bag of holding  (Container)
   99006  the heartseeker <OLD>  (Weapon)
   99007  a small hexagonal necklace <OLD>  (Armor)
   99008  a Sha-Gren skin <OLD>  (Armor)
   99009  a Mukluk  (Armor)
   99010  Aura of Alcmene <OLD>  (Light)
   99011  a rabbit slayer <OLD>  (Weapon)
   99012  the crystal shield <OLD>  (Armor)
   99013  an adamant hammer  (Weapon)
   99014  a diamond helmet <OLD>  (Armor)
   99015  a pair of real wings <OLD>  (Armor)
   99016  a ring of invisibility  (Worn)
   99017  an amulet  (Armor)
   99018  an morning star  (Weapon)
   99019  Rigel's slayer <OLD>  (Weapon)
   99020  Rigel's crystal breastplate. <OLD>  (Armor)
   99021  Rigel's impressive crystal armplates. <OLD>  (Armor)
   99022  Rigel's mindblowing legplates <OLD>  (Armor)
   99023  Rigel's weird shoes <OLD>  (Armor)
   99024  a green Light Sabre  (Light)
   99025  a blue Light Sabre  (Light)
   99026  a red Light Sabre  (Light)
   99027  A golden pool cue  (Weapon)
   99028  an Aura of Alcmene  (Light)
   99029  the Mirror of Amaterasu  (Light)
   99030  a ring of mind shielding  (Armor)
   99031  a glittering jade ring  (Armor)
   99032  an iridium ring  (Armor)
   99033  a small hexagonal necklace  (Armor)
   99034  a flashy green emerald cape  (Armor)
   99035  an inix shell armour  (Armor)
   99036  Rigel's crystal breastplate  (Armor)
   99037  a diamond helmet  (Armor)
   99038  the Crystal Visor  (Armor)
   99039  Rigel's mindblowing crystal legplates  (Armor)
   99040  a pair of silver knight spurs  (Armor)
   99041  Rigel's wierd shoes  (Armor)
   99042  a minstrel's gloves  (Armor)
   99043  gauntlets of authority  (Armor)
   99044  a pair of real wings  (Armor)
   99045  Magical Rune tattoos  (Armor)
   99046  Rigel's impressive crystal armplates  (Armor)
   99047  the Crystal shield  (Armor)
   99048  a Sha-Gren skin  (Armor)
   99049  a smooth black leather cape  (Armor)
   99050  Jia-Sha  (Armor)
   99051  a mithril battle girth  (Armor)
   99052  a demonic tattoo  (Armor)
   99053  a pair of jade bracers  (Armor)
   99054  the Soul-Reaver  (Weapon)
   99055  the Wyrmslayer  (Weapon)
   99056  the Sunblade  (Weapon)
   99057  a rabbit slayer  (Weapon)
   99058  a long curved fang  (Weapon)
   99059  the Heartseeker  (Weapon)
   99060  a chessboard  (Armor)
   99061  a poisoned dagger  (Weapon)
   99062  a girdle of power  (Armor)
   99063  an iron barbed flail  (Weapon)
   99064  a teddybear called Qualen  (Armor)
   99097  an Oedit token  (Other)
   99098  EliteMUD's PK Championship Crown  (Armor)
   99099  #Mportal#N  (Portal)

== 170.obj (31)
   17051  Muse's Robe of Office  (Armor)
   17052  a Breastplate of Mordred  (Armor)
   17053  an empathic shield  (Worn)
   17054  Selene's Diamond Necklace  (Worn)
   17055  Johan's Smurfy Hat  (Worn)
   17056  Shylanne's Black Silk Stockings  (Worn)
   17057  Shylanne's Necklace of Darkness  (Worn)
   17058  Ember's Tooth of Wisdom  (Light)
   17059  Ulath's Ogre Horn Helmet  (Worn)
   17060  Sorsha's Arm Plates  (Worn)
   17061  Quischo's webbed feet  (Worn)
   17062  Quischo's orange beak  (Worn)
   17063  Ix's Duckie Pillow  (Weapon)
   17064  a Flaming Energizer Bunny  (Light)
   17065  Vulcan's Ring of Fire  (Worn)
   17066  Ring of Death  (Worn)
   17067  a play script  (Note)
   17068  Fangorn's Sword of Ultimate Death and Destruction  (Weapon)
   17069  Nymph's Angelic Halo  (Light)
   17070  a clue  (Other)
   17071  an ego  (Trash)
   17072  an excuse  (Trash)
   17073  a hall pass  (Note)
   17075  a nuclear weapon  (Staff)
   17076  a 12 gauge shotgun  (Weapon)
   17080  Anubis's Ultra-Black cloak  (Armor)
   17081  Anubis's Kill-O-Zap Gun  (Weapon)
   17082  Radagast's Staff  (Weapon)
   17083  Tweed's Battle Shorts  (Armor)
   17084  Tweed's stamp collection  (Treasure)
   17099  the Guide to Newbie Helping  (Note)

== 11.obj (27)
    1100  letter "a"  (Other)
    1101  letter "b"  (Other)
    1102  letter "c"  (Other)
    1103  letter "d"  (Other)
    1104  letter "e"  (Other)
    1105  letter "f"  (Other)
    1106  letter "g"  (Other)
    1107  letter "h"  (Other)
    1108  letter "i"  (Other)
    1109  letter "j"  (Other)
    1110  letter "k"  (Other)
    1111  letter "l"  (Other)
    1112  letter "m"  (Other)
    1113  letter "n"  (Other)
    1114  letter "o"  (Other)
    1115  letter "p"  (Other)
    1116  letter "q"  (Other)
    1117  letter "r"  (Other)
    1118  letter "s"  (Other)
    1119  letter "t"  (Other)
    1120  letter "u"  (Other)
    1121  letter "v"  (Other)
    1122  letter "w"  (Other)
    1123  letter "x"  (Other)
    1124  letter "y"  (Other)
    1125  letter "z"  (Other)
    1126  letter "blank"  (Other)

== 2.obj (27)
     205  #NA dark winged dragon suit of the tempest #w[#RSD#w]#N  (Armor)
     206  #NRing of Maelstroms #w[#RSD#w]#N  (Armor)
     207  #Nthe Stormbringer #w[#RSD#w]#N  (Weapon)
     210  #bElysium #NClan Ring of the #wSword#N  (Armor)
     211  #bElysium#N Clan Ring of the #bShield#N  (Armor)
     212  #bElysium #NClan Ring of the #rHeart#N  (Armor)
     215  #NA Finely Tempered Dagger of #yNight Tribe#N  (Weapon)
     216  #NA Glittering Chain Halberk of #yNight Tribe#N  (Armor)
     217  #NA Large Black Cloak of #yNight Tribe#N  (Armor)
     220  #NShield of the rose #w[#bKai#w]#N  (Armor)
     221  #NTunic of Light #w[#bKai#w]#N  (Armor)
     222  #NCrown of Alannon #w[#bKai#w]#N  (Armor)
     225  #NThe golden amulet of the #bArtemisians#N  (Armor)
     226  #NThe Double-Headed Axe of the #bArtemesian#N Amazons  (Weapon)
     227  #NThe Dagger of the #bArtemisian#N High Priestess  (Weapon)
     235  #MMALINA#N clan symbol  (Light)
     236  #MMALINA's#N clan belt  (Container)
     237  #MMALINA's#N clan amulet  (Armor)
     245  #NA Diamond Dagger of The #:r#bE#yl#bi#yt#be#N#:N  (Weapon)
     246  #NThe Metallic Cloak of the #:r#bE#yl#bi#yt#be#N#:N  (Container)
     247  #NThe Platinum Platemail of The #:r#bE#yl#bi#yt#be#N#:N  (Armor)
     250  #Na strange shinto ring #w[#ySHOGUN#w]#N  (Armor)
     251  #Na tattered cloth bushido belt #w[#ySHOGUN#w]#N  (Armor)
     252  #Na golden metal shuriken #w[#ySHOGUN#w]#N  (Weapon)
     255  #NAmulet of the #BT#YR#MI#Nad  (Armor)
     256  #BT#YR#MI#Ndent of Power  (Weapon)
     257  #NHeart of Arakas #w[#BT#YR#MI#w]#N  (Light)

== 1.obj (27)
     102  #RA #rLove #REternal#N  (Worn)
     103  #RA #rLove #REternal#N  (Worn)
     105  Criz's Clarinet  (Wand)
     113  a bottle  (DrinkContainer)
     114  a teddybear called Qualen  (Armor)
     115  a bottle of champagne  (DrinkContainer)
     116  a champagne glass  (DrinkContainer)
     117  The Crown of the Elite Queen  (Armor)
     118  IO's notebook  (Trash)
     124  a mithril wedding ring  (Worn)
     133  A Wedding Invitation  (Note)
     135  Holy Book of Rules  (Trash)
     137  Rahl's Notebook  (Trash)
     138  Israfel's Notebook  (Trash)
     140  North Blocker  (Other)
     141  East Blocker  (Other)
     142  South Blocker  (Other)
     143  West Blocker  (Other)
     144  Up Blocker  (Other)
     145  Down Blocker  (Other)
     146  the Adventurer's Guild board  (Trash)
     147  a long sword  (Weapon)
     150  a magical bag of holding  (Container)
     151  A pair of sunglasses  (Armor)
     152  Nymph's notebook  (Trash)
     153  a Zoom doll (tm)  (Armor)
     157  Amok's Drawing Board  (Trash)

== 19.obj (27)
    1900  a massive slate-grey sledgehammer  (Weapon)
    1901  a violet scroll  (Scroll)
    1902  a black etched tablet  (Scroll)
    1903  a scroll with githyanki writings on it  (Scroll)
    1904  a small demon's amulet  (Wand)
    1905  a devilish talisman  (Staff)
    1906  a silvery knife  (Weapon)
    1907  a silvery dagger  (Weapon)
    1908  an extremely sharp and thin sword  (Weapon)
    1909  a thin two-handed sword  (Weapon)
    1910  a silvery sword  (Weapon)
    1911  an iron barbed flail  (Weapon)
    1912  The unholy sword of the githyanki  (Weapon)
    1913  a brighty flaming stone  (Light)
    1914  some astral powder  (Treasure)
    1915  a splint mail vest  (Armor)
    1916  a splint mail skirt  (Armor)
    1917  a pair of splint mail sleeves  (Armor)
    1918  a pair of ornately designed gauntlets  (Armor)
    1919  a decaying vest made from cracked leather  (Armor)
    1920  a violet potion  (Potion)
    1921  a vial of murky fluid  (Potion)
    1922  a silvery-colored potion  (Potion)
    1923  a tasty-looking chocolate mint  (Portal)
    1924  a black pentagram  (Key)
    1925  a rotting pouch  (Money)
    1926  a rusted sword  (Weapon)

== 126.obj (16)
   12600  A flickering portal  (Portal)
   12619  Gateway  (Portal)
   12686  MR ring +50  (Armor)
   12687  MR ring +80  (Armor)
   12688  ac ring -50  (Armor)
   12689  dr/hr ring +5+5  (Armor)
   12690  dr/hr ring +10+10  (Armor)
   12691  dr/hr ring +20+20  (Armor)
   12692  mr neckware +40  (Armor)
   12693  mr neckware +60  (Armor)
   12694  ac neckware -50  (Armor)
   12695  dr hr neckware +5+5  (Armor)
   12696  dr hr neckware +10+10  (Armor)
   12697  dr hr neckware +20+20  (Armor)
   12698  sanc regen special body armour  (Armor)
   12699  MR ring +30  (Armor)

== 130.obj (13)
   13013  the Kusari  (Weapon)
   13050  a samurai sword  (Weapon)
   13051  a samurai dagger  (Weapon)
   13052  a samurai mace  (Weapon)
   13054  a samurai shield  (Armor)
   13055  a samurai cloak  (Armor)
   13056  a samurai helmet  (Armor)
   13057  a pair of samurai leggings  (Armor)
   13058  a pair of samurai boots  (Armor)
   13059  a pair of samurai gloves  (Armor)
   13060  a pair of samurai sleeves  (Armor)
   13061  a samurai cape  (Armor)
   13062  a samurai belt  (Armor)

== 154.obj (11)
   15400  a cast iron key  (Key) [door key]
   15401  a cast iron key  (Key) [door key]
   15402  a cast iron key  (Key) [door key]
   15403  a cast iron key  (Key) [door key]
   15404  a cast iron key  (Key) [door key]
   15405  a cast iron key  (Key) [door key]
   15406  a cast iron key  (Key) [door key]
   15407  a cast iron key  (Key) [door key]
   15408  a cast iron key  (Key) [door key]
   15409  a cast iron key  (Key) [door key]
   15410  a cast iron key  (Key) [door key]

== 29.obj (9)
    2900  a book  (Portal)
    2901  a library card  (Key)
    2906  a book titled, "Legends of the Were-Folk"  (Portal)
    2907  a School of the Mind poster  (Portal)
    2908  a book about the mythical village of Miqi'erke  (Portal)
    2909  a new arrival  (Portal)
    2910  a book of nursery rhymes  (Portal)
    2911  a history book about the #bKai#N clan  (Portal)
    2912  a book of fables  (Portal)

== 0.obj (9)
       0  a bug  (Trash)
      12  A demon statue  (Other)
      13  a statue  (Other)
      14  A large monolith  (Other)
      18  the book of wolves  (Trash)
      19  A Huge dragons statue  (Other)
      34  an Artemisian clan bulletin board  (Trash)
      37  an invisible barrier  (Other)
      38  a Dragoon of Phos clan bulletin board  (Trash)

== 120.obj (9)
   12003  the glowing eye medallion  (Armor)
   12014  Froboz's robe  (Worn)
   12031  an executioner's mace  (Weapon)
   12032  the arena entrance  (Portal)
   12037  Circus Maximus entrance  (Portal)
   12039  the theater entrance  (Portal)
   12041  the cheap seats  (Portal)
   12104  an aquamarine key  (Key)
   12106  a steel key  (Key)

== 236.obj (7)
   23603  An iron key  (Key) [door key]
   23604  guard's key  (Key) [door key]
   23609  An Ogre helm  (Armor)
   23610  An adorned ring  (Staff)
   23611  An extremely large sack  (Container)
   23612  A corpse  (Trash)
   23613  A brown potion  (Potion)

== 131.obj (7)
   13100  a camouflage pocket  (Container)
   13182  the large fireplace sputtering of burning coal  (Portal)
   13191  Air Pump  (Portal)
   13192  Air Pump  (Portal)
   13193  Air Pump  (Portal)
   13194  Air Pump  (Portal)
   13199  Sic Transit Gloria Mundi  (Weapon)

== 17.obj (6)
    1711  a wicker basket  (Container)
    1713  some grain  (Food)
    1714  a skiff  (Boat)
    1716  a pile of coins  (Money)
    1719  a stone shield  (Armor)
    1723  a potion of life healing  (Potion)

== 79.obj (5)
    7900  a large key  (Key) [door key]
    7901  a copper key  (Key) [door key]
    7909  a huge treasure  (Money)
    7910  a wooden chest  (Container)
    7911  a small brass key  (Key)

== 15.obj (4)
    1515  a pair of gnome boots  (Armor)
    1519  a stone shield  (Armor)
    1522  a banana  (Food)
    1523  a potion of life healing  (Potion)

== 288.obj (4)
   28801  #yT#whe hammer of the #yD#wawn's#N  (Weapon)
   28802  #wThe #CHeir #wof #RRosewood #wStaff#N  (Weapon)
   28803  #wG#Wran Colar Ordem Torre Espada  (Armor)
   28809  the butter knife  (Weapon)

== 66.obj (4)
    6605  a small white bulb of garlic  (Potion)
    6606  a strip of willow bark  (Wand)
    6665  shadow robes  (Armor)
    6668  a good luck charm  (Armor)

== 77.obj (4)
    7700  a Shrubbery  (Treasure)
    7701  the Holy Hand Grenade of Antioch  (Scroll)
    7702  a herring  (Weapon)
    7703  A PK zone entry portal  (Portal)

== 61.obj (4)
    6101  a sign  (Item_0)
    6108  a big pile of gold coins  (Money)
    6156  the morning star  (Weapon)
    6158  Rigel's Key  (Key)

== 69.obj (4)
    6900  Quifael's global optimizer  (Weapon)
    6901  the Olympian blade  (Weapon)
    6950  a coffee machine  (Container)
    6951  a coffee cup  (DrinkContainer)

== 278.obj (4)
   27811  the high altar of the Storm Dragons  (Container)
   27813  the #RRi#rng#N of #RSt#ror#Rms#N  (Armor)
   27814  a #RFla#rsh#N of #RDra#rgon#Rfi#rre#N  (Light)
   27815  the #RSt#rorm#Rbr#rin#Rger#N  (Weapon)

== 283.obj (4)
   28304  a large square of milk chocolate fudge  (Food)
   28305  two lovely meatballs swimming in sauce  (Food)
   28306  a cold glass  (DrinkContainer)
   28309  a tall cold one  (DrinkContainer)

== 12.obj (3)
    1213  the key to the door between Mithrandir and Medea's rooms  (Worn)
    1245  a #wpearl#N wedding ring  (Worn)
    1246  a #wsilver#N wedding ring  (Worn)

== 277.obj (3)
   27703  #Ca Herring#N  (Weapon)
   27704  #Rthe #yFlaming #GShrubbery#N  (Light)
   27705  #Rthe holy #Yhand grenade #Gof Antioch#N  (Staff)

== 275.obj (3)
   27506  a shard of #bblue#N crystal  (Weapon)
   27507  A small midnight-#bblue#N sphere  (Light)
   27508  a #bsapphire#N-colored loincloth  (Armor)

== 99.obj (3)
    9937  a soft cotton blanket for the beach  (Worn)
    9938  a large fluffy beach towel  (Worn)
    9952  #wFurball#N's heavy woolen cape  (Armor)

== 31.obj (3)
    3137  a fountain carved from bleached bone  (Fountain)
    3300  a metal key  (Key) [door key]
    3301  a brass key  (Key) [door key]

== 285.obj (3)
   28500  #eThe #yTribal#N Ring#e of Valor#N  (Armor)
   28501  #eA #yTribal#e Tattoo#N  (Armor)
   28502  #eAn Eternal #NFlame#e of #yNight#N  (Light)

== 280.obj (3)
   28015  the #MDragoon#W's #CSaddlebag#N  (Container)
   28018  #Na #yShining #MSwordfish #Wof #CPhos#N  (Weapon)
   28019  a #yGlowing #MHeart #wof #CPhos#N  (Armor)

== 10.obj (2)
    1000  a pair of wings  (Armor)
    1007  a purple rose  (Armor) [door key]

== 64.obj (2)
    6442  Half house on the edge of the chasm  (Portal)
    6443  Half house on the edge of the chasm  (Portal)

== 67.obj (2)
    6759  Flyn's #Ygold #rw#ye#gd#cd#bi#mn#rg#N ring  (Armor)
    6760  Jellie's #wshining #ygold#N wedding ring  (Armor)

== 160.obj (2)
   16009  a cabinet  (Container)
   16020  a bottle of cranberry juice  (DrinkContainer)

== 150.obj (2)
   15000  Sapowox's Universal Nothingness  (Weapon)
   15027  a small brass box  (Container)

== 143.obj (2)
   14309  a stone tablet  (Note)
   14310  a stone tablet  (Note)

== 237.obj (2)
   23705  a small spade  (Weapon)
   23710  some hard leather sandals  (Worn)

== 26.obj (2)
    2668  a boat  (Boat)
    2699  #Mmoongate#N  (Portal)

== 279.obj (2)
   27904  an #RA#Ym#Rb#Ye#Rr#N jewelled ring  (Armor)
   27905  a tiny #wfairy#N trapped in #RA#Ym#Rb#Ye#Rr#N  (Armor)

== 251.obj (2)
   25101  a cannibal's axe  (Weapon)
   25102  odd looking spear  (Weapon)

== 132.obj (2)
   13203  The stairway to 'Upstairs'  (Portal)
   13224  the minotaur twohanded axe  (Weapon)

== 30.obj (2)
    3033  a box  (Container)
    3055  a staff of healing  (Staff)

== 281.obj (2)
   28105  #CN#ce#wcklace of #CD#ce#wfiance#N  (Armor)
   28106  the Spirit of the #CK#cu#wlt#N  (Light)

== 282.obj (2)
   28200  a #yKnight's #bSaddlebag#N  (Container)
   28201  the #wSpirit#N of #bKai#N  (Weapon)

== 128.obj (1)
   12825  portal  (Portal)

== 289.obj (1)
   28900  a fountain  (Fountain)

== 71.obj (1)
    7191  a golden ring  (Trash)

== 172.obj (1)
   17261  some huge spiked brass knuckles  (Weapon)

== 72.obj (1)
    7207  a pair of muddy gauntlets  (Armor)

== 147.obj (1)
   14768  small alcove  (Portal)

== 57.obj (1)
    5794  the dungeon key  (Key)

== 235.obj (1)
   23500  a null pointer  (Trash)

== 42.obj (1)
    4214  a jar  (Container)

== 55.obj (1)
    5540  a black drow longbow <item withdrawn>  (Weapon)

== 140.obj (1)
   14014  an amulet of true armor  (Armor)

== 287.obj (1)
   28718  the MO's board  (Trash)

== 34.obj (1)
    3413  a girdle of power  (Armor)
```
