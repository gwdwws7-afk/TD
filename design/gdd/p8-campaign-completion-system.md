# P8 Campaign Completion System

> Status: P8.6 implemented and MCP-verified  
> Date: 2026-07-17  
> Source plan: `full-project-plan-emberline-defense-v2.0.md`

## 1. Goal

Turn the existing 20-level data skeleton into a player-facing campaign loop:

1. Read mission intelligence before deployment.
2. Enter only unlocked missions through a formal campaign board.
3. Record clears and best performance persistently.
4. Return from results to retry, mission select, or the next mission.
5. Carry the P7 counter matrix into pre-battle planning.

## 2. P8.1 Runtime Contract

### Campaign progression

- Level 1 is unlocked for a new profile.
- Clearing level N unlocks level N+1.
- Defeat records an attempt but does not unlock progression.
- Replays never lower a previous best result.
- Existing developer-selected levels are migrated as unlocked so current test routes remain usable.

### Mission mastery

Each victory can earn three independent mastery marks:

1. Clear the mission.
2. Finish with at least 50% line integrity.
3. Finish with tactical score 75 or higher.

Stored records per level:

- Clear state
- Attempts
- Best mastery, 0 to 3
- Best tactical score
- Best remaining integrity

### Mission board

The campaign board exposes all 20 missions in four chapter rows. Each mission shows one of four states:

- Locked
- Open
- Current
- Cleared with best mastery

The selected mission reads its real campaign and wave JSON to show:

- Chapter, map and tactical hook
- Wave and route scope
- Dominant enemy composition
- Tactical threat traits
- Recommended available towers
- Recommended P7 resonance command posture
- New tower or enemy arrivals
- Previous best record and mastery targets

### Result flow

The result panel has three explicit actions:

- Retry
- Missions
- Next Mission

`Next Mission` is enabled only after victory when the following mission is unlocked.

## 3. Data Ownership

- Campaign structure: `Assets/Resources/Data/campaign/campaign_main_v1.json`
- Mission waves: `Assets/Resources/Data/waves/*.json`
- Persistent progression: `TDCampaignProgression`
- Runtime presentation and result integration: `TDGameManager`
- Automated acceptance: `tools/td_mcp_p8_campaign_audit.ps1`

No mission intelligence is maintained as duplicate hand-authored UI data.

## 4. Acceptance Gates

P8.1 is accepted when all of the following pass:

1. 20 mission buttons exist and remain inside the viewport.
2. A new profile exposes only level 1.
3. Defeat preserves the lock boundary.
4. First clear unlocks exactly the next mission.
5. Lower replay results cannot reduce a best record.
6. All 20 wave sets produce valid mission intelligence.
7. Mission text fits for all 20 selections.
8. Defeat and victory result layouts both fit with three actions.
9. Existing P0-P7 MCP hard checks remain green.

## 5. Next P8 Slices

### P8.2 Mission contracts - completed

- Every mission owns one optional contract medal, independent from the three mastery marks.
- A contract requires victory and evaluates one live P6/P7 metric.
- Contract completion is persistent and cannot be removed by a weaker replay.
- Every mission exposes at least one mutator in the briefing and battle HUD.
- Mutators can change starting budget/integrity, enemy HP/speed/armor, rewards, or resonance gain.
- Enemy changes use per-spawn clones so the global catalog and later missions remain isolated.
- Contract state is visible before deployment, during combat, in tactical events, and on results.
- Campaign totals now report `contracts completed / 20` beside clears and mastery.

P8.2 acceptance evidence:

- `output/playtest/p82_contract_audit/p82_mission_board.json`
- `output/playtest/p82_contract_audit/p82_mission_board.png`
- `output/playtest/p82_contract_audit/p82_contract_result.json`
- `output/playtest/p82_contract_audit/p82_contract_result.png`
- `output/playtest/p82_modifier_samples/`

### P8.3 Pre-battle formation

- Each mission stores an independent loadout of one to four unique tower IDs.
- The mission board opens a full pre-battle formation view before deployment. The chosen loadout becomes build slots 1-4 and directly gates placement, hotkeys, and debug builds.
- Initial deployment cannot dismiss the mission board through Back; formation confirmation is the only player-facing route into combat.
- Auto Fit exhaustively compares every valid four-tower combination in the unlocked pool and all currently available doctrines. Ties are deterministic.
- Counter Fit is a 0-100 score built from live mission wave and enemy tags, not authored display copy:
  - threat coverage: 50% (`speed`, `swarm`, `armor`, `attrition`);
  - P7 specialization trait coverage: 30%;
  - doctrine and specialization affinity: 20%.
- Before resonance unlocks, Counter Fit rebalances to 62.5% threat coverage and 37.5% matrix coverage. Doctrine controls remain visibly locked until campaign L16.
- Three resonance doctrines are available from L16:
  - Adaptive: +4% command power when the chosen command matches the live threat package;
  - Ember: +10% tower damage while Ember Surge is active;
  - Fracture: +10% Fracture Mark exposure damage.
- Formation and doctrine persist through campaign snapshot export/import and remain monotonic with the existing mastery and contract record.
- A current run locks formation edits after its first build. Reviewing the formation remains available during prep without allowing a fifth tower type.

P8.3 acceptance evidence:

- `output/playtest/p83_formation_audit_final/p83_formation.json`
- `output/playtest/p83_formation_audit_final/p83_formation.png`
- `output/playtest/p83_formation_audit_final/p83_ember_battle.json`
- `output/playtest/p83_formation_audit_final/p83_ember_battle.png`
- `output/playtest/p83_formation_audit_final/p83_fracture_battle.json`
- `output/playtest/p83_formation_audit_final/p83_fracture_battle.png`

### P8.4 Chapter completion - completed

#### Chapter mastery and rewards

- Every chapter row reports clears, stars, contracts and reward state from persistent mission records.
- A chapter is cleared at 5/5 mission clears and fully mastered at 15/15 stars plus 5/5 contracts.
- Clearing the fifth mission auto-claims the chapter reward. Completed legacy profiles can claim a missing reward from the mission board.
- Claimed rewards are permanent account-wide deployment modifiers and stack with mission mutators:
  - Chapter A, Forward Reserves: +10 starting budget.
  - Chapter B, Hardened Line: +1 starting integrity.
  - Chapter C, Tuned Relay: +5% resonance gain.
  - Chapter D, Emberline Charter: +10 starting budget and +1 integrity on replays.

#### Campaign completion

- A victorious L20 result with 20/20 clears switches from the run recap to the campaign archive.
- The archive reports all chapter records, total stars/contracts, perfect missions, deployments, average best tactical score and active legacy bonuses.
- Final rank weights stars at 60%, contracts at 30% and fully mastered chapters at 10%: S at 98%+, A at 85%+, B at 70%+, otherwise C after campaign clear.
- The final action opens the campaign archive mission board instead of pointing at a nonexistent L21.

#### Player save control

- Campaign Profile is available from the mission board and exposes archive totals and active bonuses.
- Copy Save writes a versioned `EMBERLINE-SAVE-1:<checksum>:<payload>` portable code to the system clipboard.
- Import validates prefix, Base64 payload, save version, exact 20-record shape, value ranges, formations, doctrines and known chapter reward IDs.
- Import and Reset Profile both require a second confirmation click.
- Reset clears campaign clears, stars, contracts, formations, doctrines and chapter rewards, then returns to L01.
- Codex discovery keys remain separate and are explicitly labeled as such.

P8.4 acceptance evidence:

- `output/playtest/p84_campaign_audit_final/p84_chapter_mastery.json`
- `output/playtest/p84_campaign_audit_final/p84_chapter_mastery.png`
- `output/playtest/p84_campaign_audit_final/p84_campaign_profile.json`
- `output/playtest/p84_campaign_audit_final/p84_campaign_profile.png`
- `output/playtest/p84_campaign_audit_final/p84_campaign_complete.json`
- `output/playtest/p84_campaign_audit_final/p84_campaign_complete.png`

### P8.5 Campaign challenge ladder - completed

#### Difficulty unlocks

- Standard is always available and preserves the authored mission rules.
- Veteran unlocks independently for every chapter after that chapter reaches 5/5 clears.
- Ember Trial unlocks for the full campaign after all 20 missions are cleared once.
- Every mission stores its selected difficulty and highest cleared difficulty. A lower replay cannot reduce the record.

#### Runtime composition

Runtime rules compose in this deterministic order:

1. Mission mutator.
2. Selected difficulty modifier.
3. Chapter challenge remix on Veteran and Ember Trial.
4. Claimed chapter legacy rewards.

| Tier | Enemy pressure | Starting pressure | Combat income |
|---|---|---|---|
| Standard | Authored baseline | Authored baseline | Authored baseline |
| Veteran | HP x1.15, speed x1.05, armor +1 | Budget -10 | Rewards x1.10 |
| Ember Trial | HP x1.30, speed x1.10, armor +2 | Budget -20, integrity -2 | Rewards x1.25, resonance x1.10 |

| Chapter | Remix | Effect |
|---|---|---|
| A | Rapid Escalation | Speed x1.06, budget -5 |
| B | Plated Advance | HP x1.05, armor +1 |
| C | Crossfire Tax | Budget -8, rewards x0.95 |
| D | Resonance Storm | HP x1.08, speed x1.04, resonance x1.15 |

#### Player-facing feedback

- Prebattle Formation adds a three-segment difficulty selector with visible lock states.
- The selected tier shows Counter Fit, compact rule deltas, and the active chapter remix before deployment.
- Battle HUD rules, mission records, chapter rows, campaign totals, Campaign Profile and run results expose challenge state.
- A completed 20/20 Ember Trial record changes the final L20 archive to `CAMPAIGN PERFECTED`.
- Portable saves include difficulty preference and highest difficulty clear for every mission.

P8.5 acceptance evidence:

- `output/playtest/p85_difficulty_audit_final/p85_standard_formation.json`
- `output/playtest/p85_difficulty_audit_final/p85_standard_formation.png`
- `output/playtest/p85_difficulty_audit_final/p85_veteran_formation.json`
- `output/playtest/p85_difficulty_audit_final/p85_veteran_formation.png`
- `output/playtest/p85_difficulty_audit_final/p85_embertrial_formation.json`
- `output/playtest/p85_difficulty_audit_final/p85_embertrial_formation.png`
- `output/playtest/p85_difficulty_audit_final/p85_campaign_perfected.json`
- `output/playtest/p85_difficulty_audit_final/p85_campaign_perfected.png`

### P8.6 Save slots, cloud adaptation and scenario exams - completed

#### Persistence contract

- Three independent campaign slots use separate PlayerPrefs namespaces.
- Existing P8.5 single-slot data migrates once into slot 1 without deleting the legacy keys.
- Portable saves now emit `EMBERLINE-SAVE-2`; version 1 codes remain importable and migrate to version 2.
- Cloud adaptation uses a platform-neutral envelope containing slot ID, device ID, revision, UTC modification time and a validated portable snapshot.
- Conflict resolution supports Keep Local, Use Cloud and Merge. Merge is monotonic for clears, mastery, contracts and difficulty records; the newer snapshot owns formation, doctrine and difficulty preference.
- Campaign Profile exposes slot switching, cloud copy and safe merge controls.

#### Scenario decision layer

| Map | Mechanic | Decision changed |
|---|---|---|
| Grayline Junction | Signal Gate | Spend budget and a charge to delay all groups in the next wave. |
| Ashfall Depot | Reserve Train | Invest now for delayed budget; choose whether to wait or dispatch early. |
| Split Switch Canyon | Canyon Switch | Divert center, split and cross traffic onto the selected route. |
| Hollow Kiln Basin | Kiln Purge | Spend a limited combat charge on a high-density damage, break and stagger window. |
| Last Ember Terminus | Phase Breaker | Suppress one elite/Boss health-threshold overdrive and reinforcement. |

All 20 wave sets are loader-gated to include Introduce, Reinforce and Exam phases. L05, L09, L13, L17 and L20 carry milestone-exam metadata and mechanism-specific failure focus. Run recap records scenario use/opportunity conversion and the third recommendation calls out underused exam mechanics.

P8.6 acceptance evidence:

- `output/playtest/p86_audit.json`
- `output/playtest/p86_profile_ui.json`
- `output/playtest/p86_profile_ui.png`
- `output/playtest/p86_runtime_l01.json`
- `output/playtest/p86_runtime_l05.json`
- `output/playtest/p86_runtime_l09.json`
- `output/playtest/p86_runtime_l13.json`
- `output/playtest/p86_runtime_l17.json`
- `output/playtest/p86_runtime_l20.json`
