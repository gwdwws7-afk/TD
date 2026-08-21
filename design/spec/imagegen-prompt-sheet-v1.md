# 生图提示词清单（手动提交给任意生图 AI 用）
> 自动生成自 tools/generate_worldmap.py / generate_tower_t2.py，与脚本路线完全同源。
> 用法：逐条复制提示词给生图 AI；产出按「保存文件名」命名放入指定目录后跑 import 命令。

## A. T2 补帧（4 张，img2img——需上传对应底图）
底图在本仓库 `Assets/Resources/Art/anim/`，提交给 AI 时作为参考图/底图上传。

### tower_ember_flak_t2_02.png（底图上传 tower_ember_flak_02.png）
```text
using the provided tower sprite as the exact base, keep the tower body, pose, proportions and ground contact pixel-identical, upgrade it to a mid-tier version by adding an ammo-link drum and a muzzle brake: new parts rendered in orange-red #EE6933 accent color with subtle emissive glow, existing structures unchanged, modest enhancement level (this is tier 2 of 3, the final tier stays far more dramatic), 2D game sprite, hand-painted, dark industrial ember-belt style, fully transparent background, PNG alpha, no text, no watermark
```

### tower_grav_snare_t2_04.png（底图上传 tower_grav_snare_04.png）
```text
using the provided tower sprite as the exact base, keep the tower body, pose, proportions and ground contact pixel-identical, upgrade it to a mid-tier version by adding a phase ring and anchoring claws: new parts rendered in blue-violet #7384E3 accent color with subtle emissive glow, existing structures unchanged, modest enhancement level (this is tier 2 of 3, the final tier stays far more dramatic), 2D game sprite, hand-painted, dark industrial ember-belt style, fully transparent background, PNG alpha, no text, no watermark
```

### tower_grav_snare_t2_05.png（底图上传 tower_grav_snare_05.png）
```text
using the provided tower sprite as the exact base, keep the tower body, pose, proportions and ground contact pixel-identical, upgrade it to a mid-tier version by adding a phase ring and anchoring claws: new parts rendered in blue-violet #7384E3 accent color with subtle emissive glow, existing structures unchanged, modest enhancement level (this is tier 2 of 3, the final tier stays far more dramatic), 2D game sprite, hand-painted, dark industrial ember-belt style, fully transparent background, PNG alpha, no text, no watermark
```

### tower_rail_lancer_t2_03.png（底图上传 tower_rail_lancer_03.png）
```text
using the provided tower sprite as the exact base, keep the tower body, pose, proportions and ground contact pixel-identical, upgrade it to a mid-tier version by adding an extended twin-rail barrel and a side-mounted loading ratchet: new parts rendered in steel-blue #408ADB accent color with subtle emissive glow, existing structures unchanged, modest enhancement level (this is tier 2 of 3, the final tier stays far more dramatic), 2D game sprite, hand-painted, dark industrial ember-belt style, fully transparent background, PNG alpha, no text, no watermark
```

## B. 世界图第一波（1 张，验收门禁，img2img）
参考图：`design/spec/assets/world_map_reference.png`（五地貌蛇形拼图），必须作为底图上传。

### world_map_bg.png（生成横图 1536×1024 或任意 16:9）
```text
top-down hand-painted fantasy-industrial world map, five natural terrain regions connected by one winding main railway line: cold-grey rail junction plains with signal towers, ash-desert with depot cranes, ochre slot canyon with trestle bridges, dark-red volcanic kiln basin with slag flows, near-black derelict terminus with one last signal lamp; natural region transitions via river valleys and ridges, mid-low contrast center, warm light on the journey corridor, corner vignette, each region keeps breathing room for four level anchors, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark
- transform the uploaded reference collage of the five region terrains into one continuous painted world map, keeping each region's palette and material language
```

## C. 第二波（33 张，纯文生图，全部透明背景）
每条提示词已含透明背景要求；若 AI 不支持透明，生成后跑 import 命令自动抠底。

### campaign_title_plate.png（目标 1536×180）
```text
wide horizontal title nameplate, forged iron with amber trim, blank center for overlaid text, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, transparent background
```

### landmark_L01.png（目标 384×384）
```text
small landmark vignette for a tower-defense level set in grayline junction plains: a distinct  signal tower, silhouette-readable at small size, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, fully transparent background
```

### landmark_L02.png（目标 384×384）
```text
small landmark vignette for a tower-defense level set in grayline junction plains: a distinct  locomotive shed, silhouette-readable at small size, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, fully transparent background
```

### landmark_L03.png（目标 384×384）
```text
small landmark vignette for a tower-defense level set in grayline junction plains: a distinct  water tower, silhouette-readable at small size, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, fully transparent background
```

### landmark_L04.png（目标 384×384）
```text
small landmark vignette for a tower-defense level set in grayline junction plains: a distinct  inspection pit, silhouette-readable at small size, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, fully transparent background
```

### landmark_L05.png（目标 384×384）
```text
small landmark vignette for a tower-defense level set in ashfall depot: a distinct  loading crane, silhouette-readable at small size, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, fully transparent background
```

### landmark_L06.png（目标 384×384）
```text
small landmark vignette for a tower-defense level set in ashfall depot: a distinct  ore silo, silhouette-readable at small size, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, fully transparent background
```

### landmark_L07.png（目标 384×384）
```text
small landmark vignette for a tower-defense level set in ashfall depot: a distinct  garage hall, silhouette-readable at small size, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, fully transparent background
```

### landmark_L08.png（目标 384×384）
```text
small landmark vignette for a tower-defense level set in ashfall depot: a distinct  watch silo, silhouette-readable at small size, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, fully transparent background
```

### landmark_L09.png（目标 384×384）
```text
small landmark vignette for a tower-defense level set in split switch canyon: a distinct  trestle bridge, silhouette-readable at small size, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, fully transparent background
```

### landmark_L10.png（目标 384×384）
```text
small landmark vignette for a tower-defense level set in split switch canyon: a distinct  suspension bridge, silhouette-readable at small size, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, fully transparent background
```

### landmark_L11.png（目标 384×384）
```text
small landmark vignette for a tower-defense level set in split switch canyon: a distinct  sentry tower, silhouette-readable at small size, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, fully transparent background
```

### landmark_L12.png（目标 384×384）
```text
small landmark vignette for a tower-defense level set in split switch canyon: a distinct  tunnel mouth, silhouette-readable at small size, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, fully transparent background
```

### landmark_L13.png（目标 384×384）
```text
small landmark vignette for a tower-defense level set in hollow kiln basin: a distinct  blast furnace, silhouette-readable at small size, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, fully transparent background
```

### landmark_L14.png（目标 384×384）
```text
small landmark vignette for a tower-defense level set in hollow kiln basin: a distinct  pipe bridge, silhouette-readable at small size, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, fully transparent background
```

### landmark_L15.png（目标 384×384）
```text
small landmark vignette for a tower-defense level set in hollow kiln basin: a distinct  ore cart tipper, silhouette-readable at small size, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, fully transparent background
```

### landmark_L16.png（目标 384×384）
```text
small landmark vignette for a tower-defense level set in hollow kiln basin: a distinct  chimney cluster, silhouette-readable at small size, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, fully transparent background
```

### landmark_L17.png（目标 384×384）
```text
small landmark vignette for a tower-defense level set in last ember terminus: a distinct  terminus hall, silhouette-readable at small size, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, fully transparent background
```

### landmark_L18.png（目标 384×384）
```text
small landmark vignette for a tower-defense level set in last ember terminus: a distinct  great lamp tower, silhouette-readable at small size, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, fully transparent background
```

### landmark_L19.png（目标 384×384）
```text
small landmark vignette for a tower-defense level set in last ember terminus: a distinct  carriage depot, silhouette-readable at small size, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, fully transparent background
```

### landmark_L20.png（目标 384×384）
```text
small landmark vignette for a tower-defense level set in last ember terminus: a distinct  memorial monument, silhouette-readable at small size, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, fully transparent background
```

### meta_entry_button.png（目标 384×384）
```text
square forged-iron button with an amber ember-residue emblem (crystal shard in a cog), 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, transparent background
```

### meta_node_slot.png（目标 192×192）
```text
small hexagonal upgrade node slot, neutral metal, unlit, ready for tinting, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, transparent background
```

### meta_panel_frame.png（目标 1536×1024）
```text
large UI panel frame, forged-iron border with amber trim, top currency strip zone, four horizontal upgrade-line rows inside, bottom bar, blank content areas, nine-slice friendly, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, transparent background
```

### node_available.png（目标 512×512）
```text
round heavy metal badge with riveted rim and an amber glowing railway-signal core, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, transparent background
```

### node_boss.png（目标 640×640）
```text
oversized round heavy metal badge with hazard stripes and a red glowing beast-skull core, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, transparent background
```

### node_cleared.png（目标 512×512）
```text
round heavy metal badge with riveted rim and a green sealed-stamp glowing core, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, transparent background
```

### node_locked.png（目标 512×512）
```text
round heavy metal badge with riveted rim, dark unlit core with a small padlock and chain, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, transparent background
```

### node_selected.png（目标 512×512）
```text
thin gold highlight ring, hollow center, subtle ember sparks on the rim, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, transparent background
```

### path_rail_strip.png（目标 1024×128）
```text
horizontal luminous rail track strip, twin rails with a faintly glowing groove between them, medium brightness ready for tinting, tileable left-right, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, transparent background
```

### region_plate.png（目标 1024×192）
```text
wide horizontal forged-iron nameplate strip with amber trim and rivets, gently weathered, nine-slice friendly, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, transparent background
```

### seal_pip.png（目标 128×128）
```text
small neutral metal seal medallion, unlit, ready for tinting, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, transparent background
```

### seal_pip_empty.png（目标 128×128）
```text
small dark empty seal socket groove, 2D game art, hand-painted, dark industrial post-apocalyptic ember-belt style, weathered charcoal and rust-brown metal with glowing amber-orange accents, subtle teal energy details, painterly texture with soft volumetric glow, no text, no watermark, transparent background
```

## 产出落地与导入
```bash# T2 补帧：原图放 output/imagegen/_t2_raw/tower_{kind}_t2_{ii}.png 后python tools/generate_tower_t2.py            # 自动检测已有 raw，跳过 API 直接合成# 世界图/第二波：原图放 output/imagegen/_worldmap_raw/{资产名}.png 后python tools/generate_worldmap.py --wave 1 --import-onlypython tools/generate_worldmap.py --wave 2 --import-only# 单张重导python tools/generate_worldmap.py --only landmark_L01 node_boss --import-only```
