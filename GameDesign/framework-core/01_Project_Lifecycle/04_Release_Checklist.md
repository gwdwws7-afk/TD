# Release Checklist

Comprehensive pre-release validation covering build, certification, and launch readiness.

## Usage

```
/release-checklist
/release-checklist --env staging
```

## Categories

### Build Verification
- [ ] Build completes without errors
- [ ] All platforms build successfully
- [ ] No compiler warnings in release build
- [ ] Version number correctly embedded
- [ ] Build date correctly embedded

### Certification
- [ ] Console certification requirements met (if applicable)
- [ ] Age rating submission complete
- [ ] Platform-specific requirements checked

### Store Metadata
- [ ] Store description finalized
- [ ] Screenshots captured (all languages)
- [ ] Trailer/video finalized
- [ ] Icon approved
- [ ] Rating symbols correct

### Content Integrity
- [ ] All promised features implemented
- [ ] No placeholder content
- [ ] All tutorial/tooltips filled
- [ ] Ending content complete

### Technical
- [ ] Save system tested
- [ ] Cloud save working (if applicable)
- [ ] Achievements/triggers verified
- [ ] DLC/content codes validated

### Legal/Compliance
- [ ] Privacy policy URL included
- [ ] Terms of service accepted
- [ ] Copyright notices present
- [ ] Third-party licenses attributed

### Operations
- [ ] Analytics/events configured
- [ ] Crash reporting configured
- [ ] Support contact configured
- [ ] Server infrastructure tested

## Output

```markdown
## Release Checklist: [Build]
Date: [Date]

| Category | Items | Passed | Failed |
|----------|-------|--------|--------|
| Build | 5 | 5 | 0 |
| Store | 5 | 4 | 1 |
| ... | | | |

### Critical Blockers
1. [Item]

### Recommended Fixes
1. [Item]

### Go/No-Go: [PENDING/CLEARED]
```

## Output Location

`production/releases/[version]/checklist-[date].md`