# Balance Check

Analyzes game balance data files, formulas, configuration.

## Usage

```
/balance-check
/balance-check combat-system
```

## What It Checks

### Economy Balance
- Currency sinks vs sources
- Inflation/deflation risks
- Progression pacing
- Reward scheduling

### Combat Balance
- Damage formulas across tiers
- Enemy health/damage scaling
- Player power progression
- Difficulty curve

### Progression Balance
- XP/level pacing
- Skill unlock timing
- Equipment power progression
- Unlock breadcrumbs

### Edge Cases
- Degenerate strategies
- Exploit combinations
- Zero-sum states
- Infinite loops

## Output

```markdown
## Balance Check: [System/Full Game]

### Economy Health
| Metric | Value | Status |
|--------|-------|--------|
| Sink/Source Ratio | [X] | Healthy/Warning/Critical |

### Progression Curve
- Early game: [Assessment]
- Mid game: [Assessment]
- Late game: [Assessment]

### Identified Issues
| Issue | Severity | Recommendation |
|-------|----------|----------------|
| [Issue] | H/M/L | [Fix] |

### Outliers
- [Formula with extreme values]
- [Stat that breaks progression]

### Degenerate Strategies
1. [Strategy] - [How to exploit]
2. [Counter recommendation]

### Verdict: [BALANCED / NEEDS TUNING / CRITICAL]
```

## When to Run

- After any formula change
- Before playtesting
- During balance iteration
- Pre-release validation