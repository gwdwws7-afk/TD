# image2.0 母提示词 - Emberline Frontier v1

> **Model**: gpt-image-1.5 (image2.0 pipeline)  
> **Date**: 2026-05-16  
> **Status**: Superseded (Historical Reference)  
> **Superseded By**: `design/gdd/image2-master-prompt-emberline-frontier-v2.md` (2026-05-19)

> [!WARNING]
> 本文档仅用于 Vertical Slice 阶段。20关内容生产请使用 v2 母提示词。

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

## 2. 资产子模板
### 2.1 塔类（单体）
Use case: stylized-concept  
Asset type: game character concept  
Primary request: top-down "Rail Lancer Tower", armored railgun turret mounted on industrial base  
Composition/framing: centered, transparent background, strong silhouette for instant recognition  
Constraints: no text, no logos, no watermark

### 2.2 塔类（范围）
Use case: stylized-concept  
Asset type: game character concept  
Primary request: top-down "Cinder Mortar Tower", heavy short-barrel mortar with coal ignition chamber  
Composition/framing: centered, transparent background, readable barrel direction  
Constraints: no text, no logos, no watermark

### 2.3 塔类（控制）
Use case: stylized-concept  
Asset type: game character concept  
Primary request: top-down "Frost Coil Tower", compact condenser tower with cooling rings and pressure pipes  
Composition/framing: centered, transparent background, clear blue-cold accent details  
Constraints: no text, no logos, no watermark

### 2.4 敌人（高速）
Use case: stylized-concept  
Asset type: game character concept  
Primary request: top-down "Skitter Runner", small agile wasteland creature, low armor, fast posture  
Composition/framing: centered, transparent background  
Constraints: no text, no logos, no watermark

### 2.5 敌人（重装）
Use case: stylized-concept  
Asset type: game character concept  
Primary request: top-down "Carapace Brute", large armored beast with heavy shell plates  
Composition/framing: centered, transparent background  
Constraints: no text, no logos, no watermark

### 2.6 地块（可平铺）
Use case: stylized-concept  
Asset type: tileable game texture  
Primary request: top-down rail-adjacent ground tile for wasteland outpost  
Composition/framing: seamless square texture, neutral lighting  
Constraints: seamless edges, no text, no logos, no watermark

## 3. 质量与导出规则
1. 默认尺寸：`1024x1024`
2. 需要透明底的资产：`--background transparent --output-format png`
3. 先小批评审 6 张，再扩批
4. 命名统一采用英文ID（参照命名总表）

## 4. 统一负面约束（每次可复用）
Avoid: cluttered composition, over-detailed micro noise, unreadable silhouette, neon purple dominance, heavy lens flare, text artifacts, watermark
