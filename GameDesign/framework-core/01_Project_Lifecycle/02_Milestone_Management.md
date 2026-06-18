# Milestone Review

Comprehensive milestone progress assessment with go/no-go recommendation.

## Usage

```
/milestone-review
/milestone-review [milestone-name]
```

## Workflow

### 1. Gather Context

- Read milestone definition from `production/milestones/`
- Read sprint plans and sprint summaries
- Read GDDs for completed systems
- Check build status and playtest results

### 2. Assessment Areas

```markdown
## Milestone Review: [Name]

### Feature Completeness
| Feature | Planned | Actual | Status |
|---------|---------|--------|--------|
| [Feature] | [X] | [Y] | Done/In Progress/At Risk |

### Quality Metrics
- **Bug Count**: [N] open / [X] closed
- **Performance**: [FPS data if available]
- **Playtest Score**: [Average rating]

### Risk Assessment
| Risk | Severity | Status |
|------|----------|--------|
| [Risk] | H/M/L | [Mitigated/Active/Critical] |

### Dependency Health
- [System A]: On track / At risk
- [System B]: On track / At risk
```

### 3. Verdict

| Verdict | Criteria |
|---------|----------|
| **GO** | >80% features complete, quality metrics met |
| **CONDITIONAL GO** | Major features done, known issues documented |
| **NO GO** | Critical features missing or quality unacceptable |

### 4. Recommendations

If NO GO:
- List specific blockers
- Estimate extra time needed
- Recommend whether to extend milestone or cut scope

## Output Location

`production/milestones/[milestone-name]/review-[date].md`