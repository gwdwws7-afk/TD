# Code Review

Architectural and quality code review.

## Usage

```
/code-review src/gameplay/combat/
/code-review src/core/EntityManager.cpp
```

## Checklist

### Coding Standards [6/6]
- [ ] Public methods/classes have doc comments
- [ ] Cyclomatic complexity under 10 per method
- [ ] No method exceeds 40 lines (excluding data)
- [ ] Dependencies injected (no static singletons for game state)
- [ ] Config values loaded from data files
- [ ] Systems expose interfaces (not concrete deps)

### Architecture
- [ ] Correct dependency direction (engine <- gameplay)
- [ ] No circular dependencies
- [ ] Proper layer separation
- [ ] Events/signals for cross-system communication
- [ ] Consistent with established patterns

### SOLID
- [ ] Single Responsibility: One reason to change
- [ ] Open/Closed: Extendable without modification
- [ ] Liskov Substitution: Subtypes substitutable
- [ ] Interface Segregation: No fat interfaces
- [ ] Dependency Inversion: Depends on abstractions

### Game-Specific
- [ ] Frame-rate independence (delta time)
- [ ] No allocations in hot paths
- [ ] Proper null/empty state handling
- [ ] Thread safety where required
- [ ] Resource cleanup (no leaks)

## Output

```markdown
## Code Review: [File/System]

### Standards Compliance: [X/6 passing]
[Failures with line refs]

### Architecture: [CLEAN / MINOR ISSUES / VIOLATIONS]
[Specific concerns]

### SOLID: [COMPLIANT / ISSUES FOUND]
[Violations]

### Game-Specific Concerns
[Issues]

### Positive Observations
[What's done well]

### Required Changes
[Must-fix items]

### Suggestions
[Nice-to-have improvements]

### Verdict: [APPROVED / APPROVED WITH SUGGESTIONS / CHANGES REQUIRED]
```