# P8 Campaign Completion

## P8.1 Completed

- Persistent 20-level clear, attempt and best-record storage
- Sequential unlock rules with legacy developer-route migration
- Three-mark mission mastery rating
- Four-chapter mission board with 20 selectable mission states
- Mission intel generated from campaign, wave and enemy data
- P7 tower and resonance counter recommendations in mission briefing
- Retry, Missions and Next Mission result actions
- MCP deployment compatibility for all existing automated playtests
- Dedicated progression, content and UI audit

## Verification Evidence

- `output/playtest/p8_campaign_audit/p8_campaign_audit_summary.json`
- `output/playtest/p8_campaign_audit/p8_campaign_board.png`
- `output/playtest/p8_regression/p8_result_flow.json`
- `output/playtest/p8_regression/p8_result_flow.png`
- `output/playtest/p8_regression/p8_victory_flow.json`
- `output/playtest/p8_regression/p8_victory_flow.png`

## Next

## P8.2 Completed

- 20 unique optional mission contracts backed by existing P6/P7 run metrics
- Persistent contract medals with defeat protection and monotonic replay behavior
- 20 visible mission mutators spanning enemy, economy, line, and resonance rules
- Spawn-time enemy cloning to prevent catalog or cross-mission mutation
- Contract and mutator presentation in briefing, battle HUD, tactical feed, results, and run logs
- Dedicated MCP content, persistence, boundary, clone-isolation, reward-scaling, and UI audits

## P8.2 Verification Evidence

- `output/playtest/p82_contract_audit/p82_mission_board.json`
- `output/playtest/p82_contract_audit/p82_mission_board.png`
- `output/playtest/p82_contract_audit/p82_contract_result.json`
- `output/playtest/p82_contract_audit/p82_contract_result.png`
- `output/playtest/p82_modifier_samples/l01_budget/summary.json`
- `output/playtest/p82_modifier_samples/l16_resonance/summary.json`
- `output/playtest/p82_modifier_samples/l20_final/summary.json`

## P8.3 Complete

- Added persistent per-mission tower formations with a hard four-slot cap.
- Added a dedicated pre-battle formation screen with unlocked/locked tower states, slot ordering, Auto Fit, and deployment lock feedback.
- First deployment now requires formation confirmation; the mission-board Back action cannot bypass the loadout step.
- Added Adaptive, Ember, and Fracture doctrines. Doctrine selection is gated by the campaign L16 resonance unlock.
- Added a real 0-100 Counter Fit score from mission threat categories, P7 specialization tags, and resonance affinity.
- Active formations now drive the battle build bar, numeric hotkeys, placement validation, and MCP builds.
- Added live doctrine combat power: Adaptive +4% on a threat match, Ember +10% Ember damage, Fracture +10% marked exposure damage.
- Extended campaign snapshots with formation and doctrine persistence.
- Extended the shared MCP runner with formation injection, formation-screen capture, profile preservation, P8.3 reports, and P8.3 audits.

Acceptance:

- All 20 missions produce a valid Auto Fit loadout and bounded Counter Fit score.
- All formation text fits at the runtime reference viewport.
- Four-slot normalization, duplicate rejection, snapshot round trip, active build restriction, and doctrine power audits pass.
- Ember and Fracture live runs both report `livePower=1.10` and one empowered command.
- Evidence is under `output/playtest/p83_formation_audit_final/`.

## P8.4 Complete

- Added per-chapter clear, star, contract, mastery and reward summaries to the mission board and campaign profile.
- Added four persistent chapter rewards with real starting-budget, integrity and resonance-gain effects.
- Chapter-end victory auto-claims its reward; migrated completed chapters can claim from the board.
- Added the L20 campaign archive with campaign rank, four chapter records, perfect mission count, deployments, average best score, legacy bonuses and targeted cleanup goals.
- Added player-facing Copy Save, Import and Reset Profile controls. Import and reset use explicit two-step confirmation.
- Added strict versioned portable-save validation and preservation of mission formations, doctrines and claimed rewards.
- Added P8.4 runtime reports, deterministic fixtures, tamper/unknown-reward rejection checks, clipboard/confirmation checks and profile restoration.

Acceptance:

- Chapter A fixture reaches 5/5 clears, 15/15 stars, 5/5 contracts, full mastery and an active reward.
- All four reward effects stack to +20 budget, +2 integrity and x1.05 resonance gain.
- Portable save preview, reset and round trip preserve chapter reward and formation state.
- Modified Base64 payloads and unknown reward IDs are rejected.
- L20 fixture reports 20/20 clears, 60/60 stars, 20/20 contracts, four mastered chapters and rank S.
- Mission board, campaign profile and final archive have zero bounds, overlap and text-fit failures.
- Evidence is under `output/playtest/p84_campaign_audit_final/`.

## P8.5 Complete

- Added Standard, Veteran and Ember Trial as data-driven campaign difficulty tiers.
- Veteran unlocks by cleared chapter; Ember Trial unlocks after the first 20/20 campaign clear.
- Added four chapter-specific challenge remixes that activate above Standard.
- Runtime composition now applies mission, difficulty, remix and claimed legacy effects in a fixed order.
- Added per-mission difficulty preference and monotonic highest-difficulty records to PlayerPrefs, snapshots and portable saves.
- Added prebattle segmented difficulty controls, lock feedback, Counter Fit context and compact modifier/remix previews.
- Added challenge records to mission buttons, intel, chapter rows, Campaign Profile, results and the final archive.
- Added a distinct 20/20 Ember Trial `CAMPAIGN PERFECTED` result.
- Extended the shared MCP runner and added `tools/td_mcp_p85_difficulty_audit.ps1`.

Acceptance:

- Standard, Veteran and Ember Trial L01 runtime signatures match their authored values.
- Initial, chapter-clear and campaign-clear lock boundaries pass.
- Difficulty preference, monotonic records and portable round trip pass.
- All P8.5 formation, archive and result text passes bounds, overlap and text-fit checks.
- The L20 fixture reports Veteran 20/20, Ember Trial 20/20 and `CAMPAIGN PERFECTED`.
- Player campaign progress is restored after every MCP fixture.
- Evidence is under `output/playtest/p85_difficulty_audit_final/`.

## P8.6 Complete

- Added three isolated campaign save slots and one-time migration from the legacy single-slot namespace.
- Upgraded portable saves to version 2 while keeping version 1 import compatibility.
- Added platform-neutral cloud envelopes with revision/device/time metadata and Keep Local, Use Cloud and monotonic Merge strategies.
- Added save-slot, cloud copy and cloud merge controls to Campaign Profile.
- Added five data-driven map mechanics that alter deployment timing, economy, routing, combat device timing and Boss phases.
- Loader validation now requires complete Introduce, Reinforce and Exam grammar for all 20 missions.
- L05, L09, L13, L17 and L20 are marked as milestone exams with mechanism-specific failure focus and authored decision hints.
- Added P8.6 reports, destructive-but-restored persistence audits and six live runtime mechanism probes.

Acceptance:

- Three-slot isolation, cloud preview, all three conflict strategies and legacy migration pass.
- Five unique mechanics, 20/20 scenario grammar and all five exam records pass.
- Signal Gate, Reserve Train, Canyon Switch, Kiln Purge, elite phase and final Boss phase probes all report `applied=True`.
- Campaign Profile and live scenario HUD pass bounds, overlap and text-fit checks.
- Player campaign content is restored after every MCP fixture.
- Evidence is under `output/playtest/p86_*.json` and `output/playtest/p86_runtime_l*/`.

## Next

P9 owns feedback, accessibility, controls and interactive onboarding. P10 owns campaign meta progression, automated difficulty reporting and release validation.
