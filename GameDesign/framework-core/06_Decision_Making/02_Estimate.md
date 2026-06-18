# Task Estimation

Estimates task effort by analyzing complexity, dependencies, risk.

## Usage

```
/estimate "Add parry mechanic to combat"
/estimate Implement skill tree system
```

## Analysis Factors

### Code Complexity
- Lines of code in affected files
- Number of dependencies and coupling
- Core/engine code vs leaf/feature code
- Existing patterns available

### Scope
- Number of systems touched
- New code vs modification
- Test coverage needed
- Data migration needed

### Risk
- New technology or unfamiliar libraries
- Unclear requirements
- Dependencies on unfinished work
- Cross-system integration

## Output

```markdown
## Task Estimate: [Task]

### Task Description
[Clear 1-2 sentence restatement]

### Complexity Assessment
| Factor | Assessment | Notes |
|--------|-----------|-------|
| Systems affected | [List] | |
| Files likely modified | [Count] | |
| New vs modification | [Ratio] | |
| Integration points | [Count] | |
| Test coverage | [Low/Med/High] | |
| Patterns available | [Yes/Partial/No] | |

### Effort Estimate
| Scenario | Days | Assumption |
|----------|------|------------|
| Optimistic | [X] | Everything goes right |
| Expected | [Y] | Normal pace, minor issues |
| Pessimistic | [Z] | Significant unknowns, blocked |

**Recommended budget: [Y days]**

### Confidence: [High / Medium / Low]
[Factors driving confidence level]

### Risk Factors
| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| [Risk] | H/M/L | [Days] | [Plan] |

### Dependencies
| Dependency | Status | Impact if Delayed |
|------------|--------|-------------------|
| [What first] | [Status] | [Impact] |

### Suggested Breakdown
| # | Sub-task | Days |
|---|----------|------|
| 1 | [Research/spike] | [X] |
| 2 | [Core implementation] | [X] |
| 3 | [Integration] | [X] |
| 4 | [Testing] | [X] |
| 5 | [Code review] | [X] |
| | **Total** | **[Y]** |

### Notes
- [Key assumptions]
- [Scope boundaries]
- [Recommendations]
```

## Principles

- Always give range, never single number
- Recommended = expected, not optimistic
- Round to half-day increments
- If >10 days expected, break into smaller tasks
- If confidence Low, recommend spike first