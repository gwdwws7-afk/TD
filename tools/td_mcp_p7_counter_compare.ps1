param(
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [int]$LevelIndex = 20,
    [int]$RandomSeed = 1337,
    [float]$MinMatchedLiftPct = 10.0,
    [float]$MaxMatchedLiftPct = 50.0,
    [string]$OutputDirectory = "E:/TD/output/playtest/p7_counter_compare",
    [switch]$SkipRefreshScripts
)

$ErrorActionPreference = "Stop"
$runner = Join-Path $PSScriptRoot "td_mcp_playtest.ps1"
if (-not (Test-Path -LiteralPath $runner)) {
    throw "Playtest runner not found: $runner"
}

$pairs = @(
    [pscustomobject]@{
        Name = "ember_armor"
        BuildPlan = "5,3:RailLancer;6,1:SiegeDrill"
        UpgradePlan = "5,3:Damage,Damage;6,1:Damage,Damage"
        EnemyPlan = "carapace_brute:8:default:0.26:16"
        MatchedCommand = "EmberSurge"
        WrongCommand = "FractureMark"
        DurationSeconds = 4
        TimeScale = 1.5
        MinFullMatches = 6
    },
    [pscustomobject]@{
        Name = "fracture_runners"
        BuildPlan = "5,3:FrostCoil;6,1:EmberFlak"
        UpgradePlan = "5,3:Utility,Utility;6,1:Utility,Utility"
        EnemyPlan = "skitter_runner:12:default:0.26:24"
        MatchedCommand = "FractureMark"
        WrongCommand = "EmberSurge"
        DurationSeconds = 3
        TimeScale = 1.2
        MinFullMatches = 2
    }
)

function Get-StateInt {
    param(
        [string]$State,
        [string]$Field
    )

    $match = [Regex]::Match($State, "(?m)^$([Regex]::Escape($Field))=(\d+)")
    return $(if ($match.Success) { [int]$match.Groups[1].Value } else { -1 })
}

function Invoke-Variant {
    param(
        $Pair,
        [string]$Variant,
        [string]$Command,
        [bool]$Refresh
    )

    $slug = "$($Pair.Name)_$Variant"
    $summaryPath = Join-Path $OutputDirectory "$slug.json"
    $screenshotPath = (Join-Path $OutputDirectory "$slug.png").Replace('\', '/')
    $arguments = @{
        McpUrl = $McpUrl
        LevelIndex = $LevelIndex
        DurationSeconds = $Pair.DurationSeconds
        TimeScale = $Pair.TimeScale
        RandomSeed = $RandomSeed
        BuildPlan = $Pair.BuildPlan
        UpgradePlan = $Pair.UpgradePlan
        BonusBudget = 700
        ResonanceCommand = $Command
        EnemyPlan = $Pair.EnemyPlan
        ScreenshotPath = $screenshotPath
        SummaryPath = $summaryPath
        SkipStartWave = $true
        FreezeConfiguredWaves = $true
    }
    if ($Variant -eq "matched") {
        $arguments.MinMatrixFullMatches = $Pair.MinFullMatches
        $arguments.MinConvergenceTriggers = 1
    }
    if ($Refresh) {
        $arguments.RefreshScripts = $true
    }

    Write-Host "[P7 Compare] $($Pair.Name) $Variant -> $Command"
    & $runner @arguments | Out-Null
    $summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
    $state = [string]$summary.state.data.result
    $failedChecks = @($summary.checks.PSObject.Properties | Where-Object { -not [bool]$_.Value } | ForEach-Object { $_.Name })
    return [pscustomobject]@{
        variant = $Variant
        command = $Command
        damage = Get-StateInt -State $state -Field "_totalDamageDealt"
        fullMatches = [int]$summary.matrixFullMatchCount
        convergence = [int]$summary.convergenceTriggerCount
        tacticalScore = [int]$summary.tacticalScore
        allRunnerChecksPassed = $failedChecks.Count -eq 0
        failedChecks = $failedChecks -join ","
        summaryPath = $summaryPath
        screenshotPath = $screenshotPath
    }
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$results = @()
$refreshNext = -not $SkipRefreshScripts
foreach ($pair in $pairs) {
    $matched = Invoke-Variant -Pair $pair -Variant "matched" -Command $pair.MatchedCommand -Refresh $refreshNext
    $refreshNext = $false
    $wrong = Invoke-Variant -Pair $pair -Variant "wrong" -Command $pair.WrongCommand -Refresh $false
    $delta = $matched.damage - $wrong.damage
    $liftPct = [Math]::Round(($delta / [Math]::Max(1.0, $wrong.damage)) * 100.0, 1)
    $passed = $matched.allRunnerChecksPassed -and
        $wrong.allRunnerChecksPassed -and
        $matched.fullMatches -ge $pair.MinFullMatches -and
        $matched.convergence -ge 1 -and
        $wrong.fullMatches -eq 0 -and
        $wrong.convergence -eq 0 -and
        $liftPct -ge $MinMatchedLiftPct -and
        $liftPct -le $MaxMatchedLiftPct

    $results += [pscustomobject]@{
        pair = $pair.Name
        matchedCommand = $pair.MatchedCommand
        wrongCommand = $pair.WrongCommand
        matchedDamage = $matched.damage
        wrongDamage = $wrong.damage
        damageDelta = $delta
        matchedLiftPct = $liftPct
        matchedFullMatches = $matched.fullMatches
        wrongFullMatches = $wrong.fullMatches
        matchedConvergence = $matched.convergence
        wrongConvergence = $wrong.convergence
        scoreDelta = $matched.tacticalScore - $wrong.tacticalScore
        passed = $passed
        matchedSummary = $matched.summaryPath
        wrongSummary = $wrong.summaryPath
    }

    Write-Host "  -> damage $($matched.damage) vs $($wrong.damage), lift=$liftPct%, full=$($matched.fullMatches)/$($wrong.fullMatches), conv=$($matched.convergence)/$($wrong.convergence), passed=$passed"
}

$failed = @($results | Where-Object { -not $_.passed })
$aggregate = [ordered]@{
    schemaVersion = "p7-counter-compare-v1"
    generatedAtUtc = [DateTime]::UtcNow.ToString("o")
    levelIndex = $LevelIndex
    randomSeed = $RandomSeed
    minMatchedLiftPct = $MinMatchedLiftPct
    maxMatchedLiftPct = $MaxMatchedLiftPct
    pairCount = $results.Count
    passedCount = $results.Count - $failed.Count
    failedCount = $failed.Count
    allPassed = $failed.Count -eq 0
    pairs = $results
}

$aggregatePath = Join-Path $OutputDirectory "p7_counter_compare_summary.json"
$csvPath = Join-Path $OutputDirectory "p7_counter_compare_summary.csv"
$aggregate | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $aggregatePath -Encoding UTF8
$results | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8
$aggregate | ConvertTo-Json -Depth 20

if ($failed.Count -gt 0) {
    throw "P7 counter comparison failed: $($failed.pair -join ', '). See $aggregatePath"
}
