# Demo Visual Calibration v1 (HD 2D)

Date: 2026-05-16
Scope: First playable demo (tower + enemy + board)

## Goals

- Keep HD 2D assets readable at 16:9 camera without pixel-art look.
- Decouple gameplay transform from art transform to make future art swaps safe.
- Keep click hitboxes stable while freely tuning sprite scale/offset.

## Runtime structure

- Root GameObject: movement / targeting / collider / logical position.
- Child `Visual`: `SpriteRenderer` + `TDSpriteAnimator` + art scale/offset.

This applies to:

- Towers (`TDTower` owns `Visual` child)
- Enemies (`TDGameManager.SpawnEnemy` builds `Visual` child)

## Current calibration values

### Tower visuals

- Rail Lancer: `targetCellCoverage=0.94`, `yOffset=-0.10`, `sortingOrder=12`
- Cinder Mortar: `targetCellCoverage=1.00`, `yOffset=-0.09`, `sortingOrder=12`
- Frost Coil: `targetCellCoverage=0.90`, `yOffset=-0.07`, `sortingOrder=12`
- Tower base plate layer: disabled for current HD unit batch (tower sprites include their own base silhouette)

Runtime note:

- Tower `Visual` scale is now resolved from sprite bounds at runtime (`target world width = CellSize * targetCellCoverage`), so unit size remains stable across asset PPU/slice differences.

### Tower colliders

- Rail Lancer: `size=(0.44, 0.44)`, `offset=(0, -0.04)`
- Cinder Mortar: `size=(0.48, 0.48)`, `offset=(0, -0.03)`
- Frost Coil: `size=(0.42, 0.42)`, `offset=(0, -0.02)`

### Enemy visuals

- skitter_runner: `targetCellCoverage=0.70`, `yOffset=-0.06`, `sort=16`, `animFps=10`
- carapace_brute: `targetCellCoverage=0.88`, `yOffset=-0.05`, `sort=16`, `animFps=7`
- ash_swarm: `targetCellCoverage=0.62`, `yOffset=-0.03`, `sort=16`, `animFps=12`
- plated_spore: `targetCellCoverage=0.76`, `yOffset=-0.04`, `sort=16`, `animFps=7`

Runtime note:

- Enemy `Visual` scale is now resolved from sprite bounds at spawn time (`target world width = CellSize * targetCellCoverage`).

### Enemy colliders

- skitter_runner: `size=(0.34, 0.34)`, `offset=(0, -0.05)`
- carapace_brute: `size=(0.46, 0.46)`, `offset=(0, -0.03)`
- ash_swarm: `size=(0.32, 0.32)`, `offset=(0, -0.03)`
- plated_spore: `size=(0.40, 0.40)`, `offset=(0, -0.04)`

### Other visual tweaks

- Build marker scale: resolved from sprite bounds to `0.92 * CellSize` at runtime.
- Projectile visual scale: `1.05`
- Sprite import: frame sprites normalized to full `1024x1024` rect in meta for stable bounds.

## Animation stability

`TDSpriteAnimator` now uses catch-up stepping with capped advance (`1..3` frames) during frame hitches to reduce visible jitter.

## Combat feedback (implemented)

- Hit flash: enemies briefly flash toward white (`0.10s`).
- Slow feedback: enemies under slow receive cold-blue tint based on slow strength.
- Death fade: killed enemies fade out with slight shrink (`0.22s`) before cleanup.
- AOE hint: mortar-like area hits spawn expanding ring indicator (`0.24s`) using `Art/build_marker`.
- Projectile trail (tower-specific): each tower now emits different colored afterimage trail.
- Impact spark (tower-specific): direct and AOE hits now spawn color-coded burst sprites.

## Art batch 2 (implemented)

- Added HD1024 environment decals: ash patches, scrap clusters, path cracks, rail remnants.
- Added HD1024 tower base plate and runtime tower-base rendering layer.
- Added 4K backdrop (`4096x2304`) and deterministic per-cell decal placement.
- Removed full-grid build-marker noise; replaced with cursor-cell build preview marker.

## Art batch 4 (implemented)

- Rebuilt `map_surface_grayline_16x9` into a full-board painted scene surface to remove visible tile stitching.
- Rebalanced atmosphere overlays (`map_shadow_overlay`, `map_light_overlay`) to avoid giant visible circles and preserve gameplay readability.
- Forced opaque alpha on `map_surface_grayline_16x9` and `map_backdrop` to prevent dark transparency artifacts.
- Switched demo map source to image2.0-generated hand-painted battlefield surface (post-apocalyptic railway theme), then resized to `4096x2304` for runtime.
- Updated path cells to follow the new painted road silhouette for better gameplay-art alignment.

## Art batch 5 (implemented)

- Replaced demo `anim/tower_*` and `anim/enemy_*` with HD1024 painted unit set generated from image2.0.
- Added `tools/process_hd_unit_art.py` to automate unit cutout, frame synthesis, and meta rect normalization.
- Shifted runtime scale model to sprite-bounds-based world fitting for towers, enemies, and build preview marker.

## Art batch 6 (implemented)

- Regenerated towers/enemies as a style-unified set (`units_v4_raw`) focused on battle readability.
- Per-unit background cleanup was applied with image2.0 edit mode into transparent masters (`units_v4_cut`).
- Rebuilt all runtime animation frames from cleaned masters and refreshed contact sheet:
  - `output/imagegen/units_v4_cutout/units_v4_contact_sheet.png`

## Art batch 7 (implemented)

- Upgraded runtime HUD from plain fallback rectangle to an art-skinned panel workflow.
- Added resources:
  - `hud_panel_bg`, `hud_panel_titlebar`, `hud_status_strip`
  - `hud_button_restart`
  - `hud_icon_wave`, `hud_icon_integrity`, `hud_icon_budget`
- `TDGameManager` now renders HUD with iconized metrics and skinned game-over restart button.
- Added pipeline script `tools/build_hud_art_pack.py` for HUD asset normalization/export.

## Art batch 8 (implemented)

- Added runtime soft ground shadows for both towers and enemies to improve contact and depth on painted maps.
- Increased core unit visual coverage (tower/enemy scale fit) for better readability on PC at 16:9.
- Rebuilt `map_shadow_overlay` and `map_light_overlay` via script into layered alpha textures (no flat-tint look).
- Added `tools/build_map_atmosphere_overlays.py` to regenerate these overlays deterministically.
- Preview exports:
  - `output/imagegen/preview_batch8_scale_shadow_1920x1080.png`
  - `output/imagegen/map_overlay_preview_batch8.png`

## Art batch 9 (implemented)

- Replaced map set-dressing decals/props with image2.0 HD1024 painted assets:
  - ash/scorch patches
  - scrap clusters
  - path crack / rail remnants
  - railway barricades, signal posts, wreck crates
- Added board-surface decoration pipeline in `TDGridMap`:
  - path vs non-path decal pools with deterministic hash placement
  - near-path prop placement with controlled density
  - recommended build spot marker generation (deterministic + spacing constraints)
- Layering adjustments:
  - board decals `sortingOrder=3`
  - props `sortingOrder=4`
  - recommended build spots `sortingOrder=5`
  - build preview marker `sortingOrder=7`
- Added reproducible generation script:
  - `tools/generate_batch9_set_dressing.ps1`
- Preview outputs:
  - `output/imagegen/preview_batch9_setdressing_tuned_1920x1080.png`
  - `design/reference/compare_batch9_vs_kr_level.png`

## Art batch 11 (implemented)

- Filled missing campaign unit runtime coverage from content matrix scope:
  - Towers: `arc_welder`, `siege_drill`, `ember_flak`, `resonance_beacon`, `grav_snare`
  - Enemies: `burrow_sapper`, `ember_leech`, `spore_carrier`, `rail_warden`, `cinder_glider`, `husk_titan`, `echo_mimic`, `furnace_matriarch`
- Added tower upgrade readability assets:
  - every tower now has `*_t3_*` animation frames for clear `T0` vs `T3` differentiation.
- Runtime visual switch:
  - `TDTower` now auto-resolves tier-3 sprite/animation resources when `Tier >= 3` with fallback to base resources if missing.
- Added production scripts:
  - `tools/build_batch11_unit_extension.py` (fills missing unit frames + builds tower `t3` variants)
  - `tools/generate_batch11_units_art.ps1` (image2.0 batch generation for missing masters)
  - `tools/run_batch11_units_pipeline.ps1` (end-to-end batch 11 runner)

## Art batch 12 (implemented)

- Added dedicated combat FX frame packs:
  - `fx_enemy_hit_00..05.png` (enemy hit-only frame sequence)
  - `fx_enemy_death_00..07.png` (enemy death-only frame sequence)
  - `fx_boss_warning_00..09.png` (boss spawn warning sequence)
- Runtime integration:
  - `TDEnemy` now triggers hit FX on non-lethal damage.
  - `TDEnemy` now triggers death FX on kill resolve.
  - `TDEnemy` now triggers boss warning FX when enemy tags include `boss` or `final`.
- Added production scripts:
  - `tools/generate_batch12_fx_art.ps1` (image2.0 FX master generation)
  - `tools/build_batch12_fx_frames.py` (master-to-frame synthesis pipeline)
  - `tools/run_batch12_fx_pipeline.ps1` (end-to-end batch 12 runner)

## Art batch 13 (implemented)

- Added special-mechanic enemy hint FX frame packs (E05/E07/E11):
  - `fx_burrow_ambush_00..07.png` (Burrow Sapper mid-path ambush telegraph)
  - `fx_spore_split_warning_00..07.png` (Spore Carrier low-HP split warning)
  - `fx_mimic_shift_00..07.png` (Echo Mimic variant reveal telegraph)
- Runtime integration in `TDEnemy`:
  - Burrow Sapper now triggers ambush warning FX when special burst starts.
  - Spore Carrier now triggers split warning FX when HP drops below threshold.
  - Echo Mimic now triggers variant reveal FX on spawn (variant-aware tint).
- Added production scripts:
  - `tools/generate_batch13_mechanic_fx_art.ps1` (image2.0 master generation)
  - `tools/build_batch13_mechanic_fx_frames.py` (master-to-frame synthesis)
  - `tools/run_batch13_mechanic_fx_pipeline.ps1` (end-to-end batch 13 runner)

## Art batch 14 (implemented)

- Added extended enemy-mechanic hint FX frame packs:
  - `fx_attrition_siphon_00..07.png` (Ember Leech attrition-siphon warning pulse)
  - `fx_support_link_00..07.png` (Rail Warden support-link aura pulse)
  - `fx_elite_pressure_00..09.png` (Husk Titan elite pressure surge)
- Runtime integration in `TDEnemy`:
  - Attrition-tag enemies now emit periodic siphon warning FX.
  - Support-tag enemies now emit periodic support-link FX when nearby ally buff targets exist.
  - Elite/Husk Titan now emits pressure surge FX at low-HP threshold.
- Added production scripts:
  - `tools/generate_batch14_enemy_mechanic_fx_art.ps1` (image2.0 master generation)
  - `tools/build_batch14_enemy_mechanic_fx_frames.py` (master-to-frame synthesis)
  - `tools/run_batch14_enemy_mechanic_fx_pipeline.ps1` (end-to-end batch 14 runner)

## Next tuning pass

- Validate in Play Mode at `0.75x / 1.0x / 1.25x` game speed.
- Confirm shadows remain readable but not muddy on low-contrast displays.
- Tune trail density and spark size to keep clarity in wave 8+ crowd fights.
- Revisit sort strategy if future maps add overlap-heavy terrain props.
