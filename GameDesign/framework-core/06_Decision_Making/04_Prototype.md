# Prototype

Rapid prototyping workflow for validating game concepts or mechanics.

## Usage

```
/prototype core-combat
/prototype "Does the grappling hook feel good?"
```

## When to Prototype

- Core mechanic unproven
- Design decision contested
- Player feedback unclear
- Technical feasibility unknown
- New system untested

## Prototype Workflow

### 1. Define Prototype Goal
- Specific question to answer
- Success criteria defined
- Time box established (1-7 days)

### 2. Build Minimum Viable Prototype
- Placeholder art acceptable
- Single focus area only
- No production code standards
- Rapid iteration encouraged

### 3. Playtest Immediately
- Internal playtest first
- Get external feedback fast
- Observe, don't ask
- Iterate based on feel

### 4. Decision

| Result | Action |
|--------|--------|
| Prototype validates concept | Integrate into production |
| Concept needs iteration | Prototype again with learnings |
| Concept doesn't work | Pivot, don't force it |

## Prototype Location

`prototypes/[feature-name]/`

## Output

```markdown
# Prototype: [Name]

## Goal
[Question being answered]

## Time Box
[X days]

## Success Criteria
1. [Criterion 1]
2. [Criterion 2]

## Results
### What Worked
1. [Finding]

### What Didn't Work
1. [Finding]

## Decision
[Validated / Needs Iteration / Pivoted]

## Next Steps
[Based on decision]
```

## Principles

- Time-box strictly
- Single question focus
- Placeholder everything
- Feel > correctness
- Kill darlings if not working