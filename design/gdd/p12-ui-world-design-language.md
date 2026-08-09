# P12 UI World Design Language

> Status: implementation baseline
> Fiction: Emberline Frontier rail-defense field equipment
> Scope: combat HUD, alerts, actions, prep, campaign, formation, and results

## Design Thesis

The interface is equipment issued by the Line Defense Bureau. It should feel bolted to a railway command desk, not drawn as floating software panels. Every visible decoration must read as a structural part, signal device, gauge, warning mark, or field repair.

## Material Stack

1. Coal-black steel is the primary load-bearing surface.
2. Gunmetal and smoked instrument glass hold readable information.
3. Tarnished brass appears on fasteners, dividers, and durable control edges.
4. Ember orange marks urgent action, heat, breach, and commitment.
5. Instrument cyan marks route intelligence, timing, and neutral system state.
6. Relay green marks integrity, support, and scenario devices.

Rust, soot, scratches, and glow remain restrained. Material wear supports the frontier setting but never lowers text contrast.

## Component Grammar

- **Command frame:** riveted iron housing with a narrow brass rail; used for the primary HUD and major modal surfaces.
- **Broadcast frame:** low emergency signal housing with ember warning ends; used for Boss, breach, and defense-loss alerts.
- **Gauge cell:** inset dark meter with one semantic color rail; used for wave, integrity, and budget.
- **Action bezel:** chamfered switch housing with an amber state rail; used for deploy, start wave, route switch, and confirmation.
- **Control rail:** compact mounted strip; used for playback, accessibility, build roster, and tactical feed.
- **Identity plate:** short uppercase command wording with an icon; prose is kept out of the live combat layer.

## Information Hierarchy

- Battlefield silhouettes remain the dominant visual layer.
- Integrity, wave timing, immediate threat, and the next valid action receive the strongest contrast.
- Brass is structural, not a generic highlight color.
- Ember is reserved for commitment or danger and cannot become the default selected state.
- Cyan and green must remain distinguishable without relying on hue alone; icons and labels remain present.
- Live combat uses short commands. Explanatory prose belongs in prep, tutorial, codex, or recap surfaces.

## Typography

`Barlow Semi Condensed` is the runtime Latin command face. It is bundled under the SIL Open Font License in `Assets/Resources/Fonts/BarlowSemiCondensed`.

- Bold uppercase is reserved for identity, alert, and action labels.
- Body text uses normal case where scanning matters.
- Letter spacing remains zero.
- Large-text mode adds one point without changing the hierarchy.
- Text never sits on top of a frame rail, bolt, warning lamp, or decorative corner.

## Motion And Feedback

- Alerts enter like a signal relay engaging: short, direct, and mechanically weighted.
- Buttons brighten their state rail before the whole housing.
- Disabled controls keep their physical frame visible while lowering the lamp and label intensity.
- Persistent pulsing is reserved for unresolved critical action.
- Decorative motion cannot compete with enemy movement or projectile timing.

## Prohibited Patterns

- Flat translucent rectangles with a single colored top line.
- Generic dashboard cards, rounded pills, or floating glass panels.
- Neon cyberpunk glow, fantasy filigree, or ornamental gears without function.
- Paragraphs over live combat.
- Color as the only carrier of state.
- Frames whose borders consume the text-safe interior at 960x540.

## Asset Sources

- Runtime frame sprites: `Assets/Resources/Art/UI/P12`.
- Deterministic alpha crop builder: `tools/build_p12_ui_skin_from_existing.ps1`.
- Image 2.0 high-quality generation pipeline: `tools/generate_p12_ui_skin.ps1` using the local ignored API key.
- Runtime skin implementation: `Assets/Scripts/TowerDefense/TDUiWorldSkin.cs`.

## Acceptance

- The primary HUD, Boss alert, contextual action, build rail, and playback rail read as one equipment family in a silent screenshot.
- Major campaign, formation, and result surfaces use the same materials and action grammar.
- P11/P12 resource, typography, bounds, overlap, and text-fit audits pass at 1280x720 and 960x540.
- Disabled actions remain recognizable without appearing available.
- No critical text intersects decorative frame art.
