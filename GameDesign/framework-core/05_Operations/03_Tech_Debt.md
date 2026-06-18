# Technical Debt Tracking

Track, categorize, prioritize tech debt across codebase.

## Usage

```
/tech-debt
/tech-debt scan
/tech-debt register "Legacy renderer" --severity=high --effort=large
```

## Debt Categories

| Category | Description |
|----------|-------------|
| **Architecture** | Systemic design issues |
| **Code Quality** | Complexity, duplication, clarity |
| **Testing** | Missing or inadequate tests |
| **Documentation** | Missing docs, outdated docs |
| **Performance** | Optimization opportunities |
| **Security** | Vulnerability concerns |
| **Compatibility** | Platform/version support |

## Severity Levels

| Level | Impact | Example |
|-------|--------|---------|
| Critical | Blocks feature development | Core system instability |
| High | Significant daily friction | Slow compilation |
| Medium | Regular minor annoyance | Inconsistent naming |
| Low | Cosmetic/ideal improvements | Code style variations |

## Debt Register Format

```markdown
# Tech Debt Register

## Summary
| Category | Critical | High | Medium | Low |
|----------|----------|------|--------|-----|
| Architecture | 1 | 2 | 3 | 0 |
| Code Quality | 0 | 1 | 5 | 2 |
| ... | | | | |

## High Priority
### [Debt 1]
- **Location**: [Files/systems]
- **Category**: [Category]
- **Severity**: High
- **Effort**: [S/M/L]
- **Issue**: [Description]
- **Why**: [Why it matters]
- **Remediation**: [How to fix]

## Medium Priority
...

## Low Priority
...
```

## Repayment Strategy

1. **Immediate**: Critical debt blocking progress
2. **Sprint Allocation**: 10-20% of each sprint
3. **Feature-Tied**: Fix when working on related feature
4. **Big Bang**: Major refactor when scale demands