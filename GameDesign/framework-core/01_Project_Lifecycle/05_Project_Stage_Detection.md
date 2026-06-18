# Project Stage Detection

Automatically analyzes project state, detects stage, identifies gaps.

## Usage

```
/project-stage-detect
```

## Stages

| Stage | Description |
|-------|-------------|
| **Concept** | Initial idea, no formal documentation |
| **Pre-Production** | Game design docs, prototypes, technical验证 |
| **Production** | Active development, sprints running |
| **Polish** | Feature complete, focus on quality |
| **Content Complete** | All content in, iterating on fixes |
| **Release Candidate** | Code frozen, preparing for launch |
| **Live** | Released, ongoing support |

## Detection Criteria

### Concept Stage
- No `design/gdd/game-concept.md`
- No sprint plans
- Source code minimal or prototype only

### Pre-Production Stage
- `design/gdd/game-concept.md` exists
- Systems index exists but GDDs incomplete
- No sprint history
- Prototype exists for core loop

### Production Stage
- Multiple sprint plans exist
- GDDs being actively updated
- Regular builds being produced
- Playtests occurring

### Polish Stage
- All MVP systems designed and implemented
- Bug fix commits dominate
- Feature additions minimal
- Performance optimization ongoing

### Content Complete
- All planned content implemented
- No more planned sprints for content
- Focus on bug fixes and tuning

### Release Candidate
- `production/releases/` version branch exists
- No feature commits
- Only bug fixes and polish

### Live
- Version tagged and deployed
- Live operations running

## Output

```markdown
## Project Stage Detection

**Detected Stage**: [Stage]
**Confidence**: High/Medium/Low

**Evidence**:
- [File/fact 1]
- [File/fact 2]

**Recommended Next Steps**:
1. [Step 1]
2. [Step 2]

**Gaps Identified**:
1. [Gap]
```

## Stage-Specific Guidance

Each detected stage triggers appropriate next actions and warns about common stage-specific pitfalls.