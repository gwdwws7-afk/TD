param(
    [string]$Model = "gpt-image-1.5",
    [string]$Quality = "high",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

function Resolve-CodexHome {
    if (-not [string]::IsNullOrWhiteSpace($env:CODEX_HOME)) {
        return $env:CODEX_HOME
    }

    return Join-Path $HOME ".codex"
}

if (-not $DryRun -and [string]::IsNullOrWhiteSpace($env:OPENAI_API_KEY)) {
    throw "OPENAI_API_KEY is missing. Set it before running this script."
}

$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python) {
    throw "python is required but was not found in PATH."
}

$codexHome = Resolve-CodexHome
$imageCli = Join-Path $codexHome "skills\imagegen\scripts\image_gen.py"
if (-not (Test-Path $imageCli)) {
    throw "image_gen.py not found at: $imageCli"
}

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$guideScript = Join-Path $projectRoot "tools\build_campaign_map_guides.py"
$upscaleScript = Join-Path $projectRoot "tools\upscale_campaign_map_surfaces.py"

$guideDir = Join-Path $projectRoot "output\imagegen\batch10_map_guides"
$rawDir = Join-Path $projectRoot "output\imagegen\batch10_map_raw"
$upscaledDir = Join-Path $projectRoot "output\imagegen\batch10_map_upscaled"
$artDir = Join-Path $projectRoot "Assets\Resources\Art"

New-Item -ItemType Directory -Force -Path $guideDir | Out-Null
New-Item -ItemType Directory -Force -Path $rawDir | Out-Null
New-Item -ItemType Directory -Force -Path $upscaledDir | Out-Null
New-Item -ItemType Directory -Force -Path $artDir | Out-Null

Write-Host "[batch10-map] build path guides"
& $python.Source $guideScript
if ($LASTEXITCODE -ne 0) {
    throw "guide generation failed"
}

$maps = @(
    @{
        MapId = "grayline_junction"
        Prompt = @"
Use case: stylized-concept. Asset type: top-down 2D tower-defense map surface.
Primary request: transform this gameplay guide into a production-quality painted battlefield for Emberline Defense.
Scene/background: post-apocalyptic rail junction with switch machinery and ashen scrubland.
Composition/framing: keep the road/path silhouette exactly where the highlighted guide path is; remove all grid lines and hint markers.
Lighting/mood: clean daylight readability with warm ember highlights and cool steel shadows.
Constraints: preserve path readability for gameplay, keep two landmark zones where diamond hints are, full-board composition, no text, no logos, no watermark.
Avoid: pixel-art look, blurry fog blobs, giant circles, abstract gradients, neon palette.
"@
    },
    @{
        MapId = "ashfall_depot"
        Prompt = @"
Use case: stylized-concept. Asset type: top-down 2D tower-defense map surface.
Primary request: transform this gameplay guide into a painted industrial freight depot under ashfall.
Scene/background: rail cargo yard, burnt loading pads, hazard-striped service lanes, scattered steel containers.
Composition/framing: keep the road/path silhouette exactly where the highlighted guide path is; remove all grid lines and hint markers.
Lighting/mood: dry dusty daylight, amber ash glow, strong silhouette readability.
Constraints: preserve route clarity and choke points, keep two landmark zones where diamond hints are, full-board composition, no text, no logos, no watermark.
Avoid: tile stitching look, low-detail mush, random abstract shapes, neon palette.
"@
    },
    @{
        MapId = "split_switch_canyon"
        Prompt = @"
Use case: stylized-concept. Asset type: top-down 2D tower-defense map surface.
Primary request: transform this gameplay guide into a painted rail canyon with split-switch topology.
Scene/background: fractured canyon floor, exposed rails, branching switch nodes, wind-blown dust and debris.
Composition/framing: keep the road/path silhouette exactly where the highlighted guide path is; remove all grid lines and hint markers.
Lighting/mood: bright readable battlefield tones, warm stone vs cool metal contrast.
Constraints: preserve branch-and-merge path readability, keep two landmark zones where diamond hints are, full-board composition, no text, no logos, no watermark.
Avoid: abstract circles, muddy contrast, photoreal clutter, pixel-art style.
"@
    },
    @{
        MapId = "hollow_kiln_basin"
        Prompt = @"
Use case: stylized-concept. Asset type: top-down 2D tower-defense map surface.
Primary request: transform this gameplay guide into a painted hollow kiln basin battlefield.
Scene/background: cratered kiln basin, circular industrial ruins, scorched ground plates, ash vents.
Composition/framing: keep the road/path silhouette exactly where the highlighted guide path is; remove all grid lines and hint markers.
Lighting/mood: high readability with restrained glow and clear terrain separation.
Constraints: preserve return-path shape, keep two landmark zones where diamond hints are, full-board composition, no text, no logos, no watermark.
Avoid: giant soft blobs, dark unreadable corners, heavy bloom, pixel-art look.
"@
    },
    @{
        MapId = "last_ember_terminus"
        Prompt = @"
Use case: stylized-concept. Asset type: top-down 2D tower-defense map surface.
Primary request: transform this gameplay guide into a painted final-terminal battlefield.
Scene/background: terminal approach with ember reactor infrastructure, armored platforms, broken rail gates.
Composition/framing: keep the road/path silhouette exactly where the highlighted guide path is; remove all grid lines and hint markers.
Lighting/mood: tense endgame atmosphere, clean tactical readability, premium stylized finish.
Constraints: preserve final approach route readability, keep two landmark zones where diamond hints are, full-board composition, no text, no logos, no watermark.
Avoid: abstract gradient-only background, chaotic detail noise, low-contrast mud, pixel-art style.
"@
    }
)

foreach ($map in $maps) {
    $mapId = $map.MapId
    $guideIn = Join-Path $guideDir ("guide_{0}_1536x1024.png" -f $mapId)
    $rawOut = Join-Path $rawDir ("map_surface_{0}_16x9_raw.png" -f $mapId)

    if (-not (Test-Path $guideIn)) {
        throw "guide missing for mapId=$mapId at $guideIn"
    }

    Write-Host "[batch10-map] generate $mapId"
    $args = @(
        $imageCli,
        "edit",
        "--model", $Model,
        "--image", $guideIn,
        "--prompt", $map.Prompt,
        "--no-augment",
        "--size", "1536x1024",
        "--quality", $Quality,
        "--background", "opaque",
        "--output-format", "png",
        "--input-fidelity", "high",
        "--out", $rawOut,
        "--force"
    )
    if ($DryRun) {
        $args += "--dry-run"
    }

    & $python.Source @args

    if ($LASTEXITCODE -ne 0) {
        throw "map surface generation failed for $mapId"
    }
}

if ($DryRun) {
    Write-Host "Batch 10 map surfaces dry-run complete."
    return
}

Write-Host "[batch10-map] upscale and publish"
& $python.Source $upscaleScript `
    --raw-dir $rawDir `
    --out-dir $upscaledDir `
    --art-dir $artDir

if ($LASTEXITCODE -ne 0) {
    throw "map surface upscale/publish failed"
}

Write-Host "Batch 10 map surfaces written to: $artDir"
