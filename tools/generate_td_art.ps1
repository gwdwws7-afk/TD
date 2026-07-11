param(
    [switch]$DryRun,
    [string]$Model = "gpt-image-1.5",
    [string]$Quality = "high"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "td_imagegen_common.ps1")

function Resolve-CodexHome {
    if (-not [string]::IsNullOrWhiteSpace($env:CODEX_HOME)) {
        return $env:CODEX_HOME
    }

    return Join-Path $HOME ".codex"
}

$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python) {
    throw "python is required but was not found in PATH."
}

$imageCli = Resolve-TDImageGenCli
Import-TDOpenAIApiKey -Required (-not $DryRun.IsPresent)

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$outDir = Join-Path $projectRoot "Assets\Resources\Art"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$assets = @(
    @{
        Name = "tile_grass"
        Prompt = "Use case: stylized-concept. Asset type: game tile texture. Primary request: top-down square grass tile for a 2D tower defense game. Style/medium: hand-painted stylized texture. Composition/framing: centered square tile, seamless edges, no perspective. Lighting/mood: neutral daylight. Constraints: seamless tiling, no text, no logos, no watermark."
    },
    @{
        Name = "tile_path"
        Prompt = "Use case: stylized-concept. Asset type: game tile texture. Primary request: top-down square dirt road tile for a 2D tower defense game. Style/medium: hand-painted stylized texture. Composition/framing: centered square tile, seamless edges, no perspective. Lighting/mood: neutral daylight. Constraints: seamless tiling, no text, no logos, no watermark."
    },
    @{
        Name = "build_marker"
        Prompt = "Use case: stylized-concept. Asset type: game UI icon. Primary request: simple buildable-cell marker for tower defense, geometric ring badge. Style/medium: clean painted game icon. Composition/framing: centered icon with transparent background and generous padding. Constraints: no text, no logos, no watermark."
    },
    @{
        Name = "tower_basic"
        Prompt = "Use case: stylized-concept. Asset type: game character concept. Primary request: top-down basic arrow tower for a 2D tower defense game. Style/medium: stylized hand-painted sprite-ready render. Composition/framing: single tower centered, transparent background. Constraints: no text, no logos, no watermark."
    },
    @{
        Name = "enemy_slime"
        Prompt = "Use case: stylized-concept. Asset type: game character concept. Primary request: top-down red slime enemy for a 2D tower defense game. Style/medium: stylized hand-painted sprite-ready render. Composition/framing: single enemy centered, transparent background. Constraints: no text, no logos, no watermark."
    },
    @{
        Name = "projectile_bolt"
        Prompt = "Use case: stylized-concept. Asset type: game UI icon. Primary request: small glowing projectile bolt for tower defense. Style/medium: clean painted game icon. Composition/framing: centered icon, transparent background. Constraints: no text, no logos, no watermark."
    }
)

foreach ($asset in $assets) {
    $outFile = Join-Path $outDir ($asset.Name + ".png")
    $args = @(
        $imageCli,
        "generate",
        "--prompt", $asset.Prompt,
        "--model", $Model,
        "--size", "1024x1024",
        "--quality", $Quality,
        "--background", "transparent",
        "--output-format", "png",
        "--out", $outFile
    )

    if ($DryRun) {
        $args += "--dry-run"
    }

    Write-Host "[image2.0] Generating $($asset.Name) ..."
    & $python.Source @args
    if ($LASTEXITCODE -ne 0) {
        throw "Generation failed for $($asset.Name)"
    }
}

Write-Host "Done. Assets written to: $outDir"
