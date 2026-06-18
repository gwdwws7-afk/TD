param(
    [string]$Model = "gpt-image-1.5",
    [string]$Quality = "high",
    [switch]$SkipCut,
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
$rawDir = Join-Path $projectRoot "output\imagegen\batch10_props_raw"
$cutDir = Join-Path $projectRoot "output\imagegen\batch10_props_cut"
$artDir = Join-Path $projectRoot "Assets\Resources\Art"

New-Item -ItemType Directory -Force -Path $rawDir | Out-Null
New-Item -ItemType Directory -Force -Path $cutDir | Out-Null
New-Item -ItemType Directory -Force -Path $artDir | Out-Null

$assets = @(
    @{ Name = "prop_anchor_grayline_junction_a"; Prompt = "Use case: stylized-concept. Asset type: game prop sprite. Primary request: top-down broken multi-switch railway lever cluster, iconic landmark for Grayline Junction map. Style/medium: premium hand-painted stylized. Composition: single centered prop, transparent background, no text, no watermark." },
    @{ Name = "prop_anchor_grayline_junction_b"; Prompt = "Use case: stylized-concept. Asset type: game prop sprite. Primary request: top-down rusted signal relay tower with mechanical switch gears, landmark prop for Grayline Junction. Style/medium: premium hand-painted stylized. Composition: single centered prop, transparent background, no text, no watermark." },
    @{ Name = "prop_grayline_junction_a"; Prompt = "Use case: stylized-concept. Asset type: game prop sprite. Primary request: top-down compact rail toolbox stack and warning lamp, supporting prop for Grayline Junction. Style/medium: premium hand-painted stylized. Composition: single centered prop, transparent background, no text, no watermark." },
    @{ Name = "prop_grayline_junction_b"; Prompt = "Use case: stylized-concept. Asset type: game prop sprite. Primary request: top-down cracked track maintenance crate with tools, supporting prop for Grayline Junction. Style/medium: premium hand-painted stylized. Composition: single centered prop, transparent background, no text, no watermark." },

    @{ Name = "prop_anchor_ashfall_depot_a"; Prompt = "Use case: stylized-concept. Asset type: game prop sprite. Primary request: top-down heavy freight loading crane base, iconic landmark for Ashfall Depot map. Style/medium: premium hand-painted stylized. Composition: single centered prop, transparent background, no text, no watermark." },
    @{ Name = "prop_anchor_ashfall_depot_b"; Prompt = "Use case: stylized-concept. Asset type: game prop sprite. Primary request: top-down burned cargo gantry with hanging chains, landmark prop for Ashfall Depot. Style/medium: premium hand-painted stylized. Composition: single centered prop, transparent background, no text, no watermark." },
    @{ Name = "prop_ashfall_depot_a"; Prompt = "Use case: stylized-concept. Asset type: game prop sprite. Primary request: top-down stacked freight pallets wrapped with hazard tape, supporting prop for Ashfall Depot. Style/medium: premium hand-painted stylized. Composition: single centered prop, transparent background, no text, no watermark." },
    @{ Name = "prop_ashfall_depot_b"; Prompt = "Use case: stylized-concept. Asset type: game prop sprite. Primary request: top-down scorched cargo drum cluster and ash residue, supporting prop for Ashfall Depot. Style/medium: premium hand-painted stylized. Composition: single centered prop, transparent background, no text, no watermark." },

    @{ Name = "prop_anchor_split_switch_canyon_a"; Prompt = "Use case: stylized-concept. Asset type: game prop sprite. Primary request: top-down split-track control monolith embedded in canyon rock, iconic landmark for Split-Switch Canyon. Style/medium: premium hand-painted stylized. Composition: single centered prop, transparent background, no text, no watermark." },
    @{ Name = "prop_anchor_split_switch_canyon_b"; Prompt = "Use case: stylized-concept. Asset type: game prop sprite. Primary request: top-down fractured signal arch spanning broken rails, landmark prop for Split-Switch Canyon. Style/medium: premium hand-painted stylized. Composition: single centered prop, transparent background, no text, no watermark." },
    @{ Name = "prop_split_switch_canyon_a"; Prompt = "Use case: stylized-concept. Asset type: game prop sprite. Primary request: top-down canyon barricade with bolted steel braces, supporting prop for Split-Switch Canyon. Style/medium: premium hand-painted stylized. Composition: single centered prop, transparent background, no text, no watermark." },
    @{ Name = "prop_split_switch_canyon_b"; Prompt = "Use case: stylized-concept. Asset type: game prop sprite. Primary request: top-down switch-side debris pile with rail fragments, supporting prop for Split-Switch Canyon. Style/medium: premium hand-painted stylized. Composition: single centered prop, transparent background, no text, no watermark." },

    @{ Name = "prop_anchor_hollow_kiln_basin_a"; Prompt = "Use case: stylized-concept. Asset type: game prop sprite. Primary request: top-down circular kiln reactor shell with glowing vents, iconic landmark for Hollow Kiln Basin. Style/medium: premium hand-painted stylized. Composition: single centered prop, transparent background, no text, no watermark." },
    @{ Name = "prop_anchor_hollow_kiln_basin_b"; Prompt = "Use case: stylized-concept. Asset type: game prop sprite. Primary request: top-down shattered furnace tower with ring conduits, landmark prop for Hollow Kiln Basin. Style/medium: premium hand-painted stylized. Composition: single centered prop, transparent background, no text, no watermark." },
    @{ Name = "prop_hollow_kiln_basin_a"; Prompt = "Use case: stylized-concept. Asset type: game prop sprite. Primary request: top-down ash vent machinery and pipe cluster, supporting prop for Hollow Kiln Basin. Style/medium: premium hand-painted stylized. Composition: single centered prop, transparent background, no text, no watermark." },
    @{ Name = "prop_hollow_kiln_basin_b"; Prompt = "Use case: stylized-concept. Asset type: game prop sprite. Primary request: top-down cracked furnace crate and coal debris, supporting prop for Hollow Kiln Basin. Style/medium: premium hand-painted stylized. Composition: single centered prop, transparent background, no text, no watermark." },

    @{ Name = "prop_anchor_last_ember_terminus_a"; Prompt = "Use case: stylized-concept. Asset type: game prop sprite. Primary request: top-down final terminal gate with ember core sockets, iconic landmark for Last Ember Terminus. Style/medium: premium hand-painted stylized. Composition: single centered prop, transparent background, no text, no watermark." },
    @{ Name = "prop_anchor_last_ember_terminus_b"; Prompt = "Use case: stylized-concept. Asset type: game prop sprite. Primary request: top-down armored command pylon with ember beacon, landmark prop for Last Ember Terminus. Style/medium: premium hand-painted stylized. Composition: single centered prop, transparent background, no text, no watermark." },
    @{ Name = "prop_last_ember_terminus_a"; Prompt = "Use case: stylized-concept. Asset type: game prop sprite. Primary request: top-down terminal barricade with heavy armor plates, supporting prop for Last Ember Terminus. Style/medium: premium hand-painted stylized. Composition: single centered prop, transparent background, no text, no watermark." },
    @{ Name = "prop_last_ember_terminus_b"; Prompt = "Use case: stylized-concept. Asset type: game prop sprite. Primary request: top-down reactor maintenance kit and cable rack, supporting prop for Last Ember Terminus. Style/medium: premium hand-painted stylized. Composition: single centered prop, transparent background, no text, no watermark." }
)

foreach ($asset in $assets) {
    $rawOut = Join-Path $rawDir ($asset.Name + ".png")
    Write-Host "[batch10-props] generate $($asset.Name)"
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
    Write-Host "Batch 10 map props dry-run complete."
    return
}

if (-not $SkipCut) {
    foreach ($asset in $assets) {
        $rawIn = Join-Path $rawDir ($asset.Name + ".png")
        $cutOut = Join-Path $cutDir ($asset.Name + ".png")
        Write-Host "[batch10-props] cut $($asset.Name)"
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

    Copy-Item -Force (Join-Path $cutDir "*.png") $artDir
}
else {
    Copy-Item -Force (Join-Path $rawDir "*.png") $artDir
}

Write-Host "Batch 10 map props written to: $artDir"
