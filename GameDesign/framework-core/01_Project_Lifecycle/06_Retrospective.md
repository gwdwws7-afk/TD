# Retrospective

Generates sprint or milestone retrospective.

## Usage

```
/retrospective
/retrospective sprint-5
/retrospective milestone-alpha
```

## Data Sources

- Sprint plans from `production/sprints/`
- Commit history
- Bug reports and resolutions
- Playtest feedback
- Previous retrospectives

## Output

```markdown
## Retrospective: [Sprint/Milestone Name]

### Summary
- **Planned**: [X] items
- **Completed**: [Y] items
- **Completion Rate**: [Z]%
- **Days**: [N]

### Velocity
| Metric | This Sprint | Previous | Trend |
|--------|-------------|----------|-------|
| Points | [X] | [Y] | [↑/↓/→] |
| Bug Fix Rate | [X] | [Y] | [↑/↓/→] |

### What Went Well
1. [Item]
2. [Item]

### What Didn't Go Well
1. [Item]
2. [Item]

### Blockers Encountered
| Blocker | Impact | Resolution |
|---------|--------|------------|
| [Blocker] | [Impact] | [How resolved] |

### Patterns Identified
- [Pattern 1]
- [Pattern 2]

### Action Items
| Action | Owner | Priority |
|--------|-------|----------|
| [Action] | [Who] | H/M/L |

### Recommendations for Next Sprint
1. [Recommendation]
2. [Recommendation]
```

## Types

| Type | Scope | Frequency |
|------|-------|-----------|
| Sprint Retrospective | Single sprint | End of each sprint |
| Milestone Retrospective | Multiple sprints | End of milestone |
| Project Retrospective | Full project | Post-launch |