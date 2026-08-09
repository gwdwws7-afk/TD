# Release Readiness Playtest - 2026-07-18

## Decision

The project should continue in its current design direction, but feature expansion should pause. The combat, campaign, counter, progression, scenario, save, and analytics layers are already broad enough for the intended 20-level product. The next milestone is a production-quality vertical slice and release pipeline, not another systemic layer.

Current verdict: **NO-GO for release, GO for P12 production polish**.

## MCP Coverage

- L09: 45-second real-time automated battle at 2x, authored build sites, eight upgrades, live HUD, route switch runtime, lane/tower analytics, and result flow.
- P11.1: 1280x720 and 960x540 HUD/typography checks.
- P11.2: enemy silhouettes, threat markers, status markers, projectile/impact presentation, and dense combat fixture.
- P11.3: tower foundations, build sites, charge indicators, upgrade presentation, world ordering, sprite alpha-hole audit, and L09 route integrity.
- P8.3-P8.6: formation/doctrine, chapter mastery, three difficulties, campaign completion, save slots, cloud merge, migration, and five map mechanics.
- P9: six feedback channels, short cinematics, playback, accessibility, and skippable tutorial.
- P10.2: 20 levels x 3 difficulties x 3 strategies, 180 deterministic fast-rules runs.

Primary evidence is under `output/playtest/release_readiness_20260718`.

## What Is Strong

- The strategic structure is substantial: 8 towers, 16 branch ultimates, 12 enemies, counter traits, doctrines, resonance commands, five scenario mechanics, three difficulties, 20 levels, contracts, mastery, codex, meta rewards, and post-run diagnosis.
- L09 normal combat hides route forecast lines, follows four rail-center splines, reports zero enemy route deviation, and limits building to 12 authored sites.
- The valid L09 automation built 4/4 towers and completed 8/8 upgrades with no console, bounds, overlap, or text-fit error during live combat.
- P8.6 and P9 full audits passed.
- The P10.2 matrix completed 180/180 runs with no stalls, deterministic fingerprint `8885A3AF`, five passed exam gates, and no Unity console issues.

## Release Blockers Found

1. **Resolved in P12.0 - Automation drift:** the default L09 visual-run plan now uses four valid authored sites and validates every requested build and upgrade.
2. **Resolved in P12.0 - Broken capture gate:** recording now checks process completion, exit code, minimum size, decode validity, and playtest assertions.
3. **Resolved in P12.0 - High-system UI overflow:** the L16 resonance forecast uses a compact two-line state and passes text-fit validation.
4. **Resolved in P12.0 - Campaign result overflow:** perfected, failed, and normal result fixtures pass bounds, overlap, and text-fit checks.
5. **Resolved in P12.0 - Cross-system audit mismatch:** P8.4 fixtures reapply mission runtime rules after chapter and campaign rewards.
6. **Resolved in P12.0 - Balance alarm blind spot:** the report now raises all-win, adjacent cliff, and late zero-win streak warnings while retaining a separate hard gate.
7. **Resolved in P12.5.1 - Release project identity and build baseline:** the production scene is `Assets/Scenes/EmberlineBootstrap.unity`; company, product, application identifier and semantic version are formalized; branded icon/startup assets are embedded; and the Windows x64 player passes an automated 20-wave standalone smoke.
8. **Partially resolved in P12.3/P12.5.1 - Production support layers:** localization, font fallback, controller/accessibility settings and launch metadata are integrated. Audio completion, Unity test assemblies, IL2CPP parity, signing, packaging and platform operations remain RC work.

## Quality Gap To The Target

### Gameplay And Depth

The system design is already competitive in breadth and is stronger than the current presentation can communicate. The important remaining gameplay work is authored tuning: route value, build-site value, scenario timing, boss phases, and real-time calibration. A fast-rules pass is not equivalent to 180 rendered playthroughs.

### Battlefield Readability

The hierarchy is functional, but towers and enemies often read as circular emblems placed on the map rather than animated actors occupying the world. Enemy groups collapse into dense black/red clusters, rings compete with bodies, status prose stacks above combat, and several effects do not clearly identify source, target, or timing.

### UI And Feedback

Panels avoid geometric overlap, but 960x540 text is too small and dense for comfortable play. Formation and campaign screens expose internal data well but still resemble operational dashboards. Final results print analytics as paragraphs instead of turning them into a visual story. Normal L09 combat correctly hides colored routes; scenario changes now need a short diegetic switch cue rather than persistent lines.

### Content And Presentation

Five maps support twenty levels structurally, but the four missions sharing each map need authored state changes, encounter staging, props, lighting, and exam-specific events. The five exam levels should become the quality anchors. Audio, motion, camera, transitions, and characterful animation are the largest missing production layers.

### Release Readiness

P12.5.1 now provides a reproducible Windows x64 build and a clean standalone 20-wave smoke with formal identity and branding. The remaining release evidence is IL2CPP parity, signed installer/store packaging, a 20-minute 60 FPS soak, memory stability, save recovery in a player build, crash and analytics transport, third-party credits, legal metadata and store deliverables.

## P12 Plan

### P12.0 - Stabilize The Gate

- Update all automation plans to current authored build sites and validate every requested build/upgrade action.
- Make recording fail on zero-byte/invalid MP4 and capture timestamped start, combat, boss, and result frames through MCP.
- Fix the three text-overflow states and the P8.4/P8.5 legacy-bonus integration assertion.
- Add five-map geometry audits: route-center deviation, path step continuity, build-site road/cliff clearance, occlusion, minimum useful coverage, and persistent-line count.
- Add curve alarms for abrupt win-rate cliffs, all-win flatness, real-time/simulator duration drift, and late-campaign strategy collapse.

Exit gate: all current wrappers pass from a clean profile and every artifact is non-empty.

Verification on 2026-07-18: **PASS**. `tools/td_mcp_p120_release_gate.ps1` completed seven stages in 662.7 seconds with 20 passing playtest summaries, 53 non-empty artifacts, five unique map geometry probes, 180 deterministic balance runs, and a valid decoded L09 MP4. The active campaign profile was preserved and restored. The balance curve remains `REVIEW` with three non-blocking warnings retained for P12.4.

The P12 world-facing UI baseline is also active: black-iron command frames, brass structure, ember alerts, cyan instrumentation, action bezels, control rails, and the bundled Barlow Semi Condensed command font. Runtime rules are documented in `design/gdd/p12-ui-world-design-language.md`.

### P12.1 - L09 Shipping-Quality Vertical Slice

- Replace emblem-like tower/enemy presentation with gameplay-scale silhouettes, idle/move/attack/hit/death animation, grounded shadows, and controlled foundations.
- Rebuild projectile and impact language around source recognition, target response, and threat priority; reduce decorative rings and floating prose.
- Redesign prep, live HUD, upgrade, resonance, route switch, boss warning, and result surfaces using one typography/icon system.
- Turn the five-axis result and three recommendations into charts, lane thumbnails, tower contribution bars, and one replay decision.
- Produce authored music, ambience, UI, tower, enemy, leak, resonance, boss, and result audio layers.

Exit gate: a blind player test can identify tower class, priority threat, route change, resonance opportunity, failure cause, and next strategy without debug text.

### P12.2 - Five Exam Levels

- Apply the L09 standard to L05, L13, L17, and L20.
- Give every exam a unique opening beat, escalation, scenario decision, failure signature, and ending beat.
- Author boss phase changes and environment-device feedback, including state changes visible directly on the map.
- Validate that at least three materially different formations win each exam for different reasons.

Exit gate: each exam is recognizable from a five-second silent clip and produces distinct replay advice.

Verification on 2026-07-19: **PASS**. The five exam identities now own unique opening, escalation, decision, failure and ending beats; scenario devices expose route, charge, activation and Boss-phase state directly on the map; specialization feedback is capped at 14 characters across all maps; and the Standard strategy gate requires three winning formation signatures per exam. `tools/td_mcp_p122_exam_matrix.ps1` completed 15/15 combat and result captures at 1280x720 and 960x540 with clean UI, Console, profile restoration and P12.1/P12.2 audits. Dedicated Image 2.0 device paths and a reproducible generator are present; checked-in industrial art remains the runtime fallback when the external image endpoint is unavailable.

### P12.3 - Campaign Presentation And Accessibility

- Turn Campaign Command into an authored rail-front campaign surface while retaining the current mastery, contract, difficulty, codex, and save depth.
- Replace text dumps with progressive disclosure, icons, comparison views, and controller/keyboard focus states.
- Add Chinese/English localization infrastructure, font fallback, scalable text, color-independent markers, remapping, subtitle/caption controls, and audio sliders.
- Finish first-session teaching with instrumented completion/skip/drop-off events.

Exit gate: 960x540 and 1920x1080 are readable without overflow, and all critical information survives color-blind and large-text modes.

### P12.4 - Full Campaign Calibration

- Run full real-time sessions for L01, L05, L09, L13, L17, and L20, then calibrate the fast-rules simulator against those anchors.
- Tune the Standard curve away from 100% automation flatness and smooth the L13-L14 Ember Trial cliff.
- Audit tower contribution caps, build-site dominance, scenario conversion, first leak, restarts, and completion time.
- Add per-level visual variants and encounter staging without expanding beyond the five-map scope.

Exit gate: difficulty targets match the GDD, no single tower/site dominates, and failures are explainable in playtest interviews.

Verification on 2026-07-21: **PASS**. The resumable P12.4 Release matrix completed 37/37 rendered Unity sessions with clean UI, text and Console evidence, consistent telemetry, three recommendations per result, valid L09/L20 A/B loadout differences, and passing tower/site dominance gates. Median simulator error is 4.2 score points and 14.64 percent duration, with 84.8 percent victory agreement. The deterministic 180-run fast matrix reports no curve alarms. Independent L13/L20 geometry probes confirm 4/4 routes, 12/12 legal and useful tower sites, zero route deviation and no persistent route lines. Economy saturation remains a documented P12.5 soft warning, not a combat-pressure blocker.

### P12.5 - RC And Launch

#### P12.5.0 - Economy Loop

- Taper late combat bounty and wave-clear income while preserving enough opening income to establish a formation.
- Price the first two specialization upgrades for access and make the third ultimate upgrade the major commitment.
- Turn existing scenario commands into phase-scaled, repeat-scaled competing purchases instead of adding another currency or large system.
- Gate victory economy by final-five purchases, ending reserve, saturation timing and complete income/spend telemetry.

Verification on 2026-07-23: **PASS**. The 37-run rendered release matrix finished with 29 victories, zero economy-decision failures, zero early saturation, zero legacy 12-tower/36-upgrade/1000-plus reserve states and zero telemetry failures. Victory reserve peaked at 975, every victory made at least two purchases in the final five waves, and average late spend conversion was 65.6 percent. The 180-run deterministic matrix also hard-passed with fingerprint `3E11A2D4`, five passing exam gates and no curve alarms. The retained P12.4 calibration passes at 5.9 median score error, 16.48 percent median duration error and 81.8 percent victory agreement. Enemy HP and wave pressure were not changed.

#### P12.5.1 - Release Build Baseline

- Replace placeholder project identity and the sample scene with a production bootstrap, semantic version and application identifier.
- Configure formal Windows icon slots and a branded startup background.
- Build Windows x64 through one deterministic editor/MCP or batch-mode entry point.
- Launch the packaged player, deploy L01, complete all 20 waves, validate P12.5.0 economy decision value and exit cleanly.
- Persist machine-readable build, smoke and combined hard-pass evidence.

Verification on 2026-07-23: **PASS**. Unity produced the unsigned Mono Windows x64 baseline with zero build errors or warnings. All eight application icon slots and the startup background are configured. The packaged `Emberline Defense` 0.12.5 player loaded `EmberlineBootstrap`, completed L01 at 20/20 waves with victory and economy decision value, captured no runtime errors, and exited with code 0. The combined `p1251-build-audit-v1` reports build, identity, branding, smoke and hard-pass gates true. IL2CPP parity, signing, packaging, performance and recovery remain subsequent RC gates.

#### P12.5.2 - IL2CPP, Signing, Installer, And Clean-Machine Gate

- Align Mono and IL2CPP release identity and full-mission smoke behavior.
- Apply release texture compression, managed/engine stripping, and a runtime-only stage allowlist.
- Exclude Burst DoNotShip, debug symbols, backups, editor previews, and editor-only Roslyn assemblies.
- Sign every staged PE file, compile a bilingual installer, and emit a SHA-256 manifest.
- Install, launch, complete 20 waves, uninstall, restore PlayerPrefs, and prepare isolated-machine replay.

Verification on 2026-07-23: **ENGINEERING PASS / PRODUCTION RC NO-GO**. Mono and IL2CPP rebuilt with zero errors and zero warnings, then passed the reported 20-wave technical smoke with matching identity, mission and score. The allowlisted IL2CPP stage is 113,115,876 bytes, an 84.2 percent reduction from the P12.5.1 baseline, with zero forbidden artifacts and zero runtime Roslyn assemblies. The 58,150,072-byte Inno Setup package passed test-signature continuity, fresh-root install, IL2CPP launch, full smoke, signed uninstall, registry cleanup and PlayerPrefs restoration. A network-disabled Windows Sandbox bundle is generated, but this host has neither Windows Sandbox nor an external clean VM; it also has no trusted production signing certificate. The engineering audit is green while `shippingSignaturePass`, `cleanMachinePass` and `releaseCandidatePass` correctly remain false. See `design/gdd/p12.5.2-il2cpp-signing-installer.md`.

#### P12.5.3 - Performance, Save Recovery, And Crash Stability

- Add per-slot local checksums, atomic primary/previous recovery files, and a checksummed PlayerPrefs fallback.
- Recover checksum failures through the real campaign initialization path and reject tampered recovery payloads.
- Isolate all three save slots and the selected route during packaged-player automation.
- Record frame pacing, memory and combat density from the actual IL2CPP player at 1x/2x/3x.
- Persist session heartbeats and archive the matching diagnostic after a forced process termination.

Verification on 2026-07-24: **ENGINEERING PASS**. IL2CPP build 3 completed with zero errors and zero warnings, then passed the full L01 packaged smoke with automatic `EnsureInitialized` recovery and exact profile restoration. The L20 matrix sampled 45 real seconds from wave 10 at each shipping speed: average FPS stayed between 119.79 and 119.93, P95 stayed at 8.34 ms, peak reserved memory stayed at 190.88 MB, combat peaked at 38 enemies, and all rows reported zero runtime errors. A forced process termination left a dirty session marker; the next launch archived the matching session, reported prior-session recovery, completed the full smoke, and restored PlayerPrefs and campaign recovery files. Evidence is tied to `GameAssembly.dll` SHA-256 `041DD47D2F00CABA7B047F197762CF495DA0F0DB10B2A5E1053E031256AC57EF`. See `design/gdd/p12.5.3-performance-save-crash-stability.md`.

- Replace the P12.5.2 test certificate with trusted production signing and run the prepared validator on Windows Sandbox or an external clean Windows VM.
- Add edit/play mode test assemblies and expand cloud conflict/migration fixtures into player-build automation.
- Pass a continuous 20-minute same-process soak on minimum and recommended target hardware.
- Add consented, privacy-filtered crash and analytics transport with retry and offline behavior.
- Complete licenses, privacy/terms, third-party credits, store assets, screenshots, trailer capture, support path, rollback, and release checklist sign-offs.

Exit gate: release candidate installs cleanly, survives a full campaign/profile lifecycle, meets performance targets, and has no S1/S2 defects.

## Priority Order

1. P12.0 release blockers and truthful automation.
2. P12.1 L09 shipping-quality vertical slice.
3. P12.2 five exam levels.
4. P12.3 campaign/UI/audio/accessibility production pass.
5. P12.4 full campaign calibration.
6. P12.5 release candidate and launch operations.

No P13 gameplay feature set should begin before P12.1 is accepted.
