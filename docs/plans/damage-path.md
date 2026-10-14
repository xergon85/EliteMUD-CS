# One damage path for all combat (#42)

Handover, updated Oct 14, 2026 after steps 0–5, the two prerequisite cards (#61, #62) and step 6a. Start here in a
new session, then read card #42 on GitHub (its comments hold the step 6 pipeline plan). Next up: **step 6b, the
combat tick onto the shared path**, on a branch stacked on `feat/damage-path-tick` (PR 6a).

## How we work (short version)

The details are in the project memory and AGENTS.md; the essentials:

- **Board:** every piece of work is a card (GitHub Projects). #42 is In progress. Side findings become new cards,
  not drive-by fixes.
- **Keep the user in the loop:** show the plan before coding, stop at real decision points (a question with a
  recommendation), short progress updates.
- **No Claude attribution** in commits, PRs, issues or replies (no `Co-Authored-By`, no "Generated with").
- **Never rewrite pushed history:** new commits, and merge `main` into a branch instead of rebasing.
- **Before opening a PR:** tests green, a mutation check on each new test (break the code, see the test fail),
  the server starts, and a `/code-review` run on the branch (findings fixed or explained in the PR).
- **Reviews:** Copilot is out of AI credits for the rest of October; CodeRabbit runs on a 14-day trial (ends about
  Oct 28; `.coderabbit.yaml` is on main). It re-reviews on every push, so when asked to check feedback, fetch every
  reviewer's *latest* round: review bodies (outside-diff findings), threads, and PR comments. For each finding, say
  whether it's valid and why, fix or explain, reply on GitHub, resolve the thread. The long-term choice is #68.
- **Legacy source:** `~/Dev/elitemud-index` (UTF-8 copy with cscope), e.g. `cd ~/Dev/elitemud-index && cscope -d -L -1 damage`.

## Why this card exists

Every damage source carried its own copy of legacy `damage()` (fight.c) and `die()`, and the copies had drifted:
players killed by skills or spells never died properly (#40), `kill`'s first hit had no death handling, dodge only
applied to player victims, and each source had its own alignment and death messages. Legacy shape: a source decides
hit or miss and how much, then calls `damage(ch, victim, dam, type)`, which owns everything after.

## Where it stands

`CombatDamage.Apply(attacker, victim, amount)` (Application/Combat) is the shared `damage()`. Kick, bash, backstab,
damage spells and `kill`'s first hit use it; the attacker can be a player or a mob. It runs a fixed list of
`IDamageStep`s (Combat/DamageSteps) over a `DamageContext`, in `CombatDamage.Steps`; a step can stop the hit, and
steps don't call each other. In order:

- `IgnoreCorpse`: a victim already dead is left alone (fight.c:787)
- `StartFighting` both ways (`CombatCalculator.SetFighting`; `ICombatant.Fighting` is a reference to the
  opponent, #62)
- `NotifyHurt`: `ICombatListener`s (`Hurt`, `Killed`); `MobMemory` (Application/Ai) remembers attackers and forgets
  the dead
- `CapDamage` (0–500), `LoseHitPoints` (with damage XP), `UpdatePosition`
- on death, `Kill` (fight ends, alignment silently, kill XP, death lines; nothing when you kill yourself) then
  `PlayerDeath` (corpse, penalty, respawn at `CharacterStart.StartRoom`, saves on both sides) or `MobDeath` (corpse
  and removal; the room comes from the player in the fight, as nothing deals damage between mobs yet)

Melee hits are resolved first with `CombatCalculator.ResolveAttack` / `DefendAgainstMelee` (legacy `hit()`: roll,
damage, position multiplier, dodge, parry), then dealt through `CombatDamage`. Messages are `SkillMessage`s (act
templates), sent by `SkillMessageSender` through `ActMessageExtensions`.

Around it: game state is changed one thing at a time under `GameStateGate` (#61, see AGENTS.md); output is queued
per session; fights end when a player flees, quits or disconnects; quit refuses during a fight; characters enter
the game standing.

## Step 6: the combat tick

**6a (done, PR 6a):** `CombatTickTests` pin what one round of the tick does today, path by path, with notes where
it differs from legacy; `CombatDamage` takes a mob attacker; the pipeline split with `DamageStepTests`.

**6b (next):** position messages, wimpy (decision 9) and stop fighting below stunned as steps; the four tick paths
through `DefendAgainstMelee` + `CombatDamage`; no counterattack (decision 10); removals. `CombatTickTests`'
expected lines change on purpose as each part lands.

What's left in `src/EliteMud.Server/GameTickService.cs` (1140 lines; line numbers drift):

| Path | Damage | Death |
|---|---|---|
| mob → player, `ProcessMobAttackOnPlayerAsync` :260 | `ApplyDamage` :317 | `CheckPlayerPositionAsync` :734 → `HandlePlayerDeathFromMob` :685 |
| player → player, `ProcessPlayerVsPlayerAttack` :369 | `PerformAttack` :403 + weapon effects `HitPoints -=` :421 | `HandlePlayerDeath` :634 ("You have slain", "Your soul darkens") |
| player → mob, `ProcessPlayerVsMobAttack` :492 | `HitPoints -=` :538, own `RememberPlayer` :544 | `HandleMobDeath` :840 ("You have slain … You feel more virtuous") |
| mob counterattack, same method | `ApplyDamage` :584 | `CheckPlayerPositionAsync` :630 |

Plan (details in the #42 comments):

1. **Mob attackers:** `CombatDamage.Apply` takes a `PlayerState` attacker today. Generalize to `ICombatant`
   (kill XP for a mob killer goes through `CombatExperience.Killed`, which already handles mobs).
2. **Pipeline:** split `CombatDamage` into an ordered list of small steps (`IDamageStep` over a `DamageContext`;
   corpse check, start fight, listeners, cap, hit points, position, position messages, wimpy, kill, death). One
   explicit list; each step tested alone plus one test pinning the order. `DamagePathTests` and `CastDamageTests`
   must pass unchanged through the split. No event bus, no priorities, steps don't call each other.
3. **Position messages and wimpy** move from `CheckPlayerPositionAsync` into steps, so every source gets them
   (legacy `damage()` fight.c:987–1003; today only the tick sends them).
4. **Melee for all four tick paths:** `ResolveAttack` / `DefendAgainstMelee`, then `CombatDamage`. Mobs get dodge
   and parry in melee when they have the skill (decision 1).
5. **Remove** `HandleMobDeath`, `HandlePlayerDeath`, `HandlePlayerDeathFromMob`, the tick's `RememberPlayer`, and
   the old lines ("You have slain …", "Your soul darkens …", "You feel more virtuous/sinister").
6. **Tests:** the tick needs connections; use `TestConnection` (loopback telnet session) and fixed dice
   (`CombatCalculator` takes an optional `Random`). Characterize each tick path first, as in step 0.

Then **step 7, `DamageRoll`**: one "wielded weapon + effective strength (+ weapon effects) → damage" helper for
backstab, `kill` and the tick; executors drop `IWorldState`.

## Watch out for

- **Message wording and order** are legacy act messages; keep them. Kill XP lines come after "is DEAD!!".
- **Wimpy/auto-flee** lives in `CheckPlayerPositionAsync` today; keep it working when it moves.
- **`Killed` before `Died`:** the killer's share comes from the victim's XP before the penalty.
- **The gate:** the tick pass already holds `GameStateGate`; never wait for a save or the gate inside it.
- **`PlayerState.Id` is the connection id** (#66): anything stored by player id is forgotten on reconnect.
- **Helpers:** `MobAiService.ProcessAssist` runs after the hit (legacy `autoassist` in `perform_violence`).

## Decisions

Oct 12:
1. **Dodge/parry: melee only, as in legacy** (fight.c:1523–1540, in `hit()` not `damage()`), and mobs with the
   skill dodge too. Skills and spells can't be dodged.
2. **No alignment message** (legacy `change_alignment` is silent). Rounding of the shift is #47.
3. **Legacy kill lines only:** room "$n is dead!  R.I.P.", victim "You are dead!  Sorry...". No "You have slain X!".

Oct 13–14:
4. **Mob memory is a listener**, not part of `CombatDamage`: `ICombatListener` / `MobMemory`.
5. **Serialize game state with one gate plus queued output** (#61, option C).
6. **The fight target is a direct reference** (#62).
7. **`StopFighting` stands up anyone conscious** (legacy `stop_fighting`), but never the stunned or dying.
8. **Quit refuses while you have an opponent**, also when knocked down (stricter than legacy's position check).

Oct 14 (step 6; options compared in the "Combat Tick Options" page, claude.ai/artifact/7E4SAZZ2r3vSz54eFhdqrq):
9. **Wimpy flees inside the hit.** The wimpy step flees right away through `FleeHandler` and adds its lines plus an
   ordered "show the room" marker to the outcome's messages; `SkillMessageSender` shows the look at the marker. Every
   source gets wimpy in the right order with no executor changes. Wimpy also gets legacy's "panics" room line.
10. **No mob counterattack.** The tick's counterattack after each player attack (level + 1d4) goes; a mob hits once a
    round on its own turn, as in legacy `perform_violence`. Mob multi-attack stays with #33.
11. **Every melee victim goes through `DefendAgainstMelee`; to-hit rolls stay with #48.** One call for players and mobs,
    as in legacy `hit()`, instead of a player-only branch that #48 would remove again. Step 7 rewrites the damage
    roll, not the defense, so this isn't redone. **Note:** in play this changes almost nothing yet. Mobs have no
    skills (`MobInstance.GetSkill` always returns 0; #72), so no mob dodges or parries
    until that's done; the only visible change is the position multiplier on a mob that's sitting, sleeping or
    stunned. Players still never miss mobs and mobs never miss players until #48.
12. **At least two PRs:** 6a restructures with no behavior change (tick tests, `ICombatant` attacker, pipeline split);
    6b changes behavior (position messages, wimpy, stop fighting below stunned, the tick moved over, removals).

## Done

- #50: steps 0–2 (characterization tests, `CombatDamage` with mob death, kick, bash, backstab).
- #52: step 3 (damage spells), `SkillMessageSender` on `ActMessageExtensions`, `TestConnection`.
- #54: step 4 (player death; closes #40), `CharacterStart.StartRoom`.
- #60: step 5 (`kill`'s first hit), `ResolveAttack`, `ICombatListener` / `MobMemory`, dead-victim guard.
- #63: game state gate and queued telnet output (closes #61).
- #65: fight target as a reference (closes #62), fights end on leaving, quit refuses mid-fight, enter standing.

## Related cards

- Combat behavior: #47 alignment rounding, #48 no to-hit roll between players and mobs, #72 mobs have no skills,
  #49 bash doesn't knock a player down, #51 player victims miss skill victim lines, #55 player killing rules, #70 flee
  escape skill and death traps.
- Architecture: #59 affects design, #31 spells as composed effects, #71 skills as composed building blocks (design
  #31 and #71 together, after this card), #53 one broadcast helper.
- Server: #64 shutdown notices, #66 `PlayerState.Id` is the connection id, #69 quit and link loss as legacy.
- Tooling: #68 how PRs get reviewed after the CodeRabbit trial.
- Earlier: #25 race starting points, #27 practices, #46 case-insensitive names, #33/#34 multi-attack and zone mob
  stats, #35 rules reload, #37 value types, #28 content editor, #30 live world reload.
- Admin commands need `elitemud.config.json` (gitignored) listing the account names exactly.
