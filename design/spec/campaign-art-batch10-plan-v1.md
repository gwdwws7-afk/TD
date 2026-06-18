# Campaign Art Batch 10 Plan (Map Production)

Date: 2026-05-20  
Scope: Move from demo-level art to campaign-level map art (5 maps / 20 levels)

## Source Alignment

- `design/gdd/content-matrix-20-level-v1.md`
- `design/gdd/full-project-plan-emberline-defense-v2.0.md`
- `design/spec/visual-calibration-demo-v1.md`

## New Art Requirements (Delta from Demo)

1. Build 5 distinct map surfaces matching campaign map IDs:
   - `grayline_junction`
   - `ashfall_depot`
   - `split_switch_canyon`
   - `hollow_kiln_basin`
   - `last_ember_terminus`
2. Each map must have at least one recognizable landmark group (visual memory anchors).
3. Preserve gameplay readability:
   - route silhouette must align with runtime path cells
   - route/background contrast must remain clear in high-pressure waves
4. Maintain HD pipeline:
   - generation base: `1536x1024`
   - runtime final: `4096x2304`
   - import into `Assets/Resources/Art` as `map_surface_<mapId>_16x9.png`

## Naming Contract

### Map surfaces

- `map_surface_<mapId>_16x9.png`

### Map landmark props (new)

- `prop_anchor_<mapId>_a.png`
- `prop_anchor_<mapId>_b.png`
- Optional: `prop_anchor_<mapId>_c.png`

### Map local prop overrides (optional)

- `prop_<mapId>_a.png`
- `prop_<mapId>_b.png`
- Optional: `prop_<mapId>_c.png`

### Map local decal overrides (optional)

- `decal_<mapId>_ground_a.png`
- `decal_<mapId>_ground_b.png`
- `decal_<mapId>_path_a.png`
- `decal_<mapId>_path_b.png`

## Runtime Hook (Implemented)

`TDGridMap` now supports:

1. map-specific decals/props override with fallback to generic pool
2. map-specific landmark anchor sprites:
   - loads `prop_anchor_<mapId>_*`
   - places deterministic anchor props by map profile
3. no map-specific asset found -> fallback to existing demo assets

## Batch 10 Tooling (Added)

1. `tools/build_campaign_map_guides.py`
   - Generates guide boards with path/anchor hints for all 5 maps.
2. `tools/generate_batch10_map_surfaces.ps1`
   - Uses image2.0 edit workflow from guides to produce map surfaces.
3. `tools/upscale_campaign_map_surfaces.py`
   - Upscales and publishes final `4096x2304` surfaces to runtime art folder.
4. `tools/generate_batch10_map_props.ps1`
   - Generates map-specific props + anchor props for all 5 maps.

## Acceptance Gate

1. Visual:
   - no visible tile stitching on map board
   - each map has distinct silhouette and landmark memory point
2. Gameplay read:
   - path visible at a glance in all maps
   - wave 18-20 readability not lost by atmosphere/prop noise
3. Runtime:
   - `TDGridMap` loads `map_surface_<mapId>_16x9` automatically by campaign `mapId`
   - map-specific props fall back safely when assets are missing
