# Combat Visual And UI Polish Audit

> Status: first full combat polish pass implemented and MCP-verified
> Date: 2026-07-17
> Reference: official Kingdom Rush 5: Alliance Steam and Ironhide captures

## Product Goal

The battlefield is the primary interface. Persistent UI should answer only three questions: current wave, line integrity, and available budget. Forecast, scenario, build, upgrade, tutorial, and result information should appear only while the player can act on it.

## Recorded Baseline

The original L09 recording showed four recurring problems:

- The 520x304 primary HUD, 368x296 wave panel, scenario panel, tactical feed, build bar, playback controls, and upgrade panel could all remain visible together.
- Internal wave goal keys and long prose competed with enemy and route silhouettes.
- Route previews used 0.08-0.18 world-unit lines and the L09 grid path did not follow the painted rail centerlines.
- Routine hits, tactical effects, and leaks all used rectangular text backdrops, flattening event priority.

Official KR5 combat captures keep most of the viewport on the battlefield, use compact corner resources and hero controls, and reserve large presentation for short contextual events. The target is not to copy its skin; it is to match that information discipline while retaining Emberline's forecast, route, matrix, and post-run analytics depth.

## Implemented Hierarchy

| Layer | Before | Polished behavior |
|---|---|---|
| Primary HUD | 520x304 prose panel | 400x78 combat strip; expands to 100/122 only for prep or resonance |
| Wave intel | 368x296 always visible | 330x180 prep-only forecast with compact threat, weakness, route, and readiness |
| Scenario | 450x118 persistent | 330x92; route-switch commands exist only during actionable prep |
| Build bar | Up to 916x78 | Up to 650x62 and hidden outside build windows |
| Tower panel | 344x292 persistent after selection | 300x226; prep or active hover only, always hidden at result |
| Tactical feed | 520x88, three lines | 392x42, one actionable event |
| Playback | 462x52 | 300x44 segmented controls |
| Route preview | 0.08-0.18 width, up to 0.72 alpha | Hidden in player-facing combat; debug audit only |
| Camera | Fixed 5.8 orthographic size | Aspect-aware fit with 4.8 minimum, removing dead framing |
| Hit feedback | Routine black label panels | Text plus outline; tactical tint only; critical leak backdrop retained |

## L09 Route Correction

The split-switch canyon now uses four densely sampled rail-center splines traced against the painted surface: center, left, right, and cross. Enemy movement, lane heat, and route switching share these paths; player-facing combat no longer renders colored path lines. The map uses 12 authored sub-cell build sites instead of random grid recommendations. Each site is positioned against the complete painted rail network and cliff silhouette, while runtime validation also enforces road clearance and rejects every non-authored cell.

## Player-Facing Language

Runtime keys such as `chapter_b_split_switch_canyon_l09_w01_introduce` no longer appear in combat. L09 presents goals such as `READ THE SWITCH`, `COMMIT A ROUTE`, and `HOLD THE SPLIT`. Detailed codex and counter text remains available in prebattle and result surfaces instead of occupying the live battlefield.

## Verification

- L09 1280x720 compile, prep, combat, forced-result, and 45-second 2x visual runs pass UI bounds, overlap, text fit, and Console checks.
- L09 960x540 passes the same layout checks with large-text mode enabled.
- L09 route integrity reports four spline paths, no segment longer than 0.14 world units, zero active colored forecast lines, and at most 0.01 world-unit enemy deviation.
- All 12 L09 build sites pass authored-site and rail-clearance checks; the eight-tower fixture places every foundation on a validated site.
- P9 feedback fixture emits hit, armor break, slow, specialization, resonance, and leak channels; `p9.audit.pass=True` and text overflow is `none`.
- Final four-tower automation builds and upgrades all planned towers after the route/build-cell correction.

## Remaining Gap To KR5

The first pass fixes composition and hierarchy, but production quality still needs bespoke HUD iconography, stronger tower and enemy silhouettes at gameplay scale, more varied impact animation, authored audio layers, animated mission/result transitions, and a less text-dense five-axis result visualization. Those should be treated as an art-direction and motion pass, not another expansion of persistent panels.
