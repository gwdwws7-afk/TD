# Claude Code Game Studios - Framework Core

Production-ready workflows for indie game development.

## Architecture

```
framework-core/
├── 01_Project_Lifecycle/     # Sprint, milestone, gate, release
├── 02_Creative_Process/      # Brainstorm, design, review
├── 03_Team_Orchestration/    # Combat, narrative, level, UI teams
├── 04_Quality_Assurance/     # Code review, bug report, balance
├── 05_Operations/            # Hotfix, localize, tech debt
└── 06_Decision_Making/       # Architecture, estimation, scope
```

## Skill Index

### Project Lifecycle
| Skill | Purpose |
|-------|---------|
| `/sprint-plan` | Create/update sprint plans |
| `/milestone-review` | Milestone progress assessment |
| `/gate-check` | Phase gate validation |
| `/release-checklist` | Pre-release validation |
| `/launch-checklist` | Full launch readiness |
| `/project-stage-detect` | Auto-detect project stage |

### Creative Process
| Skill | Purpose |
|-------|---------|
| `/brainstorm` | Concept ideation from zero |
| `/map-systems` | Decompose concept into systems |
| `/design-system` | Write per-system GDDs |
| `/design-review` | Validate design documents |
| `/reverse-document` | Generate docs from code |

### Team Orchestration
| Skill | Purpose |
|-------|---------|
| `/team-combat` | Combat feature end-to-end |
| `/team-narrative` | Narrative content pipeline |
| `/team-level` | Level/area creation |
| `/team-ui` | UI feature pipeline |
| `/team-audio` | Audio pipeline |
| `/team-polish` | Feature hardening |

### Quality Assurance
| Skill | Purpose |
|-------|---------|
| `/code-review` | Architectural code review |
| `/bug-report` | Structured bug reporting |
| `/balance-check` | Game balance analysis |
| `/playtest-report` | Playtest feedback analysis |

### Operations
| Skill | Purpose |
|-------|---------|
| `/hotfix` | Emergency fix workflow |
| `/localize` | Localization pipeline |
| `/tech-debt` | Technical debt tracking |
| `/asset-audit` | Asset compliance audit |

### Decision Making
| Skill | Purpose |
|-------|---------|
| `/architecture-decision` | Create ADRs |
| `/estimate` | Task effort estimation |
| `/scope-check` | Scope creep detection |
| `/prototype` | Rapid mechanic validation |

### Assessment
| Skill | Purpose |
|-------|---------|
| `/retrospective` | Sprint/milestone retrospective |
| `/patch-notes` | Player-facing patch notes |
| `/changelog` | Internal changelog generation |

## Collaboration Protocol

Every workflow follows:

```
Question → Options → Decision → Draft → Approval
```

- Ask "May I write to [path]?" before file creation
- Show drafts before requesting approval
- Incremental writes survive session disruption
- Specialist agents provide domain expertise, user decides

## Next Steps

1. Run `/start` for first-time onboarding
2. Run `/brainstorm open` to create a game concept
3. Use `/map-systems` to decompose into systems
4. Use `/design-system [system]` to write GDDs
5. Run `/sprint-plan new` to plan first sprint