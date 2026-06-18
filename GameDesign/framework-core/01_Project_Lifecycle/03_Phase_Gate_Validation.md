# Phase Gate Validation

Validates readiness to advance between development phases.

## Usage

```
/gate-check pre-production
/gate-check production
/gate-check post-launch
```

## Phases

### Pre-Production Gate
Evaluates: Concept → Pre-Production transition

**Required Artifacts:**
- [ ] Game concept document
- [ ] Game pillars document
- [ ] Systems index with priorities
- [ ] Vertical slice scope definition
- [ ] Engine configured

**Checks:**
- Concept is clear and compelling
- Pillars create meaningful trade-offs
- MVP scope is achievable
- Technical risks identified

### Production Gate
Evaluates: Pre-Production → Production transition

**Required Artifacts:**
- [ ] Core loop prototype validated
- [ ] Core systems GDDs approved
- [ ] Vertical slice playable
- [ ] Sprint velocity established
- [ ] Risk register updated

**Checks:**
- Core loop is fun
- Systems integrate correctly
- Performance meets targets
- Team can sustain velocity

### Post-Launch Gate
Evaluates: Launch readiness

**Required Artifacts:**
- [ ] All milestone criteria met
- [ ] Localization complete
- [ ] Store assets submitted
- [ ] Marketing materials ready
- [ ] Support processes defined

## Output Format

```markdown
## Gate Check: [Phase]
Date: [Date]

### PASS / CONCERNS / FAIL

#### Blocker Items (Must Fix)
1. [Item]

#### Concerns (Should Fix)
1. [Item]

#### Strengths
1. [Item]

#### Sign-offs
- [ ] Creative Director
- [ ] Technical Director
- [ ] Producer
```

## Decision Rules

| Result | Meaning |
|--------|---------|
| **PASS** | Proceed to next phase |
| **CONCERNS** | Proceed with explicit acknowledgment of risks |
| **FAIL** | Must resolve blockers before proceeding |