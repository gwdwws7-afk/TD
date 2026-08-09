# P9-P10 Release Roadmap

> Date: 2026-07-17
> Entry gate: P9.1-P9.4 presentation, controls and tutorial audit green
> Product target: exam failures are explainable and replay plans produce visibly different outcomes.

## Priority Order

1. P9.1 combat feedback hierarchy
2. P9.2 tactical micro-cinematics
3. P9.3 controls, low-resolution layout and accessibility
4. P9.4 skippable interactive first-run tutorial
5. P10.1 campaign meta loop
6. P10.2 20-level automated balance report
7. P10.3 save recovery, performance and release build validation
8. P10.4 release candidate gate

## P9.1 Combat Feedback Hierarchy

Status: Complete on 2026-07-17.

Deliverables:

- Six distinct audiovisual signatures: hit, armor break, slow, specialization proc, resonance match and leak.
- Three intensity tiers for routine, tactical and critical events.
- Per-effect colorblind-safe shape markers; color is never the only signal.
- Audio voice budget and priority rules so Boss/leak warnings cannot be masked by routine hits.

Acceptance:

- A muted-video test identifies all six effects from visuals alone.
- An audio-only test separates armor break, resonance and leak.
- Ten-minute stress runs stay inside the configured FX/audio concurrency budget.

## P9.2 Tactical Micro-Cinematics

Status: Complete on 2026-07-17.

Deliverables:

- Short, non-blocking wave transition cue.
- Boss warning and each Boss phase transition cue.
- Critical-defense-loss cue triggered by route heat plus integrity state.
- Skip/fast-forward behavior that never changes simulation timing or wave data.

Acceptance:

- Every cue is under 1.5 seconds and preserves player control unless explicitly paused.
- Repeated events obey cooldowns and do not stack camera motion.
- MCP screenshot probes verify readable framing at 1920x1080, 1366x768 and 1280x720.

## P9.3 Controls And Accessibility

Status: Complete on 2026-07-17.

Deliverables:

- Pause, 1x, 2x and 3x controls with persistent preference.
- Minimum font and control sizes for low-resolution layouts.
- Colorblind mode with shape/pattern reinforcement for routes, resistances and effect types.
- Keyboard, mouse and UI-button parity for wave start, scenario mechanic, resonance and speed controls.

Acceptance:

- No bounds, overlap or text-fit failures at the three target resolutions.
- Pause freezes combat, timers, reinforcements and Boss phases consistently.
- Speed changes preserve deterministic spawn order and results within rounding tolerance.

## P9.4 Interactive First-Run Tutorial

Status: Complete on 2026-07-17.

Deliverables:

- Stateful steps: select pad, build, inspect range, start wave, read armor, upgrade and respond to the first scenario mechanic.
- Input-gated highlights instead of passive text prompts.
- Skip, resume and completed-state persistence per save slot.
- Recovery when a required object disappears or the player opens another panel.

Acceptance:

- Fresh-profile completion, skip and interrupted-resume paths all pass.
- Returning profiles never re-enter mandatory tutorial steps.
- Tutorial restrictions cannot soft-lock wave start or spend campaign resources twice.

## P10.1 Campaign Meta Loop

Status: Complete on 2026-07-17.

Deliverables:

- Explicit campaign unlock graph and rating rewards.
- Enemy/tower codex completion rewards tied to observed behavior, not grind-only counters.
- Long-term growth that expands tactical options without invalidating Standard balance.
- Archive view for chapter exams, best strategy signatures and unlocked replay modifiers.

Acceptance:

- Every reward has an authored source, visible destination and duplicate-claim protection.
- New growth choices produce at least two viable replay plans on each milestone exam.
- Cloud merge rules cover every new progression field.

## P10.2 Automated Balance Report

Status: Complete on 2026-07-17.

Deliverables:

- Deterministic auto-run matrix for 20 levels across Standard, Veteran and Ember Trial.
- At least three formation/doctrine strategies per milestone exam.
- Report: win rate, duration, first leak wave, route heat, tower contribution, scenario conversion and five-axis score.
- Curve alarms for difficulty spikes, flat missions and strategy collapse.

Acceptance:

- 180 baseline runs complete without console errors or stalled waves.
- Standard median success trends downward smoothly without a single unexplained cliff.
- Each milestone exam has at least two meaningfully different successful strategy signatures.

## P10.3 Release Hardening

Deliverables:

- Migration fixtures for every save version and malformed/truncated recovery cases.
- Automatic backup/rollback when active-slot import or cloud resolution fails.
- CPU, allocation, object count and frame-time captures for early, swarm and Boss waves.
- Windows development and release builds with clean startup, restart and quit paths.

Acceptance:

- No supported save fixture loses monotonic progress.
- Corrupt active data restores the last valid backup and reports the recovery.
- Target hardware holds the agreed frame-time budget at the highest authored concurrency.
- Release build completes the 20-level smoke suite without editor-only dependencies.

## P10.4 Release Candidate Gate

The release candidate is accepted only when:

1. All P0-P10 MCP hard checks are green.
2. Five milestone exams explain the top failure route, segment, enemy trait, counter gap and missed scenario opportunity.
3. Replaying each milestone exam with its first recommendation changes at least two measured outcomes.
4. Save migration, cloud conflict, crash recovery and build smoke suites are green.
5. Performance, accessibility and low-resolution evidence is archived with the build.

## Post-P10

P11 should be content expansion only after the release gate is stable: additional Boss variants, optional challenge contracts, localized text/audio, platform cloud adapters and telemetry-backed balance updates. It must not introduce a new progression currency before the existing campaign replay loop proves durable.
