# Hotfix Workflow

Emergency fix workflow bypassing normal sprint processes.

## Usage

```
/hotfix BUG-123
/hotfix critical-crash-in-combat
```

## Severity Assessment

| Severity | Definition | Workflow |
|----------|------------|----------|
| S1-Critical | Game unplayable, data loss, security | Hotfix immediately |
| S2-Major | Significant feature broken, workaround exists | Hotfix within 24h |
| S3-Minor | Feature impaired | Normal bug fix |
| S4-Trivial | Cosmetic | Backlog |

## Workflow

### 1. Create Hotfix Record

```markdown
## Hotfix: [Short Description]
Date: [Date]
Severity: [S1/S2]
Reporter: [Who found it]
Status: IN PROGRESS

### Problem
[What is broken, player impact]

### Root Cause
[To be filled]

### Fix
[To be filled]

### Testing
[What was tested]

### Approvals
- [ ] Fix reviewed (lead-programmer)
- [ ] Regression passed (qa-tester)
- [ ] Release approved (producer)

### Rollback Plan
[How to revert]
```

### 2. Create Branch
```
git checkout -b hotfix/[short-name] [release-tag-or-main]
```

### 3. Implement Fix
- MINIMUM change only
- No refactoring
- No "while we're here" additions

### 4. Collect Approvals
- Lead programmer: Correctness review
- QA tester: Regression testing
- Producer: Deployment timing

### 5. Summary Output

```markdown
## Hotfix Applied: [Name]
- **Severity**: S1/S2
- **Root Cause**: [Brief]
- **Fix**: [Summary]
- **Testing**: [What passed]
- **Approvals**: [Pending/Complete]
```

## Rules

- Minimum change only - no cleanup
- Always have rollback plan
- Merge to release AND development branch
- Post-incident review within 48h
- If fix >4h, escalate to technical director