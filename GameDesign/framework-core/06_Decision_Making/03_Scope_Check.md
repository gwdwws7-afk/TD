# Scope Check

Analyzes feature or sprint for scope creep.

## Usage

```
/scope-check sprint-3
/scope-check parry-mechanic
```

## Output

```markdown
## Scope Check: [Feature/Sprint]
Generated: [Date]

### Original Scope
[Items from original plan]

### Current Scope
[Items currently implemented/in progress]

### Scope Additions
| Addition | Who | When | Justified | Effort |
|----------|-----|------|-----------|--------|
| [Item] | [Person] | [Date] | [Yes/No] | [S/M/L] |

### Scope Removals
| Removed | Reason | Impact |
|---------|--------|--------|
| [Item] | [Why] | [What's affected] |

### Bloat Score
- Original items: [N]
- Current items: [N]
- Items added: [N] (+[X]%)
- Items removed: [N]
- Net change: [+/-N] ([X>%])

### Risk Assessment
- **Schedule Risk**: [L/M/H] - [explanation]
- **Quality Risk**: [L/M/H] - [explanation]
- **Integration Risk**: [L/M/H] - [explanation]

### Recommendations
1. **Cut**: [Items to remove]
2. **Defer**: [Items to future sprint]
3. **Keep**: [Justified additions]
4. **Flag**: [Needs decision]

### Verdict
| Verdict | Criteria |
|---------|----------|
| On Track | Within 10% of original |
| Minor Creep | 10-25% increase |
| Significant Creep | 25-50% increase |
| Out of Control | >50% increase |
```

## Rules

- Scope creep = additions without cuts or timeline extension
- Not all additions bad - some are discovered requirements
- Always quantify - "feels bigger" not actionable, "+35%" is
- Prioritize core player experience over nice-to-haves