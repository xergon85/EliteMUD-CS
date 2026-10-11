# One damage path for all combat (#42)

Handover and plan, written Oct 12, 2026 at the end of the levelling session. Start here in a new session, then
read card #42 and #40 on GitHub. See Progress below for what is done.

## How we work (short version)

The details are in the project memory and AGENTS.md; the essentials:

- **Board:** every piece of work is a card (GitHub Projects). Move #42 to In progress when starting. Side findings
  become new cards, not drive-by fixes.
- **Keep the user in the loop:** show the plan before coding, stop at real decision points (use a question with a
  recommendation), short progress updates.
- **No Claude attribution** in commits, PRs, issues or replies (no `Co-Authored-By`, no "Generated with").
- **Never rewrite pushed history:** new commits, and merge `main` into a branch instead of rebasing.
- **Reviews:** Copilot reviews each PR. For each comment, say whether it's valid and why, fix or explain, reply on
  GitHub and resolve the thread.
- **Legacy source:** `~/Dev/elitemud-index` (UTF-8 copy with cscope), e.g. `cd ~/Dev/elitemud-index && cscope -d -L -1 damage`.

## Why this card exists

Every damage source carries its own copy of legacy `damage()` (fight.c) and `die()`. A skill executor therefore
needs `SkillRegistry`, `CombatCalculator`, `IWorldState` and `CombatExperience`; the user flagged that backstab
"knows way too much" and asked for this to be done carefully. The copies have drifted:

- Players killed by kick/bash/backstab or a spell are never handled as dead: no corpse, respawn or penalty (#40).
- The first hit of `kill` (`MeleeAttackExecutor`) has no death handling at all. A killed player is only noticed by
  the tick (which stops the attacker); a killed mob stays in the room at ≤0 hp, because the tick skips mobs with
  `HitPoints <= 0`.
- Dodge/parry only apply where `CombatCalculator.ApplyDamage(PlayerState, …)` is used, i.e. player victims.
- Alignment messages differ (tick: suffix on the kill line; skills: separate line at |shift| ≥ 50; spells: none).

Legacy shape: a skill decides hit or miss and how much, then calls `damage(ch, victim, dam, type)`, which owns
everything after: fighting state, damage XP, position, death (kill XP, alignment, `die()` → penalty, corpse, extract).

## Inventory (main at eb80342)

| Source | File | Damage applied | Death handling |
|---|---|---|---|
| Kick | `Application/Skills/KickSkillExecutor.cs` | player: `ApplyDamage` :106; mob: `HitPoints -=` :130 | mob: alignment :143, `Killed` :158, corpse/remove :159; player: none |
| Bash | `Application/Skills/BashSkillExecutor.cs` | player :125 (and `ApplyDamage(…, 0)` on miss :100); mob :145 | mob :161–178; player: none |
| Backstab | `Application/Skills/BackstabSkillExecutor.cs` | player :189; mob :213 | mob :226–243; player: none |
| `kill` first hit | `Application/Skills/MeleeAttackExecutor.cs` | player via `PerformAttack`; mob :178 | none |
| Damage spells | `Server/Adapters/Commands/Cast/CastCommandHandler.cs` | player :387 (no dodge); mob :403 | mob :413–416; player: none |
| Tick: mob → player | `Server/GameTickService.cs` `ProcessMobAttackOnPlayerAsync` :265 | :322 | `CheckPlayerPositionAsync` :371 → `HandlePlayerDeathFromMob` :693 |
| Tick: player → player | `ProcessPlayerVsPlayerAttack` :374 | `PerformAttack` + weapon effects :426 | `HandlePlayerDeath` :642 |
| Tick: player → mob | `ProcessPlayerVsMobAttack` :497 | :546 | `HandleMobDeath` :849 |
| Tick: mob counterattack | same method | :592 | `CheckPlayerPositionAsync` :638 |

Every site also calls `CombatExperience.DamageDealt` next to the damage, and `Killed`/`Died` at death (#24).
Line numbers drift; search for `HitPoints -= ` and `ApplyDamage(` to re-check.

## Proposed design

1. **`CombatDamage.Apply(attacker, victim, amount, attackType)`** in Application, returning a `DamageOutcome`
   (actual damage after dodge/parry, victim died, messages per audience). It owns:
   - start fighting both ways (players by connection id, mobs by negative instance id, as today)
   - dodge/parry
   - `HitPoints -=`, position update
   - `CombatExperience.DamageDealt`
   - on death: `Killed` (before the penalty), alignment and its message, then
     - mob: corpse and removal
     - player: `Died` penalty, corpse, respawn at the temple (`MortalStartRoom` 3001), "You are dead!" messages
2. **`DamageRoll`** helper for "wielded weapon + effective strength → damage" and weapon effects. It's the only
   reason skills read `IWorldState`.
3. Executors keep only their own rules (hit/damage formulas, wait state, position effects) and call the two above.
   Spells, `kill` and the tick do the same.
4. Message sending stays where it is (executors return `SkillMessage`s, the tick and `cast` send directly), so the
   outcome must carry messages in a form both can use.

## Steps (one source at a time, tests first)

0. **Characterization tests** for each source as it is today: messages and their order, XP, alignment, corpse.
   They pin behavior before anything moves. Mark the known bugs (#40, `kill` first hit) as the cases that change.
1. Add `CombatDamage` with mob death only; switch **kick**; tests green.
2. Switch **bash** and **backstab**.
3. Switch **damage spells**.
4. Add **player death** to `CombatDamage` (from `HandlePlayerDeath*`); now kick/bash/backstab/spells handle player
   victims → closes #40.
5. Switch **`kill` first hit** (fixes both of its death bugs).
6. Switch the **tick** paths (mob → player, player → player, player → mob, counterattack); remove the
   `HandleMobDeath` / `HandlePlayerDeath*` copies.
7. **`DamageRoll`**; executors drop `IWorldState` and `CombatExperience`.

Each step is its own commit, ideally its own PR if it gets large.

## Watch out for

- **Message wording and order** are legacy act messages; keep them. Kill XP lines come after "is DEAD!!".
- **Wimpy/auto-flee** lives in `CheckPlayerPositionAsync` (tick). Keep it working when the tick switches over.
- **`Killed` before `Died`:** the killer's share comes from the victim's XP before the penalty.
- **Mob memory / helpers:** `RememberPlayer` on hit (`MOB_MEMORY`) and `MobAiService.ProcessAssist` on combat start
  happen in some copies only; decide where they belong.
- **Fighting ids:** `FightingConnectionId` is a connection id for players and `-InstanceId` for mobs.
- **`ApplyDamage(PlayerState, …)`** includes dodge/parry and position; mobs only get `HitPoints -=`.

## Decisions (Oct 12, 2026)

1. **Dodge/parry: melee only, as in legacy.** Legacy has them in `hit()` (fight.c:1523–1540), not `damage()`, and
   checks the victim's skill, so mobs with the skill dodge too. Skills and spells can't be dodged. They stay in
   `ApplyDamage` for player victims until the tick switches (step 6), then move to the melee hit.
2. **No alignment message**, as in legacy (`change_alignment` is silent). Drop "You feel more virtuous/sinister"
   and "Your soul darkens from the murder." Rounding of the shift is card #47.
3. **Legacy kill lines only:** room "$n is dead!  R.I.P.", victim "You are dead!  Sorry...". Drop "You have slain X!".

Side findings: #47 (alignment shift rounds toward zero), #48 (`kill` on a mob never rolls to hit).

## Progress

- PR 1: steps 0–2 (characterization tests, `CombatDamage` with mob death, kick/bash/backstab).

## Other open cards (for orientation)

- Levelling left: #25 race starting points and title at creation (`CharacterStart` is the one place to change),
  #27 practices (with the per-level gain noted on the card).
- #46 case-insensitive account and character names (then `AdminPolicy` can ignore case again).
- #33 multi-attack activation formulas, #34 zone mobs lose attacks/hitroll, #35 rules reload, #37 value types,
  #28 content editor, #30 live world reload, #31 spells as composed effects.
- Admin commands need `elitemud.config.json` (gitignored) listing the account names exactly.
