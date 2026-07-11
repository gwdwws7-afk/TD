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
$rawDir = Join-Path $projectRoot "output\imagegen\batch13_mechanic_fx_raw"
$cutDir = Join-Path $projectRoot "output\imagegen\batch13_mechanic_fx_cut"
$liveDir = Join-Path $projectRoot "output\imagegen\batch13_mechanic_fx_live"

New-Item -ItemType Directory -Force -Path $rawDir | Out-Null
New-Item -ItemType Directory -Force -Path $cutDir | Out-Null
New-Item -ItemType Directory -Force -Path $liveDir | Out-Null

$assets = @(
    @{
        Name = "fx_burrow_ambush_master"
        Prompt = "Use case: stylized-concept. Asset type: top-down 2D tower defense behavior-hint VFX source. Primary request: burrow ambush warning effect with cracked rail ring, subterranean surge sparks, and forward strike chevrons for fast special enemy telegraph. Style/medium: premium hand-painted stylized HD, gameplay readable. Composition: single centered VFX subject, transparent background, no frame, no cast shadow. Lighting: warm ember core with cyan rail-edge sparks. Constraints: no text, no watermark, no logos."
    },
    @{
        Name = "fx_spore_split_warning_master"
        Prompt = "Use case: stylized-concept. Asset type: top-down 2D tower defense behavior-hint VFX source. Primary request: spore split warning effect with pulsing pod fissures, radial split seams and bio-luminescent hazard ring for spawn-type enemy pre-split telegraph. Style/medium: premium hand-painted stylized HD. Composition: single centered VFX subject, transparent background, no frame, no cast shadow. Lighting: toxic green core with amber caution accents. Constraints: no text, no watermark, no logos."
    },
    @{
        Name = "fx_mimic_shift_master"
        Prompt = "Use case: stylized-concept. Asset type: top-down 2D tower defense behavior-hint VFX source. Primary request: mimic shift reveal effect with mirrored echo glyphs, tri-phase spectral arcs, and unstable identity pulse for adaptive special enemy telegraph. Style/medium: premium hand-painted stylized HD. Composition: single centered VFX subject, transparent background, no frame, no cast shadow. Lighting: balanced orange-blue-green spectral highlights with dark core contrast. Constraints: no text, no watermark, no logos."
    }
)

foreach ($asset in $assets) {
    $rawOut = Join-Path $rawDir ($asset.Name + ".png")
    Write-Host "[batch13-fx] generate $($asset.Name)"
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
    Write-Host "Batch 13 mechanic FX generation dry-run complete."
    return
}

if (-not $SkipCut) {
    foreach ($asset in $assets) {
        $rawIn = Join-Path $rawDir ($asset.Name + ".png")
        $cutOut = Join-Path $cutDir ($asset.Name + ".png")
        Write-Host "[batch13-fx] cut $($asset.Name)"

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

Write-Host "Batch 13 mechanic FX masters written to: $liveDir"
