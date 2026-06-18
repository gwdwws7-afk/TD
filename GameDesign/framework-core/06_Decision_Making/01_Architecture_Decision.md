# Architecture Decision Record

Documents significant technical decisions.

## Usage

```
/architecture-decision use-ECS-for-entities
```

## ADR Format

```markdown
# ADR-[NNNN]: [Title]

## Status
[Proposed | Accepted | Deprecated | Superseded]

## Date
[Decision date]

## Context

### Problem Statement
[What problem are we solving?]

### Constraints
- [Technical constraints]
- [Timeline constraints]
- [Resource constraints]

### Requirements
- [Must support X]
- [Must perform within Y]

## Decision
[The specific decision made]

### Architecture Diagram
[ASCII diagram]

### Key Interfaces
[API contracts created]

## Alternatives Considered

### Alternative 1: [Name]
- **Description**: [How this works]
- **Pros**: [Advantages]
- **Cons**: [Disadvantages]
- **Rejection Reason**: [Why not chosen]

### Alternative 2: [Name]
...

## Consequences

### Positive
- [Good outcomes]

### Negative
- [Trade-offs accepted]

### Risks
- [Things that could go wrong]
- [Mitigation]

## Performance Implications
- **CPU**: [Expected impact]
- **Memory**: [Expected impact]
- **Load Time**: [Expected impact]

## Migration Plan
[If changing existing code, how to get from here to there]

## Validation Criteria
[How will we know this was correct?]

## Related Decisions
- [Related ADRs]
- [Related design docs]
```

## When to Create ADR

- Major technology choice (engine features, middleware)
- Architectural pattern changes
- Cross-system implications
- Performance-critical decisions
- Long-term maintenance impact

## Location

`docs/architecture/adr-[NNNN]-[slug].md`