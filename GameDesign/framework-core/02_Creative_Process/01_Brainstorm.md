# Brainstorm - Concept Ideation

Guided game concept ideation from zero to structured game concept document.

## Usage

```
/brainstorm
/brainstorm roguelike
/brainstorm open
```

## Phases

### Phase 1: Creative Discovery

Understand the person, not just the game.

**Questions:**
- Emotional anchors: What game moment moved/thrilled you?
- Taste profile: 3 games you spent most time with? Why?
- Practical constraints: Solo/team? Timeline? Platform?
- First game or experienced developer?

**Output**: Creative Brief (3-5 sentences)

### Phase 2: Concept Generation

Generate 3 distinct concepts using:
- **Verb-First Design**: Start with core verb (build, fight, explore...)
- **Mashup Method**: [Genre A] + [Theme B] = Unique hook
- **Experience-First (MDA Backward)**: Start from desired emotion

**For each concept:**
- Working Title
- Elevator Pitch (passes 10-second test)
- Core Verb
- Core Fantasy
- Unique Hook ("Like X, AND ALSO Y")
- Primary MDA Aesthetic
- Estimated Scope
- Why It Could Work
- Biggest Risk

### Phase 3: Core Loop Design

Structure at 4 time scales:
- **30-Second Loop**: Moment-to-moment, intrinsic satisfaction
- **5-Minute Loop**: Short-term goals, "one more turn"
- **Session Loop**: Natural stopping points
- **Progression Loop**: Days/weeks of growth

**Player Motivation (Self-Determination Theory):**
- Autonomy: Meaningful choice
- Competence: Skill growth feeling
- Relatedness: Connection to characters/world

### Phase 4: Pillars and Boundaries

Define 3-5 pillars:
- Name + one-sentence definition
- Design test for trade-off decisions
- Pillars should create tension with each other

Define 3+ anti-pillars:
- What this game is NOT
- Prevents "wouldn't it be cool if..." scope creep

### Phase 5: Player Type Validation

- Primary player type (Bartle taxonomy)
- Secondary appeal
- Who this is NOT for
- Market validation

### Phase 6: Scope and Feasibility

- Engine recommendation
- Art pipeline assessment
- Content scope estimate
- MVP definition
- Biggest risks
- Scope tiers (full vision vs. ship if time runs out)

## Output

Save to: `design/gdd/game-concept.md`

## Next Steps

1. `/setup-engine [engine]` - Configure engine
2. `/design-review design/gdd/game-concept.md` - Validate completeness
3. `/map-systems` - Decompose into systems
4. `/design-system [system]` - Write per-system GDDs
5. `/prototype [core-mechanic]` - Validate core loop
6. `/sprint-plan new` - Plan first sprint