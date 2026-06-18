# Team Combat

Orchestrates combat feature end-to-end with game-designer, gameplay-programmer, AI-programmer, technical-artist, sound-designer, QA-tester.

## Roles

| Role | Responsibility |
|------|---------------|
| Game Designer | Combat feel, balance, player fantasy |
| Gameplay Programmer | Combat mechanics implementation |
| AI Programmer | Enemy behavior and tactics |
| Technical Artist | VFX, juice, impact feedback |
| Sound Designer | Audio feedback, combat sounds |
| QA Tester | Combat testing, edge cases |

## Combat Feature Pipeline

### 1. Design Phase
- Game designer writes combat GDD via `/design-system combat`
- `/design-review` validates completeness
- Combat pillars defined (e.g., "deliberate pacing", "weighty impacts")

### 2. Implementation Phase
- Gameplay programmer implements core mechanics
- AI programmer implements enemy behavior
- Technical artist adds VFX and juice
- Sound designer adds audio feedback

### 3. Polish Phase
- `/team-polish` for performance and feel
- Playtesting for balance
- `/balance-check` on combat formulas

### 4. Validation Phase
- QA tests combat in various scenarios
- Bug reports filed via `/bug-report`
- Final approval from game designer

## Combat Design Principles

- Core combat must feel good in isolation
- 30-second loop: hit, dodge, strategize
- Enemy variety through AI behavior, not just stats
- Juice: screen shake, hit pause, particle bursts
- Audio: layered impact sounds, spatial positioning

## Deliverables

| Phase | Deliverable |
|-------|-------------|
| Design | Combat GDD |
| Prototype | Playable combat test scene |
| Alpha | Full enemy roster |
| Polish | Juice, audio, balance tuned |
| Release | All content complete |