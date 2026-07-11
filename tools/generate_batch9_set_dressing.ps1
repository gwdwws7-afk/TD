param(
    [string]$Model = "gpt-image-1.5",
    [string]$Quality = "high",
    [switch]$SkipCut,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "td_imagegen_common.ps1")

function Resolve-CodexHome {
    if (-not [string]::IsNullOrWhiteSpace($env:CODEX_HOME)) {
        return $env:CODEX_HOME
    }

    return Join-Path $HOME ".codex"
}

Import-TDOpenAIApiKey -Required (-not $DryRun.IsPresent)

$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python) {
    throw "python is required but was not found in PATH."
}

$imageCli = Resolve-TDImageGenCli

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$rawDir = Join-Path $projectRoot "output\imagegen\batch9_raw"
$cutDir = Join-Path $projectRoot "output\imagegen\batch9_cut"
$artDir = Join-Path $projectRoot "Assets\Resources\Art"

New-Item -ItemType Directory -Force -Path $rawDir | Out-Null
New-Item -ItemType Directory -Force -Path $cutDir | Out-Null
New-Item -ItemType Directory -Force -Path $artDir | Out-Null

$assets = @(
    @{
        Name = "prop_rail_barricade_a"
        Prompt = "Use case: stylized-concept. Asset type: game prop sprite. Primary request: top-down post-apocalyptic railway barricade with warning stripe and mixed scrap parts for a 2D tower defense map. Style/medium: premium hand-painted stylized. Composition: single centered prop, transparent background, no frame, no cast shadow. Constraints: no text, no watermark, no logos."
    },
    @{
        Name = "prop_rail_barricade_b"
        Prompt = "Use case: stylized-concept. Asset type: game prop sprite. Primary request: top-down broken steel barricade with bent rails and hazard signs, post-apocalyptic railway style. Style/medium: premium hand-painted stylized. Composition: single centered prop, transparent background, no frame, no cast shadow. Constraints: no text, no watermark, no logos."
    },
    @{
        Name = "prop_signal_post_a"
        Prompt = "Use case: stylized-concept. Asset type: game prop sprite. Primary request: top-down compact railway signal post with cyan lamp, weathered metal and cables. Style/medium: premium hand-painted stylized. Composition: single centered prop, transparent background, no frame, no cast shadow. Constraints: no text, no watermark, no logos."
    },
    @{
        Name = "prop_signal_post_b"
        Prompt = "Use case: stylized-concept. Asset type: game prop sprite. Primary request: top-down damaged railway warning post with warm amber light and rusted casing. Style/medium: premium hand-painted stylized. Composition: single centered prop, transparent background, no frame, no cast shadow. Constraints: no text, no watermark, no logos."
    },
    @{
        Name = "prop_wreck_crate_a"
        Prompt = "Use case: stylized-concept. Asset type: game prop sprite. Primary request: top-down damaged industrial cargo crate with iron braces and cracks, railway wasteland theme. Style/medium: premium hand-painted stylized. Composition: single centered prop, transparent background, no frame, no cast shadow. Constraints: no text, no watermark, no logos."
    },
    @{
        Name = "prop_wreck_crate_b"
        Prompt = "Use case: stylized-concept. Asset type: game prop sprite. Primary request: top-down shattered crate and debris stack for post-apocalyptic railway battlefield dressing. Style/medium: premium hand-painted stylized. Composition: single centered prop, transparent background, no frame, no cast shadow. Constraints: no text, no watermark, no logos."
    },
    @{
        Name = "decal_ash_patch_a"
        Prompt = "Use case: stylized-concept. Asset type: ground decal sprite. Primary request: irregular top-down ash burn patch with embers and soft broken edge for battlefield dressing. Style/medium: premium hand-painted stylized. Composition: centered irregular decal with transparent feathered edges, no cast shadow. Constraints: no text, no watermark, no logos."
    },
    @{
        Name = "decal_ash_patch_b"
        Prompt = "Use case: stylized-concept. Asset type: ground decal sprite. Primary request: scorched dirt and soot patch, irregular organic shape for railway wasteland map. Style/medium: premium hand-painted stylized. Composition: centered irregular decal with transparent feathered edges, no cast shadow. Constraints: no text, no watermark, no logos."
    },
    @{
        Name = "decal_scrap_cluster_a"
        Prompt = "Use case: stylized-concept. Asset type: ground decal sprite. Primary request: top-down cluster of rusty scrap fragments and gravel for map dressing. Style/medium: premium hand-painted stylized. Composition: centered loose cluster, transparent background, no cast shadow. Constraints: no text, no watermark, no logos."
    },
    @{
        Name = "decal_scrap_cluster_b"
        Prompt = "Use case: stylized-concept. Asset type: ground decal sprite. Primary request: top-down metal shard and rubble cluster, rough battlefield detail. Style/medium: premium hand-painted stylized. Composition: centered loose cluster, transparent background, no cast shadow. Constraints: no text, no watermark, no logos."
    },
    @{
        Name = "decal_path_crack_a"
        Prompt = "Use case: stylized-concept. Asset type: ground decal sprite. Primary request: top-down asphalt crack and chipped edge decal for road/path variation. Style/medium: premium hand-painted stylized. Composition: centered elongated decal, transparent background, no cast shadow. Constraints: no text, no watermark, no logos."
    },
    @{
        Name = "decal_path_crack_b"
        Prompt = "Use case: stylized-concept. Asset type: ground decal sprite. Primary request: top-down broken road seam and grit variation decal for ruined path. Style/medium: premium hand-painted stylized. Composition: centered elongated decal, transparent background, no cast shadow. Constraints: no text, no watermark, no logos."
    },
    @{
        Name = "decal_path_rail_a"
        Prompt = "Use case: stylized-concept. Asset type: ground decal sprite. Primary request: top-down old rail segment embedded in cracked dirt road, variant A. Style/medium: premium hand-painted stylized. Composition: centered horizontal decal with transparent edge, no cast shadow. Constraints: no text, no watermark, no logos."
    },
    @{
        Name = "decal_path_rail_b"
        Prompt = "Use case: stylized-concept. Asset type: ground decal sprite. Primary request: top-down old rail segment embedded in cracked dirt road, variant B. Style/medium: premium hand-painted stylized. Composition: centered horizontal decal with transparent edge, no cast shadow. Constraints: no text, no watermark, no logos."
    }
)

foreach ($asset in $assets) {
    $rawOut = Join-Path $rawDir ($asset.Name + ".png")
    Write-Host "[batch9] generate $($asset.Name)"
    $args = @(
        $imageCli,
        "generate",
        "--model", $Model,
        "--prompt", $asset.Prompt,
        "--size", "1024x1024",
        "--quality", $Quality,
        "--background", "transparent",
        "--output-format", "png",
        "--out", $rawOut
    )
    if ($DryRun) {
        $args += "--dry-run"
    }

    & $python.Source @args

    if ($LASTEXITCODE -ne 0) {
        throw "generation failed for $($asset.Name)"
    }
}

if ($DryRun) {
    Write-Host "Batch 9 set-dressing dry-run complete."
    return
}

if (-not $SkipCut) {
    foreach ($asset in $assets) {
        $rawIn = Join-Path $rawDir ($asset.Name + ".png")
        $cutOut = Join-Path $cutDir ($asset.Name + ".png")
        Write-Host "[batch9] cut $($asset.Name)"
        & $python.Source $imageCli edit `
            --model $Model `
            --image $rawIn `
            --prompt "Remove only the background and output a clean transparent PNG cutout. Keep shape, details, colors, and edges unchanged. No new elements." `
            --size 1024x1024 `
            --quality $Quality `
            --background transparent `
            --output-format png `
            --input-fidelity high `
            --out $cutOut

        if ($LASTEXITCODE -ne 0) {
            throw "cutout failed for $($asset.Name)"
        }
    }

    Copy-Item -Force (Join-Path $cutDir "*.png") $artDir
}
else {
    Copy-Item -Force (Join-Path $rawDir "*.png") $artDir
}

Write-Host "Batch 9 set-dressing assets written to: $artDir"
