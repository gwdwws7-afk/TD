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
$rawDir = Join-Path $projectRoot "output\imagegen\batch12_fx_raw"
$cutDir = Join-Path $projectRoot "output\imagegen\batch12_fx_cut"
$liveDir = Join-Path $projectRoot "output\imagegen\batch12_fx_live"

New-Item -ItemType Directory -Force -Path $rawDir | Out-Null
New-Item -ItemType Directory -Force -Path $cutDir | Out-Null
New-Item -ItemType Directory -Force -Path $liveDir | Out-Null

$assets = @(
    @{
        Name = "fx_enemy_hit_master"
        Prompt = "Use case: stylized-concept. Asset type: top-down 2D tower defense VFX sprite source. Primary request: enemy hit reaction burst with ember-core shock ring, metallic shards and high readability at small scale. Style/medium: premium hand-painted stylized HD with clean edge control. Composition: single centered VFX subject, transparent background, no frame, no cast shadow. Lighting: bright warm impact center with cyan rail-spark accents. Constraints: no text, no watermark, no logos."
    },
    @{
        Name = "fx_enemy_death_master"
        Prompt = "Use case: stylized-concept. Asset type: top-down 2D tower defense VFX sprite source. Primary request: enemy death dissolve blast with ember smoke plume, ash fragments and radial release energy. Style/medium: premium hand-painted stylized HD with cinematic clarity. Composition: single centered VFX subject, transparent background, no frame, no cast shadow. Lighting: orange-red furnace bloom with dark ash breakup. Constraints: no text, no watermark, no logos."
    },
    @{
        Name = "fx_boss_warning_master"
        Prompt = "Use case: stylized-concept. Asset type: top-down 2D tower defense warning VFX sprite source. Primary request: boss warning telegraph effect with industrial hazard rings, alarm chevrons and rail-sigil pulse, highly legible gameplay marker. Style/medium: premium hand-painted stylized HD. Composition: single centered VFX subject, transparent background, no frame, no cast shadow. Lighting: amber warning glow with steel-blue edge energy. Constraints: no text, no watermark, no logos."
    }
)

foreach ($asset in $assets) {
    $rawOut = Join-Path $rawDir ($asset.Name + ".png")
    Write-Host "[batch12-fx] generate $($asset.Name)"
    $args = @(
        $imageCli,
        "generate",
        "--model", $Model,
        "--prompt", $asset.Prompt,
        "--no-augment",
        "--size", "1024x1024",
        "--quality", $Quality,
        "--background", "transparent",
        "--output-format", "png",
        "--out", $rawOut,
        "--force"
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
    Write-Host "Batch 12 FX generation dry-run complete."
    return
}

if (-not $SkipCut) {
    foreach ($asset in $assets) {
        $rawIn = Join-Path $rawDir ($asset.Name + ".png")
        $cutOut = Join-Path $cutDir ($asset.Name + ".png")
        Write-Host "[batch12-fx] cut $($asset.Name)"

        & $python.Source $imageCli edit `
            --model $Model `
            --image $rawIn `
            --prompt "Remove only the background and output a clean transparent PNG cutout. Keep shape, details, colors, and edges unchanged. No new elements." `
            --no-augment `
            --size 1024x1024 `
            --quality $Quality `
            --background transparent `
            --output-format png `
            --input-fidelity high `
            --out $cutOut `
            --force

        if ($LASTEXITCODE -ne 0) {
            throw "cutout failed for $($asset.Name)"
        }
    }

    Copy-Item -Force (Join-Path $cutDir "*.png") $liveDir
}
else {
    Copy-Item -Force (Join-Path $rawDir "*.png") $liveDir
}

Write-Host "Batch 12 FX masters written to: $liveDir"
