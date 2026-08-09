param(
    [string]$PlayerPath = "E:/TD/output/builds/p1253_il2cpp/EmberlineDefense.exe",
    [string]$OutputRoot = "E:/TD/output/playtest/p1253_crash_recovery",
    [ValidateRange(3, 30)]
    [int]$CrashAfterSeconds = 6,
    [ValidateRange(60, 600)]
    [int]$RecoverySmokeTimeoutSeconds = 300
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
if (-not (Test-Path -LiteralPath $playerFull)) {
    throw "IL2CPP player was not found: $playerFull"
}
$gameAssemblyPath = Join-Path (Split-Path -Parent $playerFull) "GameAssembly.dll"
if (-not (Test-Path -LiteralPath $gameAssemblyPath)) {
    throw "IL2CPP GameAssembly.dll was not found beside the player: $gameAssemblyPath"
}

New-Item -ItemType Directory -Path $outputFull -Force | Out-Null
$persistentRoot = [IO.Path]::GetFullPath(
    (Join-Path $env:USERPROFILE "AppData/LocalLow/Emberline/Emberline Defense"))
$diagnosticsRoot = [IO.Path]::GetFullPath((Join-Path $persistentRoot "Diagnostics"))
$currentSessionPath = Join-Path $diagnosticsRoot "session-current.json"
$archiveRoot = Join-Path $diagnosticsRoot "Archive"
$recoveryRoot = [IO.Path]::GetFullPath((Join-Path $persistentRoot "CampaignRecovery"))
$safePersistentPrefix = $persistentRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
if (-not $diagnosticsRoot.StartsWith($safePersistentPrefix, [StringComparison]::OrdinalIgnoreCase) -or
    -not $recoveryRoot.StartsWith($safePersistentPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe persistent-data path."
}

$crashLogPath = Join-Path $outputFull "forced-crash-player.log"
$recoveryLogPath = Join-Path $outputFull "recovery-player.log"
$smokeReportPath = Join-Path $outputFull "recovery-smoke.json"
$auditPath = Join-Path $outputFull "p1253_crash_recovery_audit.json"
$prefsBackupPath = Join-Path $outputFull "playerprefs-before-probe.reg"
$recoveryBackupPath = Join-Path $outputFull "campaign-recovery-before-probe"
$playerPrefsKey = "HKCU\Software\Emberline\Emberline Defense"
foreach ($path in @($crashLogPath, $recoveryLogPath, $smokeReportPath, $auditPath)) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Force
    }
}

$hadPlayerPrefs = (& reg.exe query $playerPrefsKey 2>$null) -ne $null
if ($hadPlayerPrefs) {
    & reg.exe export $playerPrefsKey $prefsBackupPath /y | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Could not back up PlayerPrefs before the crash probe."
    }
}
$hadRecoveryDirectory = Test-Path -LiteralPath $recoveryRoot
if ($hadRecoveryDirectory) {
    Copy-Item -LiteralPath $recoveryRoot -Destination $recoveryBackupPath -Recurse -Force
}

$previousSessionId = ""
if (Test-Path -LiteralPath $currentSessionPath) {
    try {
        $previousSessionId = [string](Get-Content -LiteralPath $currentSessionPath -Raw | ConvertFrom-Json).sessionId
    } catch {
    }
}
$forcedSession = $null
$recoveredSession = $null
$archivedPath = ""
$probeError = ""
$restoreErrors = @()
try {
    $crashArguments = "-screen-fullscreen 0 -screen-width 1280 -screen-height 720 " +
                      "-logFile `"$crashLogPath`""
    $crashPlayer = Start-Process -FilePath $playerFull -ArgumentList $crashArguments -PassThru -WindowStyle Hidden
    $sessionDeadline = [DateTime]::UtcNow.AddSeconds(30)
    do {
        Start-Sleep -Milliseconds 250
        if (Test-Path -LiteralPath $currentSessionPath) {
            try {
                $candidate = Get-Content -LiteralPath $currentSessionPath -Raw | ConvertFrom-Json
                if (-not [string]::IsNullOrWhiteSpace([string]$candidate.sessionId) -and
                    $candidate.sessionId -ne $previousSessionId) {
                    $forcedSession = $candidate
                    break
                }
            } catch {
            }
        }
    } while ([DateTime]::UtcNow -lt $sessionDeadline)
    if ($null -eq $forcedSession) {
        throw "The forced-crash player did not create a session diagnostic."
    }

    Start-Sleep -Seconds $CrashAfterSeconds
    if (-not $crashPlayer.HasExited) {
        Stop-Process -Id $crashPlayer.Id -Force
        $crashPlayer.WaitForExit()
    }
    $forcedSession = Get-Content -LiteralPath $currentSessionPath -Raw | ConvertFrom-Json

    $recoveryArguments = "-screen-fullscreen 0 -screen-width 1280 -screen-height 720 " +
                         "-logFile `"$recoveryLogPath`" --td-smoke-test " +
                         "--td-smoke-report `"$smokeReportPath`" --td-smoke-time-scale 16 " +
                         "--td-smoke-timeout $RecoverySmokeTimeoutSeconds " +
                         "--td-smoke-technical-integrity 1000"
    $recoveryPlayer = Start-Process -FilePath $playerFull -ArgumentList $recoveryArguments -PassThru -WindowStyle Hidden
    if (-not $recoveryPlayer.WaitForExit(($RecoverySmokeTimeoutSeconds + 60) * 1000)) {
        Stop-Process -Id $recoveryPlayer.Id -Force
        throw "Recovery smoke timed out."
    }
    if (-not (Test-Path -LiteralPath $smokeReportPath)) {
        throw "Recovery smoke did not write its report."
    }

    $recoverySmoke = Get-Content -LiteralPath $smokeReportPath -Raw | ConvertFrom-Json
    $recoveredSession = Get-Content -LiteralPath $currentSessionPath -Raw | ConvertFrom-Json
    if (Test-Path -LiteralPath $archiveRoot) {
        $archived = Get-ChildItem -LiteralPath $archiveRoot -Filter "unclean-*.json" -File |
            Sort-Object LastWriteTimeUtc -Descending |
            Where-Object {
                try {
                    (Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json).sessionId -eq
                        $forcedSession.sessionId
                } catch {
                    $false
                }
            } |
            Select-Object -First 1
        if ($null -ne $archived) {
            $archivedPath = $archived.FullName
        }
    }
}
catch {
    $probeError = $_.Exception.Message
}
finally {
    try {
        & reg.exe delete $playerPrefsKey /f 2>$null | Out-Null
        if ($hadPlayerPrefs) {
            & reg.exe import $prefsBackupPath | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "PlayerPrefs registry restore returned exit code $LASTEXITCODE."
            }
        }
    } catch {
        $restoreErrors += $_.Exception.Message
    }

    try {
        if (Test-Path -LiteralPath $recoveryRoot) {
            Remove-Item -LiteralPath $recoveryRoot -Recurse -Force
        }
        if ($hadRecoveryDirectory) {
            Copy-Item -LiteralPath $recoveryBackupPath -Destination $recoveryRoot -Recurse -Force
        }
    } catch {
        $restoreErrors += $_.Exception.Message
    }
}

$smoke = if (Test-Path -LiteralPath $smokeReportPath) {
    Get-Content -LiteralPath $smokeReportPath -Raw | ConvertFrom-Json
} else {
    $null
}
$audit = [ordered]@{
    schemaVersion = "p1253-crash-recovery-probe-v1"
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    elapsedSeconds = [Math]::Round(([DateTime]::UtcNow - $startedUtc).TotalSeconds, 2)
    player = $playerFull
    gameAssembly = $gameAssemblyPath
    gameAssemblySha256 = (Get-FileHash -LiteralPath $gameAssemblyPath -Algorithm SHA256).Hash
    forcedSessionId = if ($null -ne $forcedSession) { [string]$forcedSession.sessionId } else { "" }
    forcedSessionClean = $null -ne $forcedSession -and [bool]$forcedSession.cleanShutdown
    recoveredSessionId = if ($null -ne $recoveredSession) { [string]$recoveredSession.sessionId } else { "" }
    previousSessionRecovered = $null -ne $recoveredSession -and [bool]$recoveredSession.previousSessionRecovered
    recoveredSessionClean = $null -ne $recoveredSession -and [bool]$recoveredSession.cleanShutdown
    archivedDiagnostic = $archivedPath
    archivedDiagnosticExists = -not [string]::IsNullOrWhiteSpace($archivedPath) -and
                               (Test-Path -LiteralPath $archivedPath)
    recoverySmokePassed = $null -ne $smoke -and [bool]$smoke.passed
    profileRestored = $null -ne $smoke -and [bool]$smoke.profileRestored
    externalProfileRestored = $restoreErrors.Count -eq 0
    restoreErrors = $restoreErrors
    error = $probeError
    hardPass = $null -ne $forcedSession -and
               -not [bool]$forcedSession.cleanShutdown -and
               $null -ne $recoveredSession -and
               [bool]$recoveredSession.previousSessionRecovered -and
               [bool]$recoveredSession.cleanShutdown -and
               -not [string]::IsNullOrWhiteSpace($archivedPath) -and
               (Test-Path -LiteralPath $archivedPath) -and
               $null -ne $smoke -and
               [bool]$smoke.passed -and
               [bool]$smoke.profileRestored -and
               $restoreErrors.Count -eq 0 -and
               [string]::IsNullOrWhiteSpace($probeError)
    artifacts = [ordered]@{
        forcedCrashLog = $crashLogPath
        recoveryLog = $recoveryLogPath
        recoverySmoke = $smokeReportPath
        archivedDiagnostic = $archivedPath
        audit = $auditPath
    }
}
$audit | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $auditPath -Encoding utf8
$audit | ConvertTo-Json -Depth 20
if (-not $audit.hardPass) {
    throw "P12.5.3 crash recovery probe failed. Inspect $auditPath"
}
