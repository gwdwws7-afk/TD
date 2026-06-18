# Design Review

Reviews a game design document for completeness, internal consistency, implementability.

## Usage

```
/design-review design/gdd/combat-system.md
```

## Checklist

### Completeness [8/8]
- [ ] Has Overview section (one-paragraph summary)
- [ ] Has Player Fantasy section (intended feeling)
- [ ] Has Detailed Rules section (unambiguous mechanics)
- [ ] Has Formulas section (all math defined)
- [ ] Has Edge Cases section (unusual situations handled)
- [ ] Has Dependencies section (other systems listed)
- [ ] Has Tuning Knobs section (configurable values)
- [ ] Has Acceptance Criteria section (testable conditions)

### Internal Consistency
- Do formulas produce values matching described behavior?
- Do edge cases contradict main rules?
- Are dependencies bidirectional?

### Implementability
- Are rules precise enough for programmer without guessing?
- Are there "hand-wave" sections with missing details?
- Performance implications considered?

### Cross-System Consistency
- Does this conflict with existing mechanics?
- Does this create unintended interactions?
- Consistent with game pillars and tone?

## Output Format

```markdown
## Design Review: [Document Title]

### Completeness: [X/8 sections]
[Missing sections]

### Consistency Issues
[Internal or cross-system contradictions]

### Implementability Concerns
[ Vague or unimplementable sections]

### Balance Concerns
[Obvious balance risks]

### Recommendations
[Prioritized list of improvements]

### Verdict: [APPROVED / NEEDS REVISION / MAJOR REVISION NEEDED]
```

## Next Steps

- If `game-concept.md`: Run `/map-systems` next
- If individual system GDD:
  - APPROVED: Update systems index to "Approved"
  - NEEDS REVISION: Update to "In Review"