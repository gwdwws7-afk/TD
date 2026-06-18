# Tower Mechanic Balance Sheet v1 (Pass 1)

Date: 2026-05-20
Scope: Arc Welder, Siege Drill, Ember Flak, Resonance Beacon, Grav Snare
Owner: Combat + Content

## 1) Parameter Baseline (Cooldown / Duration / Coefficient)

Cooldown is derived from base `shotsPerSecond` in `TDTower.cs`:
`cooldown = 1 / shotsPerSecond`.

| Tower | Base Shots/s | Cooldown (s) | Core Durations (s) | Core Coefficients |
|---|---:|---:|---|---|
| Arc Welder | 0.85 | 1.18 | Chain expose: 1.00 | Chain radius: `max(1.15, aoeRadius * 1.22)`; chain count: `clamp(aoeMaxTargets, 2..5)`; chain damage: `baseDamage * (0.70 * 0.83^(hop-1))`; expose multiplier: `x1.07` |
| Siege Drill | 0.72 | 1.39 | Armor-break duration: 3.00 (heavy) / 2.20 (default) | Armor-break flat: `+5` (armored), `+1` (default) |
| Ember Flak | 1.35 | 0.74 | Primary suppress: 0.30; Splash suppress: 0.18 | Primary min speed multiplier: `0.12`; splash radius: `max(0.88, aoeRadius * 1.30)`; splash damage: `0.30 * baseDamage`; splash min speed multiplier: `0.16` |
| Resonance Beacon | 0.95 | 1.05 | Primary mark: 1.60; primary expose: 1.70; pulse mark: 1.05; pulse expose: 1.05 | Primary expose multiplier: `x1.12`; pulse radius: `max(1.18, aoeRadius * 1.50)`; pulse expose multiplier: `x1.05`; pulse cap: 6 |
| Grav Snare | 0.70 | 1.43 | Primary stagger: 0.24; primary expose: 1.45; pulse stagger: 0.15; pulse expose: 0.90 | Primary min speed multiplier: `0.20`; primary expose: `x1.10`; pulse radius: `max(1.12, aoeRadius * 1.25)`; pulse min speed multiplier: `0.25`; pulse expose: `x1.04`; pulse cap floor: 6 |

## 2) Tuning Intent (Pass 1)

- Arc Welder: keep chain identity, reduce chain burst snowball and improve readability.
- Siege Drill: sharpen anti-armor identity while reducing generic all-target value.
- Ember Flak: keep anti-fast interception, reduce over-suppression lock.
- Resonance Beacon: keep mark orchestration, reduce stacked expose uptime.
- Grav Snare: keep zone control role, reduce hard-lock strength in crowd overlap.

## 3) Level Rhythm Writeback Targets

- L06 (`ashfall_depot_l06_v1`): Arc Welder chain teaching path.
- L07 (`ashfall_depot_l07_v1`): Siege Drill armor-break teaching path.
- L11 (`split_switch_canyon_l11_v1`): Ember Flak anti-fast interception path.
- L12 (`split_switch_canyon_l12_v1`): Resonance Beacon mark-focus-fire path.
- L16 (`hollow_kiln_basin_l16_v1`): Grav Snare control-window path.

Writeback policy:
- Preserve encounter budgets and counts in this pass.
- Update `threatTags` and `hint` to encode mechanic teaching rhythm (introduce -> reinforce -> exam).
- Keep existing map/chapter tags and pressure markers (`exam_peak`, `high_pressure`, `endgame`).
