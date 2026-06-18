# TD HD 2D Frame Animation Assets (Batch 1)

Generated at: `Assets/Resources/Art/anim/`

## Included animations

- Towers
  - `tower_rail_lancer_00..05.png` (6 frames)
  - `tower_cinder_mortar_00..05.png` (6 frames)
  - `tower_frost_coil_00..05.png` (6 frames)

- Enemies
  - `enemy_skitter_runner_00..07.png` (8 frames)
  - `enemy_carapace_brute_00..05.png` (6 frames)
  - `enemy_ash_swarm_00..07.png` (8 frames)
  - `enemy_plated_spore_00..05.png` (6 frames)

## Style and quality

- Non-pixel-art, smooth-edge HD 2D rendering
- Cinematic anime-inspired lighting: clear highlights, glow layers, soft shadows
- Generated at `1024x1024` per frame (PC demo target)

## Regenerate locally

```powershell
python .\tools\generate_td_frame_art.py
```

## Runtime usage

- Animation playback is handled by `TDSpriteAnimator`.
- Towers and enemies auto-bind animation prefixes in runtime creation code.
