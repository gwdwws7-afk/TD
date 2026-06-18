param(
    [string]$Model = "gpt-image-1.5",
    [string]$Quality = "high",
    [switch]$SkipLiveGeneration,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python) {
    throw "python is required but was not found in PATH."
}

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$genScript = Join-Path $projectRoot "tools\generate_batch13_mechanic_fx_art.ps1"
$buildScript = Join-Path $projectRoot "tools\build_batch13_mechanic_fx_frames.py"

if (-not $SkipLiveGeneration) {
    $genArgs = @(
        "-ExecutionPolicy", "Bypass",
        "-File", $genScript,
        "-Model", $Model,
        "-Quality", $Quality
    )
    if ($DryRun) {
        $genArgs += "-DryRun"
    }

    Write-Host "[batch13-fx] stage 1: generate masters"
    & powershell @genArgs
    if ($LASTEXITCODE -ne 0) {
        throw "batch13 mechanic fx master generation failed"
    }
}

if ($DryRun) {
    Write-Host "Batch 13 mechanic FX pipeline dry-run complete."
    return
}

Write-Host "[batch13-fx] stage 2: build animation frames"
& $python.Source $buildScript
if ($LASTEXITCODE -ne 0) {
    throw "batch13 mechanic fx frame build failed"
}

Write-Host "Batch 13 mechanic FX pipeline completed."
