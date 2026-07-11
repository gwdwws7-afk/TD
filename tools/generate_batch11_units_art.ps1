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
$rawDir = Join-Path $projectRoot "output\imagegen\batch11_units_raw"
$cutDir = Join-Path $projectRoot "output\imagegen\batch11_units_cut"
$liveDir = Join-Path $projectRoot "output\imagegen\batch11_units_live"

New-Item -ItemType Directory -Force -Path $rawDir | Out-Null
New-Item -ItemType Directory -Force -Path $cutDir | Out-Null
New-Item -ItemType Directory -Force -Path $liveDir | Out-Null

$assets = @(
    @{ Name = "tower_arc_welder_master"; Prompt = "Use case: stylized-concept. Asset type: top-down 2D tower defense tower sprite. Primary request: Arc Welder tower with chained electric coils, industrial welding core, readable silhouette for gameplay. Style/medium: premium hand-painted stylized HD. Composition: single centered subject, transparent background, no frame, no cast shadow. Lighting: cyan electric highlights over steel body. Constraints: no text, no watermark, no logos." },
    @{ Name = "tower_siege_drill_master"; Prompt = "Use case: stylized-concept. Asset type: top-down 2D tower defense tower sprite. Primary request: Siege Drill anti-armor tower with reinforced chassis and front drill cannon, clear heavy silhouette. Style/medium: premium hand-painted stylized HD. Composition: single centered subject, transparent background, no frame, no cast shadow. Lighting: warm metal highlights, soot details. Constraints: no text, no watermark, no logos." },
    @{ Name = "tower_ember_flak_master"; Prompt = "Use case: stylized-concept. Asset type: top-down 2D tower defense tower sprite. Primary request: Ember Flak rapid intercept turret with clustered barrels and ember chamber, anti-fast-unit identity. Style/medium: premium hand-painted stylized HD. Composition: single centered subject, transparent background, no frame, no cast shadow. Lighting: ember orange glow and steel contrast. Constraints: no text, no watermark, no logos." },
    @{ Name = "tower_resonance_beacon_master"; Prompt = "Use case: stylized-concept. Asset type: top-down 2D tower defense tower sprite. Primary request: Resonance Beacon support tower with ring emitters and signal pylons, tactical readable shape. Style/medium: premium hand-painted stylized HD. Composition: single centered subject, transparent background, no frame, no cast shadow. Lighting: green-cyan resonance glow. Constraints: no text, no watermark, no logos." },
    @{ Name = "tower_grav_snare_master"; Prompt = "Use case: stylized-concept. Asset type: top-down 2D tower defense tower sprite. Primary request: Grav Snare control tower with gravity node and anchor arms, strong zone-control silhouette. Style/medium: premium hand-painted stylized HD. Composition: single centered subject, transparent background, no frame, no cast shadow. Lighting: cold blue-violet field glow over dark metal. Constraints: no text, no watermark, no logos." },
    @{ Name = "enemy_burrow_sapper_master"; Prompt = "Use case: stylized-concept. Asset type: top-down 2D enemy sprite. Primary request: Burrow Sapper fast saboteur creature with digging head and aggressive forward pose, readable at small scale. Style/medium: premium hand-painted stylized HD. Composition: single centered subject, transparent background, no frame, no cast shadow. Constraints: no text, no watermark, no logos." },
    @{ Name = "enemy_ember_leech_master"; Prompt = "Use case: stylized-concept. Asset type: top-down 2D enemy sprite. Primary request: Ember Leech attrition enemy with glowing siphon core and fluid organic body, distinct silhouette. Style/medium: premium hand-painted stylized HD. Composition: single centered subject, transparent background, no frame, no cast shadow. Constraints: no text, no watermark, no logos." },
    @{ Name = "enemy_spore_carrier_master"; Prompt = "Use case: stylized-concept. Asset type: top-down 2D enemy sprite. Primary request: Spore Carrier enemy with multiple split sacs and heavy pod body, readable threat profile. Style/medium: premium hand-painted stylized HD. Composition: single centered subject, transparent background, no frame, no cast shadow. Constraints: no text, no watermark, no logos." },
    @{ Name = "enemy_rail_warden_master"; Prompt = "Use case: stylized-concept. Asset type: top-down 2D enemy sprite. Primary request: Rail Warden armored support unit with shield projection rig and industrial plating. Style/medium: premium hand-painted stylized HD. Composition: single centered subject, transparent background, no frame, no cast shadow. Constraints: no text, no watermark, no logos." },
    @{ Name = "enemy_cinder_glider_master"; Prompt = "Use case: stylized-concept. Asset type: top-down 2D enemy sprite. Primary request: Cinder Glider fast flank enemy with lateral wing-like fins and ember trail cues, clear motion silhouette. Style/medium: premium hand-painted stylized HD. Composition: single centered subject, transparent background, no frame, no cast shadow. Constraints: no text, no watermark, no logos." },
    @{ Name = "enemy_husk_titan_master"; Prompt = "Use case: stylized-concept. Asset type: top-down 2D enemy sprite. Primary request: Husk Titan elite heavy enemy with massive armored shell and furnace scars, boss-adjacent presence. Style/medium: premium hand-painted stylized HD. Composition: single centered subject, transparent background, no frame, no cast shadow. Constraints: no text, no watermark, no logos." },
    @{ Name = "enemy_echo_mimic_master"; Prompt = "Use case: stylized-concept. Asset type: top-down 2D enemy sprite. Primary request: Echo Mimic adaptive enemy with mirrored motifs and unstable core, mixed-threat readability. Style/medium: premium hand-painted stylized HD. Composition: single centered subject, transparent background, no frame, no cast shadow. Constraints: no text, no watermark, no logos." },
    @{ Name = "enemy_furnace_matriarch_master"; Prompt = "Use case: stylized-concept. Asset type: top-down 2D enemy sprite. Primary request: Furnace Matriarch final boss unit, layered furnace anatomy with commanding silhouette and ember vents. Style/medium: premium hand-painted stylized HD. Composition: single centered subject, transparent background, no frame, no cast shadow. Constraints: no text, no watermark, no logos." }
)

foreach ($asset in $assets) {
    $rawOut = Join-Path $rawDir ($asset.Name + ".png")
    Write-Host "[batch11-units] generate $($asset.Name)"
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
    Write-Host "Batch 11 unit generation dry-run complete."
    return
}

if (-not $SkipCut) {
    foreach ($asset in $assets) {
        $rawIn = Join-Path $rawDir ($asset.Name + ".png")
        $cutOut = Join-Path $cutDir ($asset.Name + ".png")
        Write-Host "[batch11-units] cut $($asset.Name)"
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

Write-Host "Batch 11 unit masters written to: $liveDir"
