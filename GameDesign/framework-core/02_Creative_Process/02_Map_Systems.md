# Map Systems

Decompose a game concept into individual systems, map dependencies, prioritize design order.

## Usage

```
/map-systems
/map-systems next
/map-systems [system-name]
```

## Phases

### Phase 1: Read Concept

Required:
- Read `design/gdd/game-concept.md`

Optional:
- `design/gdd/game-pillars.md`
- `design/gdd/systems-index.md` (if exists, resume not recreate)
- Glob `design/gdd/*.md` for existing GDDs

### Phase 2: Systems Enumeration

**Extract explicit systems** from concept:
- Core Mechanics section
- Core Loop section (implies drive systems)
- Technical Considerations
- MVP Definition

**Identify implicit systems** using inference patterns:

| Explicit | Implies |
|----------|---------|
| Inventory | Item database, equipment slots, weight rules, inventory UI |
| Combat | Damage calc, health, hit detection, status effects, AI, combat UI |
| Open World | Streaming, LOD, fast travel, map, POI tracking |
| Multiplayer | Networking, lobby, state sync, anti-cheat |
| Crafting | Recipe DB, ingredients, crafting UI, success/failure |
| Dialogue | Dialogue tree, dialogue UI, choice tracking, NPC state |
| Progression | XP, level-up, skill tree, unlock tracking |

**User reviews** enumeration for completeness.

### Phase 3: Dependency Mapping

For each system, determine dependencies:
- Input/output: System A produces data System B needs
- Structural: System A provides framework System B plugs into
- UI dependencies: Every gameplay system has a UI counterpart

**Sort into layers:**
1. Foundation: Zero dependencies (designed first)
2. Core: Depends only on Foundation
3. Feature: Depends on Core
4. Presentation: UI/feedback wrappers
5. Polish: Meta-systems, tutorials, analytics

**Detect circular dependencies** and propose resolutions.

### Phase 4: Priority Assignment

| Tier | Criteria |
|------|----------|
| MVP | Required for core loop to function |
| Vertical Slice | Complete experience in one area |
| Alpha | All remaining gameplay systems |
| Full Vision | Polish, meta, nice-to-haves |

### Phase 5: Create Systems Index

```markdown
## Systems Index: [Game Name]

### Enumeration
| System | Category | Description | Explicit? |

### Dependency Map
| System | Depends On | Layer |

### Design Order
1. [System A]
2. [System B]
...

### Priority Tiers
| Tier | Systems |
|------|---------|
| MVP | A, B, C |
| Vertical Slice | D, E |

### Progress Tracker
| System | Status | GDD Path |
|--------|--------|----------|
| A | Not Started | - |
```

## Output Location

`design/gdd/systems-index.md`

## Handoff to /design-system

After index creation:
- `/map-systems next` - Pick highest-priority undesigned system
- `/design-system [system]` - Write that system's GDD