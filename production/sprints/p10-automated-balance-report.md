# P10.2 Automated Balance Report

> Status: Complete on 2026-07-17

## Delivered

- Unity-side deterministic fast-rules simulator driven by shipping campaign and combat data.
- 20 levels x 3 difficulties x 3 strategy identities, totaling 180 baseline runs.
- Raw JSON plus run, curve and milestone CSV exports.
- Markdown report covering win rate, duration, first leak, route heat, tower contribution, scenario conversion and five-axis scores.
- Spike, flat mission, strategy collapse, difficulty inversion and stalled-run alarms.
- Deterministic repeat fingerprint and Unity console release gates.
- P10.2 hard audit integrated into the full P0-P10 campaign regression.

## Baseline Result

- Runs: 180/180 complete, zero stalls.
- Unity console: zero effective errors or warnings.
- Standard: 60/60 wins with a smooth median-score trend.
- Veteran and Ember Trial produce lower score and win-rate bands.
- Milestone exams: 5/5 expose three distinct successful Standard signatures.
- Curve alarms: zero.

## Evidence

- `output/playtest/p102_balance_matrix/p102_audit.json`
- `output/playtest/p102_balance_matrix/p102_balance_report.md`
- `output/playtest/p102_balance_matrix/p102_balance_matrix.json`
- `output/playtest/p102_balance_matrix/p102_runs.csv`
- `output/playtest/p102_balance_matrix/p102_level_curve.csv`
- `output/playtest/p102_balance_matrix/p102_exam_strategies.csv`

## Exit Gate

- A second simulation with the same seed reproduces the matrix fingerprint.
- Standard has no adjacent score cliff and no four-level flat window.
- Standard, Veteran and Ember Trial remain strictly ordered by median score on every level.
- Every milestone exam has at least two successful, meaningfully distinct Standard strategy signatures.
- The generic MCP campaign audit reports `p10.2.audit.pass=True`.

