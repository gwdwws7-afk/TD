param(
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [string]$OutputRoot = "E:/TD/output/playtest/player_20_clear_experience",
    [ValidateRange(1, 16)]
    [float]$TimeScale = 16,
    [ValidateRange(30, 300)]
    [int]$MaxRealSeconds = 120,
    [switch]$ResumeExisting,
    [switch]$RefreshScripts
)

$ErrorActionPreference = "Stop"
$startedUtc = [DateTime]::UtcNow
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$workspacePrefix = $projectRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
$outputFull = [IO.Path]::GetFullPath($OutputRoot)
if (-not $outputFull.StartsWith($workspacePrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputRoot must stay inside the workspace: $outputFull"
}

$runner = Join-Path $PSScriptRoot "td_mcp_playtest.ps1"
$runsRoot = Join-Path $outputFull "runs"
$attemptsRoot = Join-Path $runsRoot "attempts"
New-Item -ItemType Directory -Path $outputFull, $runsRoot, $attemptsRoot -Force | Out-Null

function Get-StrategyForLevel {
    param([int]$Level)
    switch ($Level % 3) {
        1 { return "adaptive_network" }
        2 { return "focused_fire" }
        default { return "control_lattice" }
    }
}

function Get-DoctrineForStrategy {
    param([string]$Strategy)
    switch ($Strategy) {
        "focused_fire" { return "EmberSurge" }
        "control_lattice" { return "FractureMark" }
        default { return "Adaptive" }
    }
}

function Get-AttemptConfigurations {
    param(
        [string]$InitialStrategy,
        [int]$InitialSite
    )

    $result = New-Object System.Collections.Generic.List[object]
    $seen = @{}
    $candidates = @(
        [pscustomobject]@{ strategy = $InitialStrategy; site = $InitialSite },
        [pscustomobject]@{ strategy = "adaptive_network"; site = 1 },
        [pscustomobject]@{ strategy = "focused_fire"; site = 0 },
        [pscustomobject]@{ strategy = "control_lattice"; site = 0 },
        [pscustomobject]@{ strategy = "adaptive_network"; site = 2 },
        [pscustomobject]@{ strategy = "focused_fire"; site = 2 },
        [pscustomobject]@{ strategy = "control_lattice"; site = 1 },
        [pscustomobject]@{ strategy = "adaptive_network"; site = 0 },
        [pscustomobject]@{ strategy = "focused_fire"; site = 1 },
        [pscustomobject]@{ strategy = "control_lattice"; site = 2 }
    )
    foreach ($candidate in $candidates) {
        $key = "$($candidate.strategy):$($candidate.site)"
        if ($seen.ContainsKey($key)) {
            continue
        }
        $seen[$key] = $true
        $result.Add($candidate)
    }
    return @($result | ForEach-Object { $_ })
}

function Test-ClearedRun {
    param(
        [string]$Path,
        [DateTime]$MinimumGeneratedUtc = [DateTime]::MinValue
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return $false
    }
    try {
        $run = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
        $generatedUtc = [DateTime]::MinValue
        if (-not [DateTime]::TryParse(
            [string]$run.generatedUtc,
            [Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::RoundtripKind,
            [ref]$generatedUtc)) {
            return $false
        }
        return [bool]$run.completed -and
               [bool]$run.victory -and
               -not [bool]$run.stalled -and
               [int]$run.waveCount -gt 0 -and
               [int]$run.wavesCleared -eq [int]$run.waveCount -and
               [int]$run.integrityRemaining -gt 0 -and
               $generatedUtc.ToUniversalTime() -ge $MinimumGeneratedUtc.ToUniversalTime()
    }
    catch {
        return $false
    }
}

function Copy-AttemptArtifacts {
    param(
        [int]$Level,
        [string]$Label,
        [string]$Strategy,
        [int]$Site,
        [string]$RunPath,
        [string]$SummaryPath,
        [string]$ScreenshotPath
    )

    $attemptStem = "l{0:d2}_{1}_{2}_site{3}" -f $Level, $Label, $Strategy, $Site
    foreach ($artifact in @(
        @{ source = $RunPath; suffix = "_run.json" },
        @{ source = $SummaryPath; suffix = "_summary.json" },
        @{ source = $ScreenshotPath; suffix = ".png" }
    )) {
        if (Test-Path -LiteralPath $artifact.source) {
            Copy-Item -LiteralPath $artifact.source `
                -Destination (Join-Path $attemptsRoot "$attemptStem$($artifact.suffix)") `
                -Force
        }
    }
}

function Get-Median {
    param([double[]]$Values)
    $sorted = @($Values | Sort-Object)
    if ($sorted.Count -eq 0) {
        return 0
    }
    $middle = [Math]::Floor($sorted.Count / 2)
    if ($sorted.Count % 2 -eq 1) {
        return [Math]::Round($sorted[$middle], 2)
    }
    return [Math]::Round(($sorted[$middle - 1] + $sorted[$middle]) / 2, 2)
}

$rows = New-Object System.Collections.Generic.List[object]
$refreshPending = $RefreshScripts.IsPresent
for ($level = 1; $level -le 20; $level++) {
    $strategy = Get-StrategyForLevel -Level $level
    $doctrine = Get-DoctrineForStrategy -Strategy $strategy
    $siteVariant = ($level - 1) % 3
    $stem = "l{0:d2}_{1}_site{2}" -f $level, $strategy, $siteVariant
    $summaryPath = Join-Path $runsRoot "$stem`_summary.json"
    $runPath = Join-Path $runsRoot "$stem`_run.json"
    $screenshotPath = Join-Path $runsRoot "$stem.png"
    $reuse = $false
    if ($ResumeExisting -and
        (Test-Path -LiteralPath $summaryPath) -and
        (Test-Path -LiteralPath $runPath) -and
        (Test-Path -LiteralPath $screenshotPath)) {
        $reuse = Test-ClearedRun -Path $runPath
    }

    $attempts = 0
    if (-not $reuse) {
        if ((Test-Path -LiteralPath $runPath) -and -not (Test-ClearedRun -Path $runPath)) {
            Copy-AttemptArtifacts `
                -Level $level `
                -Label "preexisting" `
                -Strategy $strategy `
                -Site $siteVariant `
                -RunPath $runPath `
                -SummaryPath $summaryPath `
                -ScreenshotPath $screenshotPath
        }

        $configurations = Get-AttemptConfigurations -InitialStrategy $strategy -InitialSite $siteVariant
        $completed = $false
        for ($attempt = 1; $attempt -le $configurations.Count; $attempt++) {
            $attempts = $attempt
            $configuration = $configurations[$attempt - 1]
            $attemptStrategy = [string]$configuration.strategy
            $attemptSite = [int]$configuration.site
            $attemptDoctrine = Get-DoctrineForStrategy -Strategy $attemptStrategy
            $arguments = @{
                McpUrl = $McpUrl
                LevelIndex = $level
                DurationSeconds = $MaxRealSeconds + 5
                TimeScale = $TimeScale
                ViewportWidth = 1280
                ViewportHeight = 720
                FormationDoctrine = $attemptDoctrine
                FormationDifficulty = "Standard"
                P124AutoplayStrategy = $attemptStrategy
                P124SiteVariant = $attemptSite
                P124MaxRealSeconds = $MaxRealSeconds
                P124RunReportPath = $runPath.Replace("\", "/")
                RunP124Audit = $true
                RunP125EconomyAudit = $true
                PreserveCampaignProgress = $true
                ScreenshotPath = $screenshotPath.Replace("\", "/")
                SummaryPath = $summaryPath
            }
            if ($refreshPending) {
                $arguments.RefreshScripts = $true
                $refreshPending = $false
            }

            foreach ($artifactPath in @($runPath, $summaryPath, $screenshotPath)) {
                Remove-Item -LiteralPath $artifactPath -Force -ErrorAction SilentlyContinue
            }
            $attemptStartedUtc = [DateTime]::UtcNow.AddSeconds(-1)
            Write-Output (
                "[PLAYER 20] L{0:d2} attempt={1}/{2} strategy={3} site={4}" -f
                $level, $attempt, $configurations.Count, $attemptStrategy, $attemptSite)
            $runnerError = $null
            try {
                & $runner @arguments | Out-Null
            }
            catch {
                $runnerError = $_
            }

            if (Test-ClearedRun -Path $runPath -MinimumGeneratedUtc $attemptStartedUtc) {
                $completed = $true
                break
            }

            Copy-AttemptArtifacts `
                -Level $level `
                -Label "attempt$attempt" `
                -Strategy $attemptStrategy `
                -Site $attemptSite `
                -RunPath $runPath `
                -SummaryPath $summaryPath `
                -ScreenshotPath $screenshotPath
            if ($attempt -ge $configurations.Count) {
                $errorDetail = if ($null -ne $runnerError) {
                    $runnerError.Exception.Message
                }
                else {
                    "run reached a terminal defeat"
                }
                throw "L$level was not cleared after $($configurations.Count) player strategies: $errorDetail"
            }
            Start-Sleep -Seconds 2
        }
        if (-not $completed) {
            throw "L$level did not clear."
        }
    }

    $run = Get-Content -LiteralPath $runPath -Raw | ConvertFrom-Json
    $summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
    $rows.Add([pscustomobject]@{
        clearIndex = $level
        level = [int]$run.levelIndex
        levelId = [string]$run.levelId
        map = [string]$run.mapId
        difficulty = [string]$run.difficultyId
        strategy = [string]$run.strategyId
        siteVariant = [int]$run.siteVariant
        completed = [bool]$run.completed
        victory = [bool]$run.victory
        stalled = [bool]$run.stalled
        realDurationSeconds = [Math]::Round([double]$run.realDurationSeconds, 2)
        simulationDurationSeconds = [Math]::Round([double]$run.simulationDurationSeconds, 2)
        wavesCleared = [int]$run.wavesCleared
        waveCount = [int]$run.waveCount
        score = [int]$run.totalScore
        grade = [string]$run.grade
        firstLeakWave = [int]$run.firstLeakWave
        escapes = [int]$run.escapes
        integrity = [int]$run.integrityRemaining
        endingBudget = [int]$run.endingBudget
        finalFiveSpend = [int]$run.finalFiveSpend
        finalFivePurchases = [int]$run.finalFivePurchases
        economyDecisionValue = [bool]$run.economyDecisionValue
        towersBuilt = [int]$run.towersBuilt
        towerKindsUsed = [int]$run.towerKindsUsed
        upgradesPurchased = [int]$run.upgradesPurchased
        resonanceWindows = [int]$run.resonanceWindows
        resonanceCommands = [int]$run.resonanceCommands
        convergenceTriggers = [int]$run.convergenceTriggers
        scenarioOpportunities = [int]$run.scenarioOpportunities
        scenarioUses = [int]$run.scenarioUses
        scenarioConversionPct = [Math]::Round([double]$run.scenarioConversionPct, 1)
        topTowerKind = [string]$run.topTowerKind
        topTowerKindDamageSharePct = [Math]::Round([double]$run.topTowerKindDamageSharePct, 1)
        topSite = [string]$run.topSite
        topSiteDamageSharePct = [Math]::Round([double]$run.topSiteDamageSharePct, 1)
        analyticsConsistent = [bool]$run.analyticsConsistent
        failureReasons = @($run.failureReasons) -join " | "
        recommendations = @($run.recommendations) -join " | "
        uiBounds = [bool]$summary.checks.uiBounds
        uiOverlap = [bool]$summary.checks.uiOverlap
        uiTextFit = [bool]$summary.checks.uiTextFit
        consoleClean = [bool]$summary.checks.consoleClean
        p130Audit = [bool]$summary.checks.p130Audit
        attempts = $attempts
        screenshot = [IO.Path]::GetFullPath($screenshotPath)
        runReport = [IO.Path]::GetFullPath($runPath)
        summary = [IO.Path]::GetFullPath($summaryPath)
    })
}

$victories = @($rows | Where-Object victory)
$examRows = @($rows | Where-Object level -in @(5, 9, 13, 17, 20))
$mapGroups = foreach ($map in @($rows.map | Sort-Object -Unique)) {
    $matches = @($rows | Where-Object map -eq $map)
    [pscustomobject]@{
        map = $map
        runs = $matches.Count
        medianScore = Get-Median @($matches | ForEach-Object { [double]$_.score })
        medianEscapes = Get-Median @($matches | ForEach-Object { [double]$_.escapes })
        medianDuration = Get-Median @($matches | ForEach-Object { [double]$_.simulationDurationSeconds })
        distinctTopSites = @($matches.topSite | Sort-Object -Unique).Count
        distinctTopTowerKinds = @($matches.topTowerKind | Sort-Object -Unique).Count
        maximumTopSiteSharePct = [Math]::Round(
            [double](($matches.topSiteDamageSharePct | Measure-Object -Maximum).Maximum),
            1)
        maximumTopKindSharePct = [Math]::Round(
            [double](($matches.topTowerKindDamageSharePct | Measure-Object -Maximum).Maximum),
            1)
    }
}

$allCleared = $rows.Count -eq 20 -and
              $victories.Count -eq 20 -and
              @($rows | Where-Object {
                  -not $_.completed -or $_.stalled -or
                  $_.wavesCleared -ne $_.waveCount -or $_.integrity -le 0
              }).Count -eq 0
$surfacePass = @($rows | Where-Object {
    -not $_.uiBounds -or -not $_.uiOverlap -or -not $_.uiTextFit -or
    -not $_.consoleClean -or -not $_.p130Audit
}).Count -eq 0
$dataPass = @($rows | Where-Object {
    -not $_.analyticsConsistent -or -not $_.economyDecisionValue
}).Count -eq 0

$rows | Export-Csv -LiteralPath (Join-Path $outputFull "player_20_clears.csv") -NoTypeInformation -Encoding UTF8
$mapGroups | Export-Csv -LiteralPath (Join-Path $outputFull "player_20_map_summary.csv") -NoTypeInformation -Encoding UTF8
$report = [ordered]@{
    schemaVersion = "player-20-clear-experience-v1"
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    elapsedSeconds = [Math]::Round(([DateTime]::UtcNow - $startedUtc).TotalSeconds, 2)
    requestedClears = 20
    completedRuns = $rows.Count
    victories = $victories.Count
    allCleared = $allCleared
    surfacePass = $surfacePass
    dataPass = $dataPass
    medianScore = Get-Median @($rows | ForEach-Object { [double]$_.score })
    medianEscapes = Get-Median @($rows | ForEach-Object { [double]$_.escapes })
    medianSimulationDuration = Get-Median @($rows | ForEach-Object { [double]$_.simulationDurationSeconds })
    examLevels = $examRows
    maps = $mapGroups
    runs = $rows
    hardPass = $allCleared -and $surfacePass -and $dataPass
}
$jsonPath = Join-Path $outputFull "player_20_experience.json"
$report | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
$report | ConvertTo-Json -Depth 30
if (-not $report.hardPass) {
    throw "20-clear player experience gate failed. Inspect $jsonPath"
}
