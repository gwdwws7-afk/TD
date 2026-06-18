# Localization Workflow

Extract strings, validate readiness, check for hardcoded text.

## Usage

```
/localize scan
/localize extract
/localize validate
/localize status
```

## Subcommands

### scan
Search for localization issues:
- String literals not wrapped in localization function
- Concatenated strings that should be parameterized
- Wrong placeholder types (%s vs {name})
- Date/time formatting not locale-aware
- Number formatting without locale awareness
- Text embedded in images
- Strings assuming left-to-right text

### extract
- Scan source for localized string references
- Compare against existing string table
- Generate new entries with suggested keys
- Convention: `[category].[subcategory].[description]`

### validate
Check each entry for:
- Missing translations
- Placeholder mismatches
- String length violations
- Orphaned keys

### status
Generate coverage matrix:

```markdown
## Localization Status

| Locale | Total | Translated | Missing | Stale | Coverage |
|--------|-------|-----------|---------|-------|----------|
| en (source) | [N] | [N] | 0 | 0 | 100% |
| [locale] | [N] | [N] | [N] | [N] | [X]% |

### Issues
- [N] hardcoded strings found
- [N] strings exceeding limits
- [N] placeholder mismatches
- [N] orphaned keys
```

## String Table Location

`assets/data/strings/[locale].json`

## Rules

- English (en) is always source locale
- Every entry needs translator comment
- Never modify translation files directly
- Character limits defined per-UI-element
- RTL support from start, not bolted on