# Bug Report

Creates structured bug reports.

## Usage

```
/bug-report Player dies when hitting wall at high speed
/bug-report analyze src/gameplay/player.cpp
```

## Report Template

```markdown
# Bug Report

## Summary
**Title**: [Concise, descriptive]
**ID**: BUG-[NNNN]
**Severity**: [S1-Critical / S2-Major / S3-Minor / S4-Trivial]
**Priority**: [P1-Immediate / P2-Next Sprint / P3-Backlog / P4-Wishlist]
**Status**: Open
**Reported**: [Date]
**Reporter**: [Name]

## Classification
- **Category**: [Gameplay / UI / Audio / Visual / Performance / Crash / Network]
- **System**: [Affected game system]
- **Frequency**: [Always / Often (>50%) / Sometimes / Rare]
- **Regression**: [Yes/No/Unknown]

## Environment
- **Build**: [Version/commit]
- **Platform**: [OS, hardware]
- **Scene/Level**: [Location]
- **Game State**: [Relevant state]

## Reproduction Steps
**Preconditions**: [Required setup]

1. [Exact step 1]
2. [Exact step 2]
3. [Exact step 3]

**Expected**: [What should happen]
**Actual**: [What happens instead]

## Technical Context
- **Likely files**: [Based on codebase search]
- **Related systems**: [Interactions]
- **Possible root cause**: [If identifiable]

## Evidence
- **Logs**: [Log output]
- **Visual**: [Description]
```

## Severity Definitions

| Severity | Definition | Response |
|----------|------------|----------|
| S1-Critical | Game unplayable, data loss, security | Immediate hotfix |
| S2-Major | Significant feature broken, workaround exists | Within 24h |
| S3-Minor | Feature impaired, minor issue | Next sprint |
| S4-Trivial | Cosmetic, very minor | Backlog |

## Analyze Mode

For `/bug-report analyze [path]`:
- Read target files
- Identify: null refs, off-by-ones, race conditions, unhandled edge cases, resource leaks
- Generate report with trigger scenario and fix recommendation