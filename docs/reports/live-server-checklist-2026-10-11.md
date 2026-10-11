# Live Server Checklist

_For checking on `telnet elitemud.se 4500` as a mortal. Created Oct 11, 2026; revised the same day after the code
scan explained most portals._

**Why:** some objects in our data are never created by anything we can find. The live game shows whether they exist.
See `unsourced-objects-2026-10-11.md` for the full list.

**How to use:**
- Directions start at the **Temple of Midgaard** (where you recall to). They were computed from our data, so the live
  game may differ slightly, and doors on the way may be closed.
- `14n` means "north 14 times".
- Write what you find under **Answer**. "Not there" is a useful answer too.

---

## 1. Sanity check: the Midgaard jeweler

**Where:** *Jeweler's*, `s w s` from the Temple.
**Look for:** *the Jeweler* standing in the shop.
**Why:** our importer used to lose this spawn's room. The fix should put him here.

- **Answer:**
- The jeweler is there

## 2. Samurai Temple gear

**Where:** *Entrance to Temple*, `3s 3w 4s e 6s e 5s 2e 10s e s` from the Temple of Midgaard (via the Japanese Island).
**Look for:** the equipment on the Samurai Trainees, Apprentices and Warriors (`look <mob>`), and what they drop.
Specifically check for *the Kusari*, *a samurai sword*, *a samurai dagger*, *a samurai mace*, *a samurai shield* or
*a samurai cloak*.
**Why:** no zone reset, special procedure or saved file creates these. The legacy zone only loads the samurai vest
and bracer. If the rest shows up on the live server, it runs code or data we don't have.

- **Answer:**
 I was not able to find those, I could find these
  -  the Kodachi
  - a samurai vest
  - a samurai bracer
  - the Laroche
  - and some keys from the shoguns, but I was not able to get to the top, so I cant say what's on the other floors
## 3. Locked doors with no key

Nothing in the data or code creates these keys. Two quick checks:

| Door | Directions | Look for |
|---|---|---|
| Troll Caves: *NE Quadrant of a Guard Barracks*, door north | `s 3e s 4e d 3n e 2s 2e 2s w n 3e n e 2n 2e n e n e` | Is the door locked? Does a guard or the Queen carry *guard's key* / *An iron key*? |
| *Outside Redferne's residence*, door north | `3s 3w 3s e 4s e 5u` | Is it a player house? Locked? |

- **Answer:**
- Troll caves: Too dangerous for me, so i cant say at this moment
- Residence: It seems like a normal zone, the gate was locked, but i could bash it in. Seems like an empty zone though

## 4. Things you might just know

No need to visit; answer from memory if you can.

- **Player houses:** are *Papi's Cozy Cottage*, *Quifael's hall* and *Redferne's Residence* immortal or player homes
  whose keys were given out by hand? *BIGBIRD'S CHAMBERS* (Clan Zone, opened by *a purple rose*) too?
  - **Answer:**
  Probably very old school players or immortals, I dont know of these
- **The Amazeing Maze** (zone 154): the legacy zone never loads its mobs or keys and has no walking route in. Was it
  ever open to players?
  - **Answer:**
   never heard of it

---

## Conclusions (Oct 11, 2026)

- **Jeweler:** present live, matching our fixed import. ✅
- **Samurai Temple:** the live mobs carry *the Kodachi* (Trainees), *the Laroche* (Apprentices, Warriors), *a samurai
  vest* and *a samurai bracer*, exactly as our zone resets load them. *The Kusari* and the samurai sword, dagger, mace,
  shield and cloak weren't found, and nothing in legacy loads them. **Conclusion: unused content.** The shogun keys on
  the upper floors weren't checked.
- **Redferne's Residence:** a normal zone whose gate is locked but bashable, and it's empty. That matches the legacy zone
  file, where the chest, rings and key loads are commented out. **Conclusion: unfinished zone, keys 7900/7901 unused.**
- **Troll Caves keys:** not checked (too dangerous). Legacy loads the Queen and guards but gives them no keys.
- **Papi's Cozy Cottage, Quifael's hall, BIGBIRD'S CHAMBERS:** unknown; probably very old player or immortal homes.
  Keys 3300, 3301, 1007 treated as personal items.
- **The Amazeing Maze:** never heard of. The legacy zone loads no mobs or keys. **Conclusion: unfinished zone.**

---

## Answered from data (no need to check)

The code scan found that zone files can load portals with an `L` reset command (and set their key with `A`). That
explains these:

| Portal | Lies in | Directions |
|---|---|---|
| *the arena entrance* (needs key 12033) | *Colosseum Entrance - Patrons* | `14n 15w 2n e` |
| *Circus Maximus entrance* (needs *an circus ticket*) | *Circus Maximus - Entrance* | `14n 15w 3n w` |
| *the theater entrance* and *the cheap seats* (need theater tickets) | *Theater Entrance* | `14n 8w 2s 6w` |
| *a book of nursery rhymes*, *a book of fables* | *Nursery Rhymes, Fables, and Other Children's Books* | `s 2w n u 2n w` |
| *a history book about the Kai clan* | *Rare Books and Historical Volumes* | `s 2w n u 2n e` |
| *Half house on the edge of the chasm* (Iron Mill entrance) | Dwarven Kingdom, room 6442/6443 area | |
| Air Pumps, furnace, PK zone portal, Ultima moongates | their own zones | |

If you happen to pass by any of them, confirming they're there is still a nice sanity check, but it isn't needed.
