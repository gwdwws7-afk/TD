# P11.1 HUD, Typography, And Tower Identity Standard

> Status: implemented and MCP-verified
> Scope: runtime combat HUD, build bar, tower upgrade panel, 1280x720 and 960x540

## Product Intent

P11.1 turns the combat UI from a functional prototype into a repeatable visual system. The battlefield remains the dominant surface; icons reduce reading load, typography establishes priority, and each tower must be recognizable without relying on its name or hue alone.

## Typography Ladder

| Role | Base size | Weight | Use |
|---|---:|---|---|
| Screen title | 20 | Bold | results, formation, campaign profile |
| Section title | 16 | Bold | mission intel and major modal sections |
| Panel title | 14 | Bold | HUD brand, tower title, focused readouts |
| Metric/action | 12 | Bold | wave, integrity, budget, primary buttons |
| Body | 11 | Normal | statistics and explanatory combat copy |
| Caption | 10 | Bold/Normal | tactical feed, tags, secondary labels |

Rules:

- One panel uses at most three adjacent tiers.
- Combat numerals are at least the metric tier and are never shown in caption size.
- All live combat labels use short uppercase tokens; prose belongs in prep, codex, or recap surfaces.
- Large-text accessibility adds one point to the same ladder instead of creating independent sizes.
- Text keeps zero letter spacing and may reduce only to 9 px through best-fit at 960x540.

## HUD Icon Pack

The source-of-truth builder is `tools/build_p11_hud_icon_pack.py`. It exports 128x128 transparent PNGs under `Assets/Resources/Art/UI/P11` and a visual proof sheet under `output/p11`.

Core combat icons: wave, integrity, budget, build, damage branch, utility branch, route, enemy, speed, and pause.

Rules:

- Icons use a dark industrial badge, a pale primary silhouette, and one semantic accent.
- The central symbol must remain legible at 20 px; decoration must not carry meaning.
- UI code loads icons through Resources and preserves aspect ratio.
- Text remains available beside critical economy and integrity icons; icons never become the only carrier of a changing number.

## Eight-Tower Identity Matrix

| Tower | Identity color | Shape marker | Combat promise |
|---|---|---|---|
| Rail Lancer | Signal blue | Lance | priority pierce and armor pressure |
| Cinder Mortar | Burnt orange | Blast ring | area burst and packed-wave clear |
| Frost Coil | Ice cyan | Snowflake | direct slow and tempo control |
| Arc Welder | Electric teal | Linked nodes | chain damage and formation punishment |
| Siege Drill | Brass ochre | Cracked diamond | heavy break and structural pressure |
| Ember Flak | Vermilion | Three pellets | rapid swarm suppression |
| Resonance Beacon | Relay green | Broadcast arcs | support, marks, and resonance charge |
| Grav Snare | Gravity violet | Concentric well | area control and attrition |

Each tower button combines three independent signals: the production tower silhouette, its fixed identity color, and its role marker. Selection may brighten the color and frame, but never changes the assigned hue or marker. The same identity appears in prebattle formation, the live build bar, and the focused tower panel.

## Runtime Acceptance

- Wave, integrity, and budget metrics display their formal icons without reducing number legibility.
- All unlocked tower build buttons display the correct portrait, role color, hotkey, and cost.
- The L20 formation screen displays all eight tower identities with matching selected, available, and locked states.
- Selected, affordable, and unavailable states remain distinguishable at normal and colorblind settings.
- Damage and utility upgrade actions have different symbols and fixed branch colors.
- No P11 icon is missing from Resources, stretched, or clipped.
- HUD and build bar remain inside bounds with no overlap or text overflow at 1280x720 and 960x540.
- The generated proof sheet and MCP screenshots are retained as visual regression references.

## Verification Record

- `tools/td_mcp_p111_visual_audit.ps1` passes at 1280x720 and 960x540 with clean Console, bounds, overlap, text-fit, resource, typography, and identity checks.
- L20 formation verifies all eight portraits and identity states in one view.
- L09 tower focus verifies the Cinder Mortar portrait plus damage and utility branch icons in the live upgrade panel.
- Reference captures are stored under `output/playtest/p11`; the generated pack proof sheet is stored under `output/p11`.
