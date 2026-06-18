# Reverse Document

Generate design or architecture docs from existing implementation.

## Usage

```
/reverse-document design src/gameplay/combat
/reverse-document architecture src/core/entity-component
/reverse-document concept prototypes/vehicle-combat
```

## When to Use

- Built feature without design doc first
- Inherited codebase without documentation
- Prototyped mechanic, need to formalize
- Need to document "why" behind existing code

## Workflow

### 1. Analyze Implementation

**For design docs (GDD):**
- Identify mechanics, rules, formulas
- Extract gameplay values (damage, cooldowns, ranges)
- Find state machines, ability systems, progression
- Detect edge cases handled in code
- Map dependencies

**For architecture docs (ADR):**
- Identify patterns (ECS, singleton, observer)
- Understand technical decisions
- Map dependencies and coupling
- Assess performance characteristics

**For concept docs:**
- Identify core mechanic
- Extract emergent gameplay patterns
- Note what worked vs didn't
- Document player fantasy / feel

### 2. Ask Clarifying Questions

**Design questions:**
- "Stamina depletes during combat. Is this for pacing, resource management, or...?"
- "Stagger seems central. Core pillar or supporting feature?"
- "Damage scales exponentially. Intentional power fantasy or needs rebalancing?"

**Architecture questions:**
- "Service locator pattern chosen for testability, decoupling, or...?"
- "Manual memory management instead of smart pointers. Performance requirement or legacy?"

### 3. Present Findings

Show discovered mechanics, formulas, unclear areas before drafting.

### 4. Draft Document

Capture:
- What exists (mechanics, patterns, implementation)
- Why it exists (intent clarified with user)
- What's missing (gaps in design)
- Follow-up work needed

### 5. Write with Metadata

```markdown
---
status: reverse-documented
source: src/gameplay/combat/
date: 2026-02-13
verified-by: [User name]
---

> **Note**: This document was reverse-engineered from existing implementation.
> It captures current behavior and clarified design intent.
```

## Output Locations

| Type | Path |
|------|------|
| design | `design/gdd/[system-name].md` |
| architecture | `docs/architecture/[decision-name].md` |
| concept | `design/concepts/[name].md` |

## Follow-Up Work

After writing, flag:
1. Run `/balance-check` on discovered formulas
2. Create ADR for architecture decisions
3. Implement missing edge cases
4. Extend doc when features expanded