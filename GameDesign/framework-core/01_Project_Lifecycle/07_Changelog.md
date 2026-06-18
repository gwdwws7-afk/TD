# Changelog

Auto-generates changelog from git commits, sprint data, design docs.

## Usage

```
/changelog
/changelog --since v1.2.0
/changelog --format both
```

## Formats

### Internal Changelog
Detailed, technical, for development team:
- Commit hashes
- Technical details
- Breaking changes
- Migration notes

### Player-Facing Changelog
Accessible, marketing-ready:
- Plain language
- Exciting feature descriptions
- Bug fix summaries

## Output Structure

```markdown
# Changelog

## [Version] - [Date]

### Added
- [Feature 1]
- [Feature 2]

### Changed
- [Improvement 1]
- [Improvement 2]

### Fixed
- [Bug fix 1]
- [Bug fix 2]

### Removed
- [Removed feature]

### Breaking
- [Migration required]
```

## Sources

1. Git commits (conventional commits format)
2. Sprint completion data
3. Design doc updates
4. Bug fixes from tracker

## Rules

- Group by type (Added, Changed, Fixed, etc.)
- Use imperative mood ("Add" not "Added")
- Be specific ("Fix crash on startup" not "Bug fixes")
- Include issue numbers if available