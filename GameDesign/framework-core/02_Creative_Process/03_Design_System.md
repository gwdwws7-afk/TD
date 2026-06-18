# Design System - GDD Authoring

Guided, section-by-section GDD authoring for a single game system.

## Usage

```
/design-system combat-system
/design-system inventory
```

## Prerequisite

Must have:
- `design/gdd/game-concept.md`
- `design/gdd/systems-index.md`

## Section Cycle

For each section, follow:

```
Context → Questions → Options → Decision → Draft → Approval → Write
```

## 8 Required Sections

### A. Overview
One paragraph a stranger could read and understand.
- What is this system in one sentence?
- How does a player interact with it?

### B. Player Fantasy
The emotional target - what player should feel.
- What emotion/power fantasy does this serve?
- Reference games that nail this feeling.

### C. Detailed Design
Core Rules, States and Transitions, Interactions with Other Systems.
- Unambiguous specification programmer could implement
- Use numbered rules for sequential processes
- Map every state and valid transition

### D. Formulas
Every mathematical formula with:
- Variables defined
- Ranges specified
- Edge cases noted
- Early/mid/late game outputs

### E. Edge Cases
Explicitly handle unusual situations:
- What happens at zero? At maximum?
- Simultaneous effect triggers?
- Player exploitation possibilities?

### F. Dependencies
Map every system connection with direction and nature.
- Upstream: Systems this depends on
- Downstream: Systems depending on this

### G. Tuning Knobs
Every designer-adjustable value:
- Safe ranges
- Extreme behaviors
- What breaks if set too high/low?

### H. Acceptance Criteria
Testable conditions proving system works:
- Minimum test set
- Performance budget
- QA first checks

## File Skeleton

```markdown
# [System Name]

> **Status**: In Design
> **Author**: [user + agents]
> **Last Updated**: [date]
> **Implements Pillar**: [pillar]

## Overview
[To be designed]

## Player Fantasy
[To be designed]

## Detailed Design
### Core Rules
[To be designed]
### States and Transitions
[To be designed]
### Interactions with Other Systems
[To be designed]

## Formulas
[To be designed]

## Edge Cases
[To be designed]

## Dependencies
[To be designed]

## Tuning Knobs
[To be designed]

## Acceptance Criteria
[To be designed]

## Open Questions
[To be designed]
```

## Output Location

`design/gdd/[system-name].md`

## Specialist Routing

| System Category | Primary Agent | Supporting |
|----------------|---------------|------------|
| Combat, damage, health | game-designer | systems-designer |
| Economy, loot, crafting | economy-designer | systems-designer |
| Progression, XP, skills | game-designer | systems-designer |
| Dialogue, quests, lore | game-designer | narrative-director |
| AI, pathfinding | game-designer | ai-programmer |

## Post-Design

1. Self-check: All 8 sections have content
2. Offer design review: `/design-review`
3. Update systems index status
4. Suggest next system or stop