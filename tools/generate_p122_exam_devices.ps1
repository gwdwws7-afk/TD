param(
    [string]$Model = "gpt-image-1.5",
    [string]$Quality = "high",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "td_imagegen_common.ps1")

Import-TDOpenAIApiKey -Required (-not $DryRun.IsPresent)
$imageCli = Resolve-TDImageGenCli
$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$venvPython = Join-Path $projectRoot ".venv\Scripts\python.exe"
$pythonPath = if (Test-Path -LiteralPath $venvPython) {
    $venvPython
} else {
    (Get-Command python -ErrorAction Stop).Source
}
$rawDirectory = Join-Path $projectRoot "output\imagegen\p122_exam_devices_raw"
$liveDirectory = Join-Path $projectRoot "Assets\Resources\Art\Exam\P12"
New-Item -ItemType Directory -Force -Path $rawDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $liveDirectory | Out-Null

$sharedStyle = @"
Use case: game-asset. Asset type: top-down three-quarter 2D tower-defense environment device sprite.
Style: premium hand-painted stylized industrial fantasy, blackened iron, aged brass, ember heat,
crisp gameplay silhouette, matching a polished Kingdom Rush-scale battlefield prop.
Composition: one complete centered device, isolated, generous transparent padding, no terrain tile,
no frame, no text, no symbols, no cast shadow, no cut-off parts. Camera: fixed top-down three-quarter
view around 55 degrees, orthographic feeling. Lighting: warm directional key with cool readable rim.
Constraints: transparent background, no watermark, no logo, no UI, no characters.
"@

$assets = @(
    @{
        Name = "device_reserve_train"
        Prompt = "A compact armored reserve-train dispatch apparatus: brass timetable drum, short rail-coupled supply capsule, cyan signal lamp, heavy brake lever and pressure pipes. The silhouette should immediately communicate delayed railway reinforcement and stored supplies."
    },
    @{
        Name = "device_canyon_switch"
        Prompt = "A rugged canyon railway points machine: forked switch blades, protected mechanical lever housing, three route indicator shutters, green signal lens, exposed gears and a sturdy track-side base. The silhouette should immediately communicate choosing one of three rail routes."
    },
    @{
        Name = "device_kiln_purge"
        Prompt = "A squat kiln purge manifold: circular furnace vent, orange ceramic heat core, armored pressure valve, four exhaust vanes, rupture bolts and cyan safety gauge. The silhouette should immediately communicate storing pressure and releasing one powerful area purge."
    },
    @{
        Name = "device_phase_breaker"
        Prompt = "A heavy phase-breaker relay: concentric brass induction rings around a dark iron core, twin piston clamps, cyan calibration coils, ember overload channels and two clearly separated charge capacitors. The silhouette should communicate interrupting a boss transformation threshold."
    }
)

foreach ($asset in $assets) {
    $rawPath = Join-Path $rawDirectory ($asset.Name + ".png")
    $livePath = Join-Path $liveDirectory ($asset.Name + ".png")
    Write-Host "[P12.2] generate $($asset.Name)"
    $arguments = @(
        $imageCli,
        "generate",
        "--model", $Model,
        "--prompt", ($sharedStyle + "`nPrimary request: " + $asset.Prompt),
        "--no-augment",
        "--size", "1024x1024",
        "--quality", $Quality,
        "--background", "transparent",
        "--output-format", "png",
        "--out", $rawPath,
        "--force"
    )
    if ($DryRun) {
        $arguments += "--dry-run"
    }

    & $pythonPath @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Image generation failed for $($asset.Name)."
    }

    if (-not $DryRun) {
        Copy-Item -LiteralPath $rawPath -Destination $livePath -Force
    }
}

Write-Host "P12.2 exam devices written to $liveDirectory"
