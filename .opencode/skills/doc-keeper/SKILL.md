---
name: doc-keeper
description: Automatically update roadmap.md after commits to reflect project progress
license: MIT
compatibility: opencode
metadata:
  trigger: after-commit
  files: docs/roadmap.md
  audience: developers
---

## What I do

After every git commit, I:
1. Analyze the commit diff to understand what changed
2. Update `docs/roadmap.md` to reflect completed work
3. Move items from "TODO" to "COMPLETE" status with dates
4. Add new known issues or bugs discovered during the commit
5. Update phase completion percentages
6. Keep the roadmap synchronized with actual code changes

## When to use me

**Automatically triggered after:**
- Any `git commit` operation
- Changes that affect project milestones or phases
- Bug fixes that resolve tracked issues
- New features that complete roadmap items

**I should NOT run when:**
- Commits are only documentation updates
- Changes are trivial (typos, formatting)
- User explicitly says "skip doc update"

## How I work

### 1. Analyze the Commit
- Read the commit message and diff
- Identify which roadmap phases/sections are affected
- Determine if any tracked items are now complete
- Check for new bugs or issues mentioned in commit

### 2. Update Roadmap Sections

**For completed items:**
```markdown
- ✅ Item description - COMPLETE (Jan 25, 2026)
```

**For new issues:**
```markdown
- ❌ **Issue description** - NOT FIXED
  - Impact: What breaks
  - Root cause: Why it happens
  - Expected: What should happen
  - Fix needed: How to fix it
```

**For phase completion:**
Update phase headers from "IN PROGRESS" to "COMPLETE" when all items done.

### 3. Key Patterns to Match

**Status Markers:**
- `✅` = Complete
- `❌` = Not started / Known bug
- `🔄` = In progress
- `⚠️` = Warning/caution

**Dates:**
Always use format: `(Month DD, YYYY)` e.g., `(Jan 25, 2026)`

**Section Headers:**
- Phase N: COMPLETE
- Phase N: IN PROGRESS  
- Phase N: PLANNED

### 4. Preserve Roadmap Structure

**DO NOT:**
- Reorder existing sections
- Remove historical completion notes
- Change date formats
- Modify phase numbering

**DO:**
- Keep consistent formatting
- Add dates to newly completed items
- Update "Current Status" if major milestone reached
- Add items to "Recently Completed" section for visibility

## Example Updates

### Example 1: Feature completion
**Commit:** "Add recall command for low-level players"

**Roadmap update:**
```markdown
### Phase 6: World Systems
- ✅ `recall` command - teleport to temple (level 10 and below) - COMPLETE (Jan 25, 2026)
```

### Example 2: Bug fix
**Commit:** "Fix: Players can get stuck sitting while fighting"

**Roadmap update:**
```markdown
### Position/State Bugs
- ✅ **Players can get stuck sitting/sleeping while fighting** - FIXED (Jan 25, 2026)
  - Impact: Player ended up sitting while fighting
  - Fix: Auto-stand victims when taking damage
```

### Example 3: New known issue
**Commit:** "Document player spawn bug"

**Roadmap update:**
```markdown
### Persistence Bugs
- ❌ **Player spawns in "The Void" (room 0) after death** - CRITICAL
  - Impact: Players spawn wrong location
  - Root cause: Death handling doesn't set respawn room
  - Expected: Should respawn at Temple (room 3001)
  - Fix needed: Update death handler
```

## Important Notes

- Always preserve existing content structure
- Use exact date format shown in examples
- Match emoji style (✅ ❌ 🔄 ⏳)
- Keep line lengths reasonable (wrap at ~120 chars)
- Maintain chronological order in "Recently Completed" section
- Cross-reference commit hashes when adding completed items
- Update both the specific phase AND the "Recently Completed" section

## Questions to Ask

Before updating, consider:
1. Does this commit truly complete a roadmap item?
2. Should this go in "Recently Completed" for visibility?
3. Are there related items that should also be updated?
4. Does this change phase status (PLANNED → IN PROGRESS → COMPLETE)?
5. Should any new TODO items be added based on this commit?

## Permissions

This skill requires:
- Read access to git history (`git log`, `git diff`)
- Read access to `docs/roadmap.md`
- Write access to `docs/roadmap.md`
- No other file modifications
