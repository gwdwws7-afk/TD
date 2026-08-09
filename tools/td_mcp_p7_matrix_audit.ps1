param(
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [int]$LevelIndex = 20,
    [int]$DurationSeconds = 4,
    [float]$TimeScale = 1.5,
    [int]$BonusBudget = 500,
    [string]$OutputDirectory = "E:/TD/output/playtest/p71_matrix_audit",
    [switch]$SkipRefreshScripts
)

$ErrorActionPreference = "Stop"
$runner = Join-Path $PSScriptRoot "td_mcp_playtest.ps1"
if (-not (Test-Path -LiteralPath $runner)) {
    throw "Playtest runner not found: $runner"
}

$cases = @(
    [pscustomobject]@{ Id = "rail_armor_lance"; Tower = "RailLancer"; Branch = "Damage"; Command = "EmberSurge"; Enemy = "carapace_brute"; Count = 4 },
    [pscustomobject]@{ Id = "rail_pinning_rail"; Tower = "RailLancer"; Branch = "Utility"; Command = "FractureMark"; Enemy = "skitter_runner"; Count = 6 },
    [pscustomobject]@{ Id = "mortar_cinder_saturation"; Tower = "CinderMortar"; Branch = "Damage"; Command = "FractureMark"; Enemy = "ash_swarm"; Count = 8 },
    [pscustomobject]@{ Id = "mortar_ash_denial"; Tower = "CinderMortar"; Branch = "Utility"; Command = "FractureMark"; Enemy = "ash_swarm"; Count = 8 },
    [pscustomobject]@{ Id = "frost_cryo_shatter"; Tower = "FrostCoil"; Branch = "Damage"; Command = "FractureMark"; Enemy = "skitter_runner"; Count = 6 },
    [pscustomobject]@{ Id = "frost_absolute_zero"; Tower = "FrostCoil"; Branch = "Utility"; Command = "FractureMark"; Enemy = "skitter_runner"; Count = 6 },
    [pscustomobject]@{ Id = "arc_chain_overload"; Tower = "ArcWelder"; Branch = "Damage"; Command = "FractureMark"; Enemy = "ash_swarm"; Count = 8 },
    [pscustomobject]@{ Id = "arc_conductive_net"; Tower = "ArcWelder"; Branch = "Utility"; Command = "FractureMark"; Enemy = "ash_swarm"; Count = 8 },
    [pscustomobject]@{ Id = "siege_core_bore"; Tower = "SiegeDrill"; Branch = "Damage"; Command = "EmberSurge"; Enemy = "carapace_brute"; Count = 4 },
    [pscustomobject]@{ Id = "siege_breach_lock"; Tower = "SiegeDrill"; Branch = "Utility"; Command = "EmberSurge"; Enemy = "rail_warden"; Count = 5 },
    [pscustomobject]@{ Id = "flak_redline_burst"; Tower = "EmberFlak"; Branch = "Damage"; Command = "FractureMark"; Enemy = "skitter_runner"; Count = 8 },
    [pscustomobject]@{ Id = "flak_intercept_screen"; Tower = "EmberFlak"; Branch = "Utility"; Command = "FractureMark"; Enemy = "skitter_runner"; Count = 8 },
    [pscustomobject]@{ Id = "beacon_signal_burn"; Tower = "ResonanceBeacon"; Branch = "Damage"; Command = "EmberSurge"; Enemy = "ember_leech"; Count = 6 },
    [pscustomobject]@{ Id = "beacon_resonance_relay"; Tower = "ResonanceBeacon"; Branch = "Utility"; Command = "EmberSurge"; Enemy = "ember_leech"; Count = 6 },
    [pscustomobject]@{ Id = "grav_event_horizon"; Tower = "GravSnare"; Branch = "Damage"; Command = "EmberSurge"; Enemy = "carapace_brute"; Count = 5 },
    [pscustomobject]@{ Id = "grav_singularity_well"; Tower = "GravSnare"; Branch = "Utility"; Command = "FractureMark"; Enemy = "skitter_runner"; Count = 8 }
)

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$results = @()
$index = 0
foreach ($case in $cases) {
    $index++
    $slug = $case.Id
    $summaryPath = Join-Path $OutputDirectory "$slug.json"
    $screenshotPath = (Join-Path $OutputDirectory "$slug.png").Replace('\', '/')
    $upgradePlan = "5,3:$($case.Branch),$($case.Branch)"
    $enemyPlan = "$($case.Enemy):$($case.Count):default:0.28:12"
    Write-Host "[P7.1 $index/$($cases.Count)] $($case.Id) | $($case.Tower) $($case.Branch) | $($case.Command) x $($case.Enemy)"

    $arguments = @{
        McpUrl = $McpUrl
        LevelIndex = $LevelIndex
        DurationSeconds = $DurationSeconds
        TimeScale = $TimeScale
        BuildPlan = "5,3:$($case.Tower)"
        UpgradePlan = $upgradePlan
        BonusBudget = $BonusBudget
        ResonanceCommand = $case.Command
        EnemyPlan = $enemyPlan
        ExpectUltimateId = $case.Id
        MinUltimateProcs = 1
        MinMatrixFullMatches = 1
        ScreenshotPath = $screenshotPath
        SummaryPath = $summaryPath
        SkipStartWave = $true
        FreezeConfiguredWaves = $true
    }
    if ($index -eq 1 -and -not $SkipRefreshScripts) {
        $arguments.RefreshScripts = $true
    }

    & $runner @arguments | Out-Null
    $summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
    $failedChecks = @($summary.checks.PSObject.Properties | Where-Object { -not [bool]$_.Value } | ForEach-Object { $_.Name })
    $passed = $failedChecks.Count -eq 0 -and
        [int]$summary.ultimateProcCount -ge 1 -and
        [int]$summary.ultimateFullMatchCount -ge 1

    $results += [pscustomobject]@{
        id = $case.Id
        tower = $case.Tower
        branch = $case.Branch
        command = $case.Command
        enemy = $case.Enemy
        procs = [int]$summary.ultimateProcCount
        fullMatches = [int]$summary.ultimateFullMatchCount
        tacticalScore = [int]$summary.tacticalScore
        consoleClean = [bool]$summary.checks.consoleClean
        passed = $passed
        failedChecks = $failedChecks -join ","
        summaryPath = $summaryPath
        screenshotPath = $screenshotPath
    }

    Write-Host "  -> procs=$($summary.ultimateProcCount) full=$($summary.ultimateFullMatchCount) passed=$passed"
}

$failed = @($results | Where-Object { -not $_.passed })
$aggregate = [ordered]@{
    schemaVersion = "p7.1-matrix-audit-v1"
    generatedAtUtc = [DateTime]::UtcNow.ToString("o")
    levelIndex = $LevelIndex
    caseCount = $results.Count
    passedCount = $results.Count - $failed.Count
    failedCount = $failed.Count
    allPassed = $failed.Count -eq 0
    totalProcs = [int](($results | Measure-Object -Property procs -Sum).Sum)
    totalFullMatches = [int](($results | Measure-Object -Property fullMatches -Sum).Sum)
    cases = $results
}

$aggregatePath = Join-Path $OutputDirectory "p71_matrix_audit_summary.json"
$csvPath = Join-Path $OutputDirectory "p71_matrix_audit_summary.csv"
$aggregate | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $aggregatePath -Encoding UTF8
$results | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8
$aggregate | ConvertTo-Json -Depth 20

if ($failed.Count -gt 0) {
    throw "P7.1 matrix audit failed for $($failed.Count) case(s): $($failed.id -join ', '). See $aggregatePath"
}
