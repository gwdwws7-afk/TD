# Patch Notes

Generate player-facing patch notes from git history, sprint data, internal changelogs.

## Usage

```
/patch-notes
/patch-notes v1.2.0
/patch-notes --format markdown
```

## Output Structure

```markdown
# Patch Notes - v[VERSION]

## New Features
> **Exciting intro line about what's new**

- **[Feature Name]**: [Description that makes player want this]
- **[Feature Name]**: [Description]

## Improvements
> **What got better**

- **[Improvement]**: [How it helps player]
- **[Improvement]**: [How it helps player]

## Bug Fixes
> **Players asked, we listened**

- Fixed [issue] that caused [problem]
- Fixed [issue] affecting [situation]
- Fixed [issue] where [scenario]

## Balance Changes
> **For games with combat/economy**

- [Change 1]: [Numbers if applicable]
- [Change 2]: [Numbers if applicable]

## Known Issues
> **Transparent about current problems**

- [Issue 1] - [Workaround if any]
- [Issue 2] - [ETA if known]

## What's Coming
> **Tease next patch without overpromising**

- [Hint at one feature]
```

## Tone Guidelines

- **Player-centric**: Focus on what player gains, not technical changes
- **Conversational**: "We fixed" not "Fixed bug #1234"
- **Exciting**: Make new features sound appealing
- **Honest**: Don't oversell, don't hide problems
- **Concise**: Players scan, don't read

## Sources

1. Git commits (analyzed for player impact)
2. Internal changelog
3. Bug tracker (for fix attribution)
4. Community feedback (what players asked for)

## Rules

- Don't include internal technical debt fixes
- Attribute to community when players reported
- Balance detail with readability
- Include workaround for known issues
- Never promise specific dates