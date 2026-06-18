# Asset Audit

Audits game assets for compliance with naming conventions, file size budgets, format standards.

## Usage

```
/asset-audit
/asset-audit --scope models
/asset-audit --scope textures
```

## What It Checks

### Naming Conventions
- Consistent naming pattern
- Correct prefixes/suffixes
- Proper versioning in name

### File Size Budgets
- Texture memory usage
- Model polygon counts
- Audio file sizes
- Overall asset bundle size

### Format Standards
- Correct file formats
- Compression settings
- Mipmap generation
- Platform-specific formats

### Pipeline Issues
- Orphaned assets
- Missing references
- Duplicate assets
- Outdated formats

## Output

```markdown
## Asset Audit Report

### Summary
| Category | Count | Total Size | In Budget |
|----------|-------|------------|-----------|
| Models | [N] | [X] MB | Yes/No |
| Textures | [N] | [X] MB | Yes/No |
| Audio | [N] | [X] MB | Yes/No |
| Other | [N] | [X] MB | Yes/No |

### Violations Found
| Asset | Issue | Severity |
|-------|-------|----------|
| [Asset] | [Issue] | H/M/L |

### Orphaned Assets
- [Asset path]

### Recommendations
1. [Recommendation]
2. [Recommendation]

### Verdict: [PASS / FAIL]
```

## When to Run

- Pre-alpha (establish standards)
- Before major milestones
- Pre-release optimization
- Monthly compliance check