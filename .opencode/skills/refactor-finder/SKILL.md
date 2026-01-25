---
name: refactor-finder
description: Identify refactoring opportunities in C# code after commits
license: MIT
compatibility: opencode
metadata:
  trigger: after-commit
  language: csharp
  audience: developers
  related-skills: pragmatic-clean-architecture
---

## What I do

After every git commit, I analyze the changed files to identify refactoring opportunities:

1. **Architecture violations** - Clean architecture layer dependencies, wrong project references
2. **Code complexity** - Large methods/classes, high cyclomatic complexity, deep nesting
3. **Code duplication** - Repeated patterns, similar logic that could be extracted
4. **Naming issues** - Poor naming conventions, inconsistent terminology, unclear intent
5. **Error handling gaps** - Missing validation, unhandled edge cases, swallowed exceptions

I provide specific, actionable suggestions with:
- Location (file:line)
- Severity (Critical, High, Medium, Low)
- Description of the issue
- Recommended refactoring approach
- Example code (when helpful)

## When to use me

**Automatically triggered after:**
- Any `git commit` operation with C# file changes
- Commits that modify existing code (not just additions)
- Changes to core business logic or infrastructure

**I should NOT run when:**
- Commits only touch test files
- Changes are pure documentation/comments
- User explicitly says "skip refactor check"
- Commit is a revert or merge

## Detection Patterns

### 1. Architecture Violations

**Clean Architecture Layer Rules (from pragmatic-clean-architecture skill):**
- `Domain` → Cannot reference Application, Infrastructure, or Presentation
- `Application` → Can reference Domain, but not Infrastructure or Presentation
- `Infrastructure` → Can reference Domain and Application
- `Presentation/Server` → Can reference Application and Domain

**What I detect:**
```csharp
// ❌ BAD: Domain referencing Infrastructure
namespace EliteMud.Game // Domain layer
{
    using EliteMud.Data; // Infrastructure layer - VIOLATION!
}

// ❌ BAD: Application referencing Presentation
namespace EliteMud.Application
{
    using EliteMud.Server; // Presentation layer - VIOLATION!
}
```

**Severity:** Critical - Breaks dependency inversion

**Recommendation:**
- Extract interface to Domain layer
- Move implementation to Infrastructure
- Use dependency injection at composition root

---

### 2. Size & Complexity Issues

**What I detect:**

**A. Large Methods (>50 lines)**
```csharp
// ❌ BAD: 100+ line method
public async Task HandleAsync(...)
{
    // Validation logic (20 lines)
    // Business rules (30 lines)
    // Database operations (25 lines)
    // Messaging (15 lines)
    // Response building (10 lines)
}
```

**Severity:** High

**Recommendation:**
- Extract validation into separate method
- Extract database operations into repository
- Use CQRS command/query handlers for separate concerns
- Consider vertical slice architecture

**B. Large Classes (>500 lines)**

**Severity:** Medium-High (depends on cohesion)

**Recommendation:**
- Split by responsibility (SRP)
- Extract inner classes or helpers
- Consider feature-based vertical slices

**C. Deep Nesting (>3 levels)**
```csharp
// ❌ BAD: Deep nesting
if (condition1)
{
    if (condition2)
    {
        if (condition3)
        {
            if (condition4)
            {
                // Business logic buried here
            }
        }
    }
}
```

**Severity:** Medium

**Recommendation:**
- Use guard clauses (early returns)
- Extract nested logic into methods
- Use LINQ where appropriate

---

### 3. Code Duplication

**What I detect:**

**A. Repeated Validation Logic**
```csharp
// ❌ BAD: Validation duplicated across handlers
public class CreateUserHandler
{
    public async Task Handle(...)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Name required");
        if (request.Name.Length > 50)
            throw new ValidationException("Name too long");
    }
}

public class UpdateUserHandler
{
    public async Task Handle(...)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Name required");
        if (request.Name.Length > 50)
            throw new ValidationException("Name too long");
    }
}
```

**Severity:** Medium

**Recommendation:**
- Extract to shared validator class
- Use FluentValidation library
- Create domain value objects (e.g., `UserName` record)

**B. Similar Command/Query Handlers**

**Severity:** Medium

**Recommendation:**
- Extract common logic into base class or helper
- Use generic handlers for CRUD operations
- Consider repository patterns for data access

**C. Repeated String Constants**
```csharp
// ❌ BAD: Magic strings duplicated
await context.Session.SendLineAsync("You can't do that!", cancellationToken);
// ... elsewhere ...
await context.Session.SendLineAsync("You can't do that!", cancellationToken);
```

**Severity:** Low

**Recommendation:**
- Extract to constants class
- Use resource files for user messages
- Consider message catalog pattern

---

### 4. Naming Issues

**What I detect:**

**A. Inconsistent Terminology**
```csharp
// ❌ BAD: Mixed terminology
public class PlayerState { }
public class CharacterDefinition { }  // Should be PlayerDefinition?
public class UserService { }          // Should be PlayerService?
```

**Severity:** Low-Medium

**Recommendation:**
- Align on domain terminology (use ubiquitous language)
- Rename for consistency across codebase
- Update related documentation

**B. Unclear Intent**
```csharp
// ❌ BAD: Vague names
public void Process(Data d) { }
public class Manager { }
public class Helper { }
```

**Severity:** Medium

**Recommendation:**
- Use descriptive names that reveal intent
- Avoid generic suffixes (-Manager, -Helper, -Processor)
- Follow C# naming conventions (PascalCase for types/methods)

**C. Abbreviations & Acronyms**
```csharp
// ❌ BAD: Unclear abbreviations
int hp, mp, mv;  // What are these?
```

**Severity:** Low

**Recommendation:**
- Spell out in code, abbreviate in comments
- Use properties with descriptive names
- Keep legacy compatibility in serialization only

---

### 5. Error Handling Gaps

**What I detect:**

**A. Missing Null Checks**
```csharp
// ❌ BAD: No validation
public void Attack(ICombatant target)
{
    target.TakeDamage(10); // What if target is null?
}
```

**Severity:** High

**Recommendation:**
- Add guard clauses
- Use nullable reference types (`ICombatant?`)
- Validate at boundaries (controllers, handlers)

**B. Swallowed Exceptions**
```csharp
// ❌ BAD: Exception ignored
try
{
    await SavePlayerAsync(player);
}
catch (Exception)
{
    // Silent failure
}
```

**Severity:** Critical

**Recommendation:**
- Log exceptions with context
- Fail fast for critical errors
- Provide user feedback for recoverable errors

**C. Missing Edge Case Handling**
```csharp
// ❌ BAD: No boundary checks
public void SetLevel(byte level)
{
    player.Level = level; // What if level > 100?
}
```

**Severity:** Medium

**Recommendation:**
- Validate input ranges
- Define domain rules in entity
- Use value objects with invariants

---

## EliteMUD-CS Specific Patterns

### Project Structure (from AGENTS.md)
```
src/
  EliteMud.Game/        - Domain layer (models, enums, core logic)
  EliteMud.Application/ - Use cases, commands, queries
  EliteMud.Data/        - SQLite persistence contracts
  EliteMud.Scripting/   - Lua integration (MoonSharp)
  EliteMud.Legacy/      - Legacy content loaders
  EliteMud.Server/      - Telnet server, adapters
tests/
  EliteMud.Tests/       - xUnit tests
```

### Common Violations to Check

**1. Server Layer Depending on Data Layer**
```csharp
// ❌ BAD: Server → Data direct dependency
using EliteMud.Data; // in EliteMud.Server
```

**Fix:** Server should only depend on Application contracts

**2. Game Layer with Infrastructure Concerns**
```csharp
// ❌ BAD: Domain with database code
namespace EliteMud.Game
{
    public class PlayerState
    {
        public void SaveToDatabase() { } // Infrastructure concern!
    }
}
```

**Fix:** Move persistence to EliteMud.Data, use repository pattern

**3. Application Layer with Presentation Details**
```csharp
// ❌ BAD: Application with Telnet specifics
public class CommandHandler
{
    public async Task Handle(TelnetSession session) { } // Presentation leak!
}
```

**Fix:** Use abstraction (ISession, ConnectionContext)

---

## Output Format

When I find issues, I report them like this:

```markdown
## Refactoring Opportunities Found

### Critical Issues (Fix Immediately)
1. **Architecture Violation** - `src/EliteMud.Game/PlayerState.cs:45`
   - Domain layer referencing Infrastructure (EliteMud.Data)
   - Extract IPlayerRepository to Domain, implement in Data layer

2. **Swallowed Exception** - `src/EliteMud.Server/TelnetServer.cs:123`
   - Exception caught but not logged or handled
   - Add logging with context, consider fail-fast behavior

### High Priority
3. **Large Method** - `src/EliteMud.Application/Commands/CreateCharacterHandler.cs:30`
   - HandleAsync is 127 lines (>50 line threshold)
   - Extract: ValidateCharacterName(), AssignStartingStats(), SaveNewCharacter()

4. **Missing Null Check** - `src/EliteMud.Game/CombatCalculator.cs:89`
   - `victim` parameter not validated before use
   - Add: `ArgumentNullException.ThrowIfNull(victim);`

### Medium Priority
5. **Code Duplication** - Multiple command handlers
   - Position validation repeated in 5 handlers
   - Extract to: `PositionValidator.RequireStanding(player)`

6. **Naming Inconsistency** - Mixed terminology
   - PlayerState, CharacterDefinition, UserService use different terms
   - Align on "Player" or "Character" throughout codebase

### Low Priority
7. **Magic Strings** - `src/EliteMud.Server/Adapters/Commands/**/*.cs`
   - Error messages duplicated across 8 files
   - Extract to: `MessageCatalog.CannotDoThatMessage`
```

---

## Questions to Ask Before Reporting

1. **Is this a real issue or acceptable tradeoff?**
   - Legacy code patterns may be intentional
   - Prototypes don't need perfect architecture

2. **Does this violation have business impact?**
   - Architecture violations are critical
   - Naming issues are low priority

3. **Is there already a plan to address this?**
   - Check roadmap.md for planned refactoring
   - Don't duplicate known issues

4. **Can I provide a concrete example fix?**
   - Show before/after code when possible
   - Reference similar good examples in codebase

5. **Is this the right time to refactor?**
   - Don't suggest refactoring during feature work
   - Flag for later cleanup unless critical

---

## Integration with Other Skills

**Works well with:**
- `pragmatic-clean-architecture` - Architecture violations
- `doc-keeper` - Add refactoring items to roadmap
- Testing skills - Suggest tests before refactoring

**Doesn't replace:**
- Compiler warnings (use dotnet build)
- Static analysis (use dotnet format, analyzers)
- Code reviews (human judgment still needed)

---

## Permissions

This skill requires:
- Read access to git history (`git diff`, `git log`)
- Read access to all C# source files (`src/**/*.cs`)
- Read access to project structure (`.csproj` files)
- Read access to `AGENTS.md` for architecture guidelines
- No write access (reports only, doesn't modify code)
