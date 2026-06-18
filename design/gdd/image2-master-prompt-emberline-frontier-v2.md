# image2.0 母提示词 - Emberline Frontier v2

> **Model**: gpt-image-1.5  
> **Date**: 2026-05-19  
> **Status**: Active for Campaign Production (20-level scope)  
> **Naming Source**: `design/gdd/worldview-naming-emberline-frontier-v2.md`

## 1. 风格母提示词（Master Prompt）
Use case: stylized-concept  
Asset type: 2D tower defense game asset for top-down gameplay  
Primary request: create clean, readable, stylized dieselpunk frontier assets for "Emberline Frontier", centered composition, gameplay-first readability  
Scene/background: arid wasteland rail outpost, ember-lit industrial infrastructure  
Style/medium: hand-painted stylized 2D game art, crisp silhouette, restrained texture noise  
Composition/framing: top-down friendly orientation, single subject centered, clear edges, transparent background when applicable  
Lighting/mood: warm ember glow vs cold steel shadows, high readability contrast  
Color palette: rust orange, coal black, steel gray, hazard yellow accents, dust beige background notes  
Materials/textures: worn metal, rail steel, rivets, industrial paint, soot and dust  
Constraints: no text, no logos, no watermark, no photoreal gore, avoid visual clutter  
Avoid: neon cyberpunk palette, excessive bloom, noisy backgrounds, tiny unreadable details

## 2. 塔资产模板（8塔）
1. `rail_lancer_tower`: precise single-target railgun tower, long barrel, stable footing.
2. `cinder_mortar_tower`: heavy short-range mortar with blast chamber.
3. `frost_coil_tower`: condenser rings and coolant pipes, control silhouette.
4. `arc_welder_tower`: chained arc projector with conductive coils.
5. `siege_drill_tower`: anti-armor drilling cannon with reinforced chassis.
6. `ember_flak_tower`: rapid intercept flak turret for fast units.
7. `resonance_beacon_tower`: tactical beacon tower with signal emitters.
8. `grav_snare_tower`: area-control gravity node with field anchors.

## 3. 敌人资产模板（12敌）
1. `skitter_runner`: small, agile fast crawler.
2. `ash_swarm`: clustered swarm body with grouped motion readability.
3. `carapace_brute`: heavy armored hulking unit.
4. `plated_spore`: mid-size armored pod with dense shell.
5. `burrow_sapper`: low-profile rushing saboteur form.
6. `ember_leech`: siphon-like attrition creature with core glow.
7. `spore_carrier`: carrier form with split-capable sacs.
8. `rail_warden`: support unit with shield projection rig.
9. `cinder_glider`: lateral fast glider silhouette.
10. `husk_titan`: elite massive shell unit with threat readability.
11. `echo_mimic`: adaptive mimic body with mirrored motifs.
12. `furnace_matriarch`: final boss scale, layered furnace anatomy.

## 4. 地图资产方向（5图）
1. `grayline_junction`: main rail junction, compact learning geometry.
2. `ashfall_depot`: loading depot with long lanes and choke bends.
3. `split_switch_canyon`: split/merge switch nodes, readable branch topology.
4. `hollow_kiln_basin`: ring-like basin and return paths.
5. `last_ember_terminus`: terminal zone with staged final approach.

## 5. 产出规格
1. 默认尺寸：`1024x1024`
2. 透明底资产：`--background transparent --output-format png`
3. 批次策略：每批先 6 张评审，再扩批
4. 输出命名：`<asset_id>_<variant>_<index>.png`

## 6. 统一负面约束
Avoid: cluttered composition, over-detailed micro noise, unreadable silhouette, neon purple dominance, heavy lens flare, text artifacts, watermark
