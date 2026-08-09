# P10.2 Automated Balance Report

## Product Rule

The balance matrix is a repeatable decision audit, not a replacement for hands-on play. It reads the shipping campaign and combat data inside Unity, models a consistent baseline pilot, and raises a review gate when authored pressure or strategy outcomes stop behaving as intended.

## Matrix

- 20 campaign levels.
- Standard, Veteran and Ember Trial, including the same level mutators and challenge remixes used at runtime.
- Three strategies per level and difficulty.
- Exactly 180 deterministic runs for one baseline seed.

| Strategy | Doctrine | Upgrade identity | Primary answer |
| --- | --- | --- | --- |
| Focused Fire Bulwark | Ember Surge | Damage | Armor, heavy enemies and Boss phases |
| Control Lattice | Fracture Mark | Utility | Fast, swarm, flank and route pressure |
| Adaptive Counter Network | Adaptive | Mixed | Broad coverage, economy and scenario timing |

Every signature includes doctrine, branch identity, unlocked tower priority and final composition. A signature remains distinct even in early missions with a small tower pool.

## Data Authority

`TDBalanceSimulator` loads the same campaign, map mechanic, chapter remix, level mutator, wave set, enemy catalog, tower base profile, specialization matrix and unlock order used by live play. It allocates build and upgrade budget per wave and evaluates lane pressure against output, control, coverage and counter fit.

The report labels its mode `deterministic_fast_rules_v1`. It does not claim to have rendered 180 real-time sessions. A combat-rule change, a curve alarm or a milestone score shift of five or more points requires a real-time MCP calibration run.

## Metrics

Each run records:

- victory, duration, first leak wave, escapes and integrity;
- route heat and hottest route;
- tower count, upgrades and per-tower damage/control contribution;
- scenario opportunities, uses and conversion;
- coverage, counter, output, economy and command scores;
- total score and strategy signature.

The 20-level curve aggregates three strategy samples per difficulty. Standard median score is the primary authored-curve signal; win rate is intentionally coarser and remains visible beside it.

## Alarms

- `DIFFICULTY_SPIKE`: adjacent Standard median score drops by more than 7.5 points.
- `FLAT_MISSIONS`: a four-level Standard window spans less than one score point.
- `STRATEGY_COLLAPSE`: a milestone exam has fewer than two distinct successful Standard signatures.
- `DIFFICULTY_INVERSION`: a level does not preserve Standard > Veteran > Ember Trial median score.
- `STALLED_RUN`: any matrix cell does not complete.

Warnings keep the report in review. Errors fail the hard gate. The release gate also requires 180 completed runs, zero stalls, five passing milestone exams, deterministic fingerprint repetition and a clean Unity console.

## Artifacts

Run `tools/td_mcp_p102_balance_matrix.ps1 -RefreshScripts` to produce:

- `p102_balance_matrix.json`: complete nested run data;
- `p102_runs.csv`: flat 180-run analysis table;
- `p102_level_curve.csv`: 20-level difficulty curve;
- `p102_exam_strategies.csv`: milestone viability signatures;
- `p102_balance_report.md`: human-readable release report;
- `p102_audit.json`: machine-readable gate result.

