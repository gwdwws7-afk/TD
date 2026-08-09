# P9 Presentation, Controls And Tutorial

> Status: P9.1-P9.4 implemented and MCP-verified
> Date: 2026-07-17

## Product Goal

Combat information must communicate urgency without drowning tactical decisions. Routine hits stay local and quiet, tactical effects identify counter play, and critical events can preempt lower-priority presentation. The simulation remains controllable at every speed and first-run learning requires player actions rather than passive prompt dismissal.

## Combat Feedback Language

| Event | Shape | Tier | Audio behavior |
|---|---|---|---|
| Hit | `[+]` | Routine | Short, damage-pitched tone; globally throttled. |
| Armor break | `[#]` | Tactical | Low crack tone; transition-only and throttled. |
| Slow | `[v]` | Tactical | Descending control tone; transition-only and throttled. |
| Specialization | `[*]` | Tactical/Critical | Distinct damage/utility proc tones; matrix matches promote the tier. |
| Resonance | `[R]` | Tactical/Critical | Command signature; threat matches promote the tier. |
| Leak | `[!]` | Critical | Preempts routine audio and opens the defense-breach cue. |

The floating-signal pool is capped at 18. Routine hit visuals and audio use separate cooldowns. Critical audio stops lower-priority playback before sounding.

## Tactical Micro-Cinematics

- Wave dispatch: phase, readiness and route summary.
- Boss entry: named threat and lane warning.
- Boss phase: overdrive/reinforcement warning or Phase Breaker cancellation.
- Defense breach: leaking enemy, exit segment and remaining integrity.
- Critical defense: one-time warning below 35% starting integrity.
- Every cue is 0.65-1.5 seconds, uses unscaled UI time and does not block input or alter simulation state.

## Controls And Accessibility

- Bottom segmented controls provide pause, 1x, 2x and 3x.
- `P` toggles pause; minus/plus step speed down/up.
- Restart restores a non-zero time scale before scene reload.
- Pause freezes combat, prep countdowns, reinforcements and Boss phase logic while presentation remains responsive.
- Shape markers are enabled by default and color is supplementary.
- `Aa` large-text mode is enabled by default at 768p or below and persisted globally.
- Fixed Game View MCP probes cover 1920x1080, 1366x768 and 1280x720.

## Interactive First Run

The six persisted steps are:

1. Deploy a tower on a build pad.
2. Keep its range preview visible long enough to inspect coverage.
3. Dispatch the first wave.
4. Confirm the armor/break visual language.
5. Buy a Damage or Utility upgrade during prep.
6. Use the map scenario command during Reinforce or Exam.

Progress and completion are isolated by the active P8.6 save slot. Skip writes the same terminal completion state. An interrupted run resumes from the last completed action. The legacy opening guide collapses to a short training status while the interactive panel is active.

## Acceptance Evidence

- `output/playtest/p9_presentation_audit.json`: six feedback channels, three cue types, pause/speed, accessibility, tutorial flow/skip and UI checks pass.
- `output/playtest/p9_live_feedback.json`: real Rail/Frost combat produced hit, armor break, slow, specialization, resonance and leak events.
- `output/playtest/p9_1920x1080.json`, `p9_1366x768.json`, `p9_1280x720.json`: bounds, overlap, text fit and Console checks pass at exact screenshot dimensions.

