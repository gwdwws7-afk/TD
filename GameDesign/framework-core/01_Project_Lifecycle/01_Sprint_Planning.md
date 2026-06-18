# Sprint Planning

Creates or updates sprint plans based on milestone context, completed work, and capacity.

## Usage

```
/sprint-plan new
/sprint-plan update
/sprint-plan current
```

## Workflow

### 1. Read Context

- Read `production/milestones/` milestone doc
- Read `production/sprints/` existing sprint plans
- Read `design/gdd/systems-index.md` for system priorities
- Check session state at `production/session-state/active.md`

### 2. Generate Sprint Plan

```markdown
## Sprint [N]: [Name]
Generated: [Date]
Milestone: [Target milestone]

### Goals
1. [Specific goal 1]
2. [Specific goal 2]

### Deliverables
| Deliverable | System | Priority | Points |
|-------------|--------|----------|--------|
| [Item] | [System] | P1/P2/P3 | [Est.] |

### Tasks
| # | Task | Owner | Days | Dependencies |
|---|------|-------|------|--------------|
| 1 | [Task] | [Agent] | [X] | [Depends on] |

### Capacity
- Available: [X] days
- Planned: [Y] days
- Buffer: [Z] days

### Risks
| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| [Risk] | H/M/L | H/M/L | [Plan] |

### Sprint Commitment
**Committed**: [Deliverables]
**Stretch**: [If capacity allows]
```

### 3. Approval Protocol

- Present draft to user
- Ask: "May I write to `production/sprints/sprint-[N].md`?"
- After writing, update session state

## Key Principles

- Sprint goals align with milestone targets
- Capacity includes buffer for unexpected issues
- Dependencies tracked explicitly
- Risks identified before sprint starts