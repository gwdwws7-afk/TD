param(
    [string]$PlayerPath = "E:/TD/output/builds/p1254_il2cpp/EmberlineDefense.exe",
    [string]$OutputRoot = "E:/TD/output/playtest/p1254_release_gate",
    [ValidateRange(1200, 3600)]
    [int]$SoakSeconds = 1200,
    [ValidateRange(12, 96)]
    [int]$TargetEnemies = 36,
    [ValidateRange(1, 3)]
    [float]$TimeScale = 1,
    [ValidateRange(15, 120)]
    [float]$TargetAverageFps = 55,
    [ValidateRange(256, 8192)]
    [int]$MaximumMemoryMegabytes = 1536,
    [ValidateRange(1024, 49151)]
    [int]$TelemetryPort = 18454
)

$ErrorActionPreference = "Stop"
$startedUtc = [DateTime]::UtcNow
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$workspacePrefix = $projectRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
$outputFull = [IO.Path]::GetFullPath($OutputRoot)
$playerFull = [IO.Path]::GetFullPath($PlayerPath)
if (-not $outputFull.StartsWith($workspacePrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputRoot must stay inside the workspace: $outputFull"
}
if (-not (Test-Path -LiteralPath $playerFull -PathType Leaf)) {
    throw "IL2CPP player was not found: $playerFull"
}
$playerRoot = Split-Path -Parent $playerFull
$gameAssemblyPath = Join-Path $playerRoot "GameAssembly.dll"
if (-not (Test-Path -LiteralPath $gameAssemblyPath -PathType Leaf)) {
    throw "GameAssembly.dll was not found beside the player: $gameAssemblyPath"
}
if (Test-Path -LiteralPath (Join-Path $playerRoot "MonoBleedingEdge")) {
    throw "P12.5.4 release gate requires an IL2CPP player."
}

New-Item -ItemType Directory -Path $outputFull -Force | Out-Null
$cloudReportPath = Join-Path $outputFull "cloud-player-report.json"
$cloudLogPath = Join-Path $outputFull "cloud-player.log"
$telemetryReportPath = Join-Path $outputFull "telemetry-player-report.json"
$telemetryLogPath = Join-Path $outputFull "telemetry-player.log"
$telemetryCapturePath = Join-Path $outputFull "telemetry-capture.ndjson"
$telemetrySummaryPath = Join-Path $outputFull "telemetry-summary.json"
$soakReportPath = Join-Path $outputFull "soak-player-report.json"
$soakLogPath = Join-Path $outputFull "soak-player.log"
$auditPath = Join-Path $outputFull "p1254_release_gate.json"
$markdownPath = Join-Path $outputFull "p1254_release_gate.md"
$prefsBackupPath = Join-Path $outputFull "playerprefs-before-gate.reg"
$persistentBackupRoot = Join-Path $outputFull "persistent-backup"
$playerPrefsKey = "HKCU\Software\Emberline\Emberline Defense"
$internetSettingsPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings"
$originalProxyEnable = [int](
    Get-ItemPropertyValue -LiteralPath $internetSettingsPath -Name "ProxyEnable" -ErrorAction SilentlyContinue
)
$proxyRestorePending = $false
$persistentRoot = [IO.Path]::GetFullPath(
    (Join-Path $env:USERPROFILE "AppData/LocalLow/Emberline/Emberline Defense"))
$persistentPrefix = $persistentRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
$persistentNames = @("CampaignRecovery", "Diagnostics", "TelemetryQueue")
$mockServer = $null
$restoreErrors = @()
$gateError = ""
$cloudReport = $null
$telemetryReport = $null
$telemetrySummary = $null
$soakReport = $null

foreach ($path in @(
    $cloudReportPath,
    $cloudLogPath,
    $telemetryReportPath,
    $telemetryLogPath,
    $telemetryCapturePath,
    $telemetrySummaryPath,
    $soakReportPath,
    $soakLogPath,
    $auditPath,
    $markdownPath
)) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Force
    }
}

$hadPlayerPrefs = (& reg.exe query $playerPrefsKey 2>$null) -ne $null
if ($hadPlayerPrefs) {
    & reg.exe export $playerPrefsKey $prefsBackupPath /y | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Could not back up PlayerPrefs before the P12.5.4 gate."
    }
}

$persistentState = @{}
if (Test-Path -LiteralPath $persistentBackupRoot) {
    $resolvedBackupRoot = (Resolve-Path -LiteralPath $persistentBackupRoot).Path
    if (-not $resolvedBackupRoot.StartsWith($workspacePrefix, [StringComparison]::OrdinalIgnoreCase) -or
        $resolvedBackupRoot -eq $projectRoot -or
        $resolvedBackupRoot -eq $outputFull) {
        throw "Refusing to clean unsafe persistent backup path: $resolvedBackupRoot"
    }
    Remove-Item -LiteralPath $resolvedBackupRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $persistentBackupRoot -Force | Out-Null
foreach ($name in $persistentNames) {
    $source = [IO.Path]::GetFullPath((Join-Path $persistentRoot $name))
    if (-not $source.StartsWith($persistentPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Unsafe persistent path: $source"
    }
    $present = Test-Path -LiteralPath $source -PathType Container
    $persistentState[$name] = $present
    if ($present) {
        Copy-Item -LiteralPath $source -Destination (Join-Path $persistentBackupRoot $name) -Recurse -Force
    }
}

function Invoke-P1254Player {
    param(
        [string]$Arguments,
        [string]$ReportPath,
        [int]$TimeoutSeconds,
        [string]$Label
    )

    Write-Output "[P12.5.4] Running $Label..."
    $process = Start-Process `
        -FilePath $playerFull `
        -ArgumentList $Arguments `
        -PassThru `
        -WindowStyle Hidden
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        Stop-Process -Id $process.Id -Force
        throw "$Label timed out after $TimeoutSeconds seconds."
    }
    if (-not (Test-Path -LiteralPath $ReportPath -PathType Leaf)) {
        throw "$Label did not write its Player report."
    }
    $report = Get-Content -LiteralPath $ReportPath -Raw | ConvertFrom-Json
    if ($process.ExitCode -ne 0 -or -not [bool]$report.passed) {
        throw "$Label failed with exit code $($process.ExitCode). Inspect $ReportPath"
    }
    return $report
}

try {
    $cloudArguments = "-screen-fullscreen 0 -screen-width 1280 -screen-height 720 " +
                      "-logFile `"$cloudLogPath`" --td-p1254-cloud-test " +
                      "--td-p1254-report `"$cloudReportPath`" --td-smoke-level 20"
    $cloudReport = Invoke-P1254Player `
        -Arguments $cloudArguments `
        -ReportPath $cloudReportPath `
        -TimeoutSeconds 120 `
        -Label "three-slot cloud conflict and migration matrix"

    $python = Get-Command python -ErrorAction Stop
    $serverScript = Join-Path $PSScriptRoot "td_p1254_mock_telemetry_server.py"
    $serverArguments = "`"$serverScript`" --host 127.0.0.1 --port $TelemetryPort " +
                       "--capture `"$telemetryCapturePath`" --summary `"$telemetrySummaryPath`" --fail-first 1"
    $mockServer = Start-Process `
        -FilePath $python.Source `
        -ArgumentList $serverArguments `
        -PassThru `
        -WindowStyle Hidden
    $healthUri = "http://127.0.0.1:$TelemetryPort/health"
    $healthReady = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        try {
            $health = Invoke-WebRequest -Uri $healthUri -UseBasicParsing -TimeoutSec 2
            if ($health.StatusCode -eq 200) {
                $healthReady = $true
                break
            }
        }
        catch {
            Start-Sleep -Milliseconds 250
        }
    }
    if (-not $healthReady) {
        throw "P12.5.4 mock telemetry endpoint did not become ready."
    }

    $telemetryEndpoint = "http://localhost:$TelemetryPort/v1/events"
    $telemetryArguments = "-screen-fullscreen 0 -screen-width 1280 -screen-height 720 " +
                          "-logFile `"$telemetryLogPath`" --td-p1254-telemetry-test " +
                          "--td-p1254-report `"$telemetryReportPath`" --td-smoke-level 20 " +
                          "--td-telemetry-consent 1 --td-telemetry-allow-loopback-http " +
                          "--td-telemetry-endpoint `"$telemetryEndpoint`" --td-telemetry-test-timeout 60"
    try {
        Set-ItemProperty `
            -LiteralPath $internetSettingsPath `
            -Name "ProxyEnable" `
            -Value 0 `
            -Type DWord
        $proxyRestorePending = $true
        Start-Sleep -Milliseconds 250
        $telemetryReport = Invoke-P1254Player `
            -Arguments $telemetryArguments `
            -ReportPath $telemetryReportPath `
            -TimeoutSeconds 120 `
            -Label "consent, redaction, offline retry and upload transport"
    }
    finally {
        Set-ItemProperty `
            -LiteralPath $internetSettingsPath `
            -Name "ProxyEnable" `
            -Value $originalProxyEnable `
            -Type DWord
        $proxyRestorePending = $false
    }
    Start-Sleep -Milliseconds 500
    if (-not (Test-Path -LiteralPath $telemetrySummaryPath -PathType Leaf)) {
        throw "Mock telemetry server did not write its validation summary."
    }
    $telemetrySummary = Get-Content -LiteralPath $telemetrySummaryPath -Raw | ConvertFrom-Json
    if (-not [bool]$telemetrySummary.passed) {
        throw "Telemetry payload validation failed. Inspect $telemetrySummaryPath"
    }

    if ($null -ne $mockServer -and -not $mockServer.HasExited) {
        Stop-Process -Id $mockServer.Id -Force
        $mockServer = $null
    }

    $scaleToken = $TimeScale.ToString([Globalization.CultureInfo]::InvariantCulture)
    $soakTimeout = $SoakSeconds + 480
    $soakArguments = "-screen-fullscreen 0 -screen-width 1280 -screen-height 720 " +
                     "-logFile `"$soakLogPath`" --td-p1254-soak-test " +
                     "--td-p1254-report `"$soakReportPath`" --td-smoke-level 20 " +
                     "--td-soak-seconds $SoakSeconds --td-soak-target-enemies $TargetEnemies " +
                     "--td-soak-time-scale $scaleToken --td-soak-warmup-wave 10 " +
                     "--td-soak-warmup-scale 16 --td-soak-warmup-timeout 360 " +
                     "--td-soak-target-fps $TargetAverageFps " +
                     "--td-soak-max-memory-mb $MaximumMemoryMegabytes"
    $soakReport = Invoke-P1254Player `
        -Arguments $soakArguments `
        -ReportPath $soakReportPath `
        -TimeoutSeconds $soakTimeout `
        -Label "$SoakSeconds-second same-process L20 combat soak"
}
catch {
    $gateError = $_.Exception.Message
}
finally {
    if ($null -ne $mockServer -and -not $mockServer.HasExited) {
        Stop-Process -Id $mockServer.Id -Force
    }
    if ($proxyRestorePending) {
        try {
            Set-ItemProperty `
                -LiteralPath $internetSettingsPath `
                -Name "ProxyEnable" `
                -Value $originalProxyEnable `
                -Type DWord
            $proxyRestorePending = $false
        }
        catch {
            $restoreErrors += "System proxy: $($_.Exception.Message)"
        }
    }

    try {
        & reg.exe delete $playerPrefsKey /f 2>$null | Out-Null
        if ($hadPlayerPrefs) {
            & reg.exe import $prefsBackupPath | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "PlayerPrefs registry restore returned exit code $LASTEXITCODE."
            }
        }
    }
    catch {
        $restoreErrors += $_.Exception.Message
    }

    foreach ($name in $persistentNames) {
        try {
            $target = [IO.Path]::GetFullPath((Join-Path $persistentRoot $name))
            if (-not $target.StartsWith($persistentPrefix, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Unsafe persistent restore target: $target"
            }
            if (Test-Path -LiteralPath $target) {
                Remove-Item -LiteralPath $target -Recurse -Force
            }
            if ([bool]$persistentState[$name]) {
                Copy-Item `
                    -LiteralPath (Join-Path $persistentBackupRoot $name) `
                    -Destination $target `
                    -Recurse `
                    -Force
            }
        }
        catch {
            $restoreErrors += "$name`: $($_.Exception.Message)"
        }
    }
}

$cloudPassed = $null -ne $cloudReport -and
               [bool]$cloudReport.passed -and
               [bool]$cloudReport.cloudMatrix.passed -and
               @($cloudReport.cloudMatrix.rows).Count -eq 3
$telemetryPassed = $null -ne $telemetryReport -and
                   [bool]$telemetryReport.passed -and
                   $null -ne $telemetrySummary -and
                   [bool]$telemetrySummary.passed
$soakPassed = $null -ne $soakReport -and
              [bool]$soakReport.passed -and
              [bool]$soakReport.soak.shippingDurationPassed -and
              [bool]$soakReport.soak.sustainedCombatPassed
$systemProxyRestored = [int](
    Get-ItemPropertyValue `
        -LiteralPath $internetSettingsPath `
        -Name "ProxyEnable" `
        -ErrorAction SilentlyContinue
) -eq $originalProxyEnable
$audit = [ordered]@{
    schemaVersion = "p1254-release-gate-v1"
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    elapsedSeconds = [Math]::Round(([DateTime]::UtcNow - $startedUtc).TotalSeconds, 2)
    player = $playerFull
    playerSha256 = (Get-FileHash -LiteralPath $playerFull -Algorithm SHA256).Hash
    gameAssembly = $gameAssemblyPath
    gameAssemblySha256 = (Get-FileHash -LiteralPath $gameAssemblyPath -Algorithm SHA256).Hash
    scriptingBackend = "IL2CPP"
    cloudMatrixPassed = $cloudPassed
    telemetryTransportPassed = $telemetryPassed
    continuousSoakPassed = $soakPassed
    profileAndPersistentDataRestored = $restoreErrors.Count -eq 0
    systemProxyRestored = $systemProxyRestored
    restoreErrors = $restoreErrors
    error = $gateError
    externalRcBlockers = @(
        "production_code_signing_certificate",
        "independent_clean_machine_install_validation"
    )
    cloud = if ($null -ne $cloudReport) { $cloudReport.cloudMatrix } else { $null }
    telemetry = if ($null -ne $telemetryReport) { $telemetryReport.telemetry } else { $null }
    telemetryEndpointValidation = $telemetrySummary
    soak = if ($null -ne $soakReport) { $soakReport.soak } else { $null }
    host = if ($null -ne $soakReport) { $soakReport.host } else { $null }
    hardPass = $cloudPassed -and
               $telemetryPassed -and
               $soakPassed -and
               $systemProxyRestored -and
               $restoreErrors.Count -eq 0 -and
               [string]::IsNullOrWhiteSpace($gateError)
}
$audit | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $auditPath -Encoding utf8

$markdown = [Text.StringBuilder]::new()
[void]$markdown.AppendLine("# P12.5.4 Release Gate")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("- Player: ``$playerFull``")
[void]$markdown.AppendLine("- Backend: IL2CPP")
[void]$markdown.AppendLine("- Cloud conflict/migration Player matrix: **$cloudPassed**")
[void]$markdown.AppendLine("- Consent/redaction/offline retry transport: **$telemetryPassed**")
[void]$markdown.AppendLine("- Same-process combat soak: **$soakPassed**")
[void]$markdown.AppendLine("- Profile and persistent data restored: **$($audit.profileAndPersistentDataRestored)**")
[void]$markdown.AppendLine("- System proxy restored: **$($audit.systemProxyRestored)**")
if ($null -ne $soakReport) {
    [void]$markdown.AppendLine()
    [void]$markdown.AppendLine("## Soak Evidence")
    [void]$markdown.AppendLine()
    [void]$markdown.AppendLine("- Real duration: $([Math]::Round([double]$soakReport.soak.actualRealSeconds, 2)) s")
    [void]$markdown.AppendLine("- Average FPS: $([Math]::Round([double]$soakReport.soak.averageFps, 2))")
    [void]$markdown.AppendLine("- P95 / P99: $([Math]::Round([double]$soakReport.soak.p95FrameMilliseconds, 2)) / $([Math]::Round([double]$soakReport.soak.p99FrameMilliseconds, 2)) ms")
    [void]$markdown.AppendLine("- Peak reserved: $([Math]::Round([double]$soakReport.soak.peakReservedMemoryBytes / 1MB, 2)) MB")
    [void]$markdown.AppendLine("- Memory slope: $([Math]::Round([double]$soakReport.soak.reservedMemorySlopeMegabytesPerMinute, 3)) MB/min")
    [void]$markdown.AppendLine("- Spawned / resolved: $($soakReport.soak.finalRuntime.spawnedEnemies) / $($soakReport.soak.finalRuntime.resolvedEnemies)")
}
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("External RC blockers remain production signing and independent clean-machine installation.")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("Hard pass: **$($audit.hardPass)**")
$markdown.ToString() | Set-Content -LiteralPath $markdownPath -Encoding utf8

$audit | ConvertTo-Json -Depth 30
if (-not $audit.hardPass) {
    throw "P12.5.4 release gate failed. Inspect $auditPath"
}
