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
$rawDir = Join-Path $projectRoot "output\imagegen\batch14_enemy_mechanic_fx_raw"
$cutDir = Join-Path $projectRoot "output\imagegen\batch14_enemy_mechanic_fx_cut"
$liveDir = Join-Path $projectRoot "output\imagegen\batch14_enemy_mechanic_fx_live"

New-Item -ItemType Directory -Force -Path $rawDir | Out-Null
New-Item -ItemType Directory -Force -Path $cutDir | Out-Null
New-Item -ItemType Directory -Force -Path $liveDir | Out-Null

$assets = @(
    @{
        Name = "fx_attrition_siphon_master"
        Prompt = "Use case: stylized-concept. Asset type: top-down 2D tower defense behavior-hint VFX source. Primary request: attrition siphon warning effect for ember leech enemy, with draining ember spiral, resource-leak pulse ring, and danger readability. Style/medium: premium hand-painted stylized HD. Composition: single centered VFX subject, transparent background, no frame, no cast shadow. Lighting: hot orange-red core with smoky dark rim. Constraints: no text, no watermark, no logos."
    },
    @{
        Name = "fx_support_link_master"
        Prompt = "Use case: stylized-concept. Asset type: top-down 2D tower defense behavior-hint VFX source. Primary request: support-link pulse effect for rail warden enemy, with defensive rail arcs, concentric shield ring and allied buff telegraph clarity. Style/medium: premium hand-painted stylized HD. Composition: single centered VFX subject, transparent background, no frame, no cast shadow. Lighting: cool blue-cyan shielding with steel highlights. Constraints: no text, no watermark, no logos."
    },
    @{
        Name = "fx_elite_pressure_master"
        Prompt = "Use case: stylized-concept. Asset type: top-down 2D tower defense behavior-hint VFX source. Primary request: elite pressure surge effect for husk titan enemy, with furnace fracture pulse, heavy intimidation ring and burst-window readability. Style/medium: premium hand-painted stylized HD. Composition: single centered VFX subject, transparent background, no frame, no cast shadow. Lighting: intense amber-gold flare with dark metallic contrast. Constraints: no text, no watermark, no logos."
    }
)

foreach ($asset in $assets) {
    $rawOut = Join-Path $rawDir ($asset.Name + ".png")
    Write-Host "[batch14-fx] generate $($asset.Name)"
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
    Write-Host "Batch 14 enemy mechanic FX generation dry-run complete."
    return
}

if (-not $SkipCut) {
    foreach ($asset in $assets) {
        $rawIn = Join-Path $rawDir ($asset.Name + ".png")
        $cutOut = Join-Path $cutDir ($asset.Name + ".png")
        Write-Host "[batch14-fx] cut $($asset.Name)"

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

Write-Host "Batch 14 enemy mechanic FX masters written to: $liveDir"
