# Playtest Report

Analyzes playtest feedback into structured format.

## Usage

```
/playtest-report
/playtest-report analyze feedback.txt
/playtest-report template
```

## Report Template

```markdown
# Playtest Report

## Session Info
- **Date**: [Date]
- **Playtester**: [Name/Type: Internal/External]
- **Build**: [Version]
- **Duration**: [Time played]
- **Focus Areas**: [What to test]

## Feedback Summary

### What Worked
1. [Positive feedback]
2. [Positive feedback]

### What Didn't Work
1. [Negative feedback]
2. [Negative feedback]

### Unexpected Issues
1. [Surprising feedback]
2. [Surprising feedback]

## Quantitative Data
| Metric | Value | Target |
|--------|-------|--------|
| Fun rating | [X/10] | [Y] |
| Difficulty | [Easy/Medium/Hard] | [Target] |
| Completion time | [X min] | [Y min] |
| Restart rate | [X%] | [<Y%] |

## Issues Found
| Issue | Severity | Suggested Fix |
|-------|----------|---------------|
| [Issue] | H/M/L | [Fix] |

## Design Recommendations
1. [Recommendation]
2. [Recommendation]

## Follow-Up
- [ ] Investigate [issue]
- [ ] Adjust [balance element]
- [ ] Retest [feature]
```

## Template Generation

`/playtest-report template` generates a blank template for manual playtest sessions.

## Analysis

When given feedback files, extracts:
- Sentiment (positive/negative/neutral)
- Recurring themes
- Severity ratings
- Actionable recommendations