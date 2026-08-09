# P10.1 Campaign Meta Loop

## Product Rule

Long-term progress expands replay choices. It does not add a currency or a permanent tower damage ladder. Standard remains the authored combat baseline.

## Unlock Graph And Rewards

- Mission unlocks remain explicit and sequential: L01 -> L20. Locked mission buttons display the required previous mission.
- Rating rewards use best campaign stars, so replaying a mission can improve progress without repeating a grind counter.
- Codex rewards use unique observed behaviors. Enemy dossiers require sighting, a trait-relevant interaction and a counter kill. Tower dossiers require a build, both upgrade branches and a specialization proc across runs.
- Every reward is data-authored in `campaign_main_v1.json`, names its source threshold and points to one tactical protocol destination.

## Tactical Protocols

| Protocol | Benefit | Cost | Source |
| --- | --- | --- | --- |
| Standard Charter | No modifier | No modifier | Default |
| Forward Recon | +4 seconds prep | -8 starting budget | 12 stars |
| Salvage Mandate | +12% combat income | +6% enemy health | 30 stars |
| Field Control | +1 scenario charge | +25% scenario cost | 4 enemy dossiers |
| Modular Reserve | +12 starting budget | +8% enemy health | 4 tower dossiers |

The selected protocol persists per mission. This lets milestone exams retain multiple viable plans: earlier setup, scaling economy, extra environment control or immediate build flexibility.

## Archive

The Campaign Profile records each milestone exam's best tactical score, compact tower formation and selected protocol. It also shows star, enemy dossier and tower dossier progress beside unlocked protocol count.

## Save And Cloud Rules

- Claimed reward IDs and unlocked protocol IDs merge by set union.
- Enemy and tower observation flags merge by bitwise OR.
- Per-level protocol choices use the newer profile value, with older values retained for levels absent from the newer profile.
- Claims are idempotent. A reward ID can unlock its destination once.
- All fields are additive members of save v2; missing fields migrate to empty collections and Standard Charter.

## Verification

`tools/td_mcp_playtest.ps1 -PrepareP101Meta -RunP101Audit -PreserveCampaignProgress` verifies authored content, sidegrade tradeoffs, runtime application, duplicate claims, observation merging, snapshot round trips, cloud conflict merging, archive visibility and UI text fit.
