param(
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [string]$OutputDirectory = "E:/TD/output/playtest/p133_combat_readability",
    [switch]$RefreshScripts,
    [switch]$ResumeExisting
)

$ErrorActionPreference = "Stop"
$runner = Join-Path $PSScriptRoot "td_mcp_playtest.ps1"
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$probes = @(
    @{ name = "l01_grayline_1280"; level = 1; width = 1280; height = 720 },
    @{ name = "l05_ashfall_1280"; level = 5; width = 1280; height = 720 },
    @{ name = "l09_split_1280"; level = 9; width = 1280; height = 720 },
    @{ name = "l13_hollow_1280"; level = 13; width = 1280; height = 720 },
    @{ name = "l17_terminus_1280"; level = 17; width = 1280; height = 720 },
    @{ name = "l20_boss_1280"; level = 20; width = 1280; height = 720 },
    @{ name = "l09_split_960"; level = 9; width = 960; height = 540 },
    @{ name = "l20_boss_960"; level = 20; width = 960; height = 540 }
)

$results = New-Object System.Collections.Generic.List[object]
$refreshPending = $RefreshScripts.IsPresent
foreach ($probe in $probes) {
    $screenshotPath = (Join-Path $OutputDirectory "$($probe.name).png").Replace("\", "/")
    $summaryPath = Join-Path $OutputDirectory "$($probe.name).json"
    $summary = $null
    if ($ResumeExisting -and (Test-Path -LiteralPath $summaryPath)) {
        try {
            $existing = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
            if ([bool]$existing.checks.p133Audit -and
                [bool]$existing.checks.uiBounds -and
                [bool]$existing.checks.uiOverlap -and
                [bool]$existing.checks.uiTextFit -and
                [bool]$existing.checks.consoleClean) {
                $summary = $existing
            }
        } catch {
            $summary = $null
        }
    }

    $arguments = @{
        McpUrl = $McpUrl
        LevelIndex = $probe.level
        DurationSeconds = 1
        TimeScale = 1
        ViewportWidth = $probe.width
        ViewportHeight = $probe.height
        SkipAutoBuild = $true
        SkipStartWave = $true
        PrepareP133Combat = $true
        RunP133Audit = $true
        PreserveCampaignProgress = $true
        ScreenshotPath = $screenshotPath
        SummaryPath = $summaryPath
    }
    if ($refreshPending -and $null -eq $summary) {
        $arguments.RefreshScripts = $true
        $refreshPending = $false
    }

    for ($attempt = 1; $null -eq $summary -and $attempt -le 2; $attempt++) {
        try {
            & $runner @arguments | Out-Null
        } catch {
            # The runner writes its summary before a regression exception.
        }

        if (Test-Path -LiteralPath $summaryPath) {
            try {
                $summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
            } catch {
                $summary = $null
            }
        }
        if ($null -ne $summary -and [bool]$summary.checks.p133Audit) {
            break
        }
        $summary = $null

        $arguments.Remove("RefreshScripts")
        Start-Sleep -Seconds 2
    }

    if ($null -eq $summary) {
        throw "P13.3 probe did not produce a readable summary: $($probe.name)"
    }

    $stateText = [string]$summary.state.data.result
    $mapMatch = [regex]::Match($stateText, "(?m)^p13\.3\.audit\.map=(.+)$")
    $routeMatch = [regex]::Match($stateText, "(?m)^p13\.3\.audit\.route=True \[.+maxStep=([0-9.]+)")
    $buildMatch = [regex]::Match($stateText, "(?m)^p13\.3\.audit\.buildSites=True \[authored=([0-9]+),recommended=([0-9]+)")
    $motionMatch = [regex]::Match($stateText, "(?m)^p13\.3\.audit\.enemyMotion=True \[count=([0-9]+),turn=([0-9.]+),shadowGap=([0-9.]+)-([0-9.]+)")
    $interactionMatch = [regex]::Match($stateText, "(?m)^p13\.3\.audit\.interaction=True \[selected=([0-9]+),hovered=([0-9]+),maxDiameter=([0-9.]+)")

    $results.Add([pscustomobject]@{
        name = $probe.name
        level = $probe.level
        map = if ($mapMatch.Success) { $mapMatch.Groups[1].Value.Trim() } else { "unknown" }
        viewport = "$($probe.width)x$($probe.height)"
        p133Audit = [bool]$summary.checks.p133Audit
        routeMaximumStep = if ($routeMatch.Success) { [double]$routeMatch.Groups[1].Value } else { -1 }
        authoredSites = if ($buildMatch.Success) { [int]$buildMatch.Groups[1].Value } else { 0 }
        recommendedSites = if ($buildMatch.Success) { [int]$buildMatch.Groups[2].Value } else { 0 }
        enemyCount = if ($motionMatch.Success) { [int]$motionMatch.Groups[1].Value } else { 0 }
        maximumTurnPose = if ($motionMatch.Success) { [double]$motionMatch.Groups[2].Value } else { -1 }
        minimumShadowGap = if ($motionMatch.Success) { [double]$motionMatch.Groups[3].Value } else { -1 }
        maximumShadowGap = if ($motionMatch.Success) { [double]$motionMatch.Groups[4].Value } else { -1 }
        selectedRings = if ($interactionMatch.Success) { [int]$interactionMatch.Groups[1].Value } else { 0 }
        hoveredRings = if ($interactionMatch.Success) { [int]$interactionMatch.Groups[2].Value } else { 0 }
        maximumInteractionDiameter = if ($interactionMatch.Success) { [double]$interactionMatch.Groups[3].Value } else { -1 }
        uiBounds = [bool]$summary.checks.uiBounds
        uiOverlap = [bool]$summary.checks.uiOverlap
        uiTextFit = [bool]$summary.checks.uiTextFit
        consoleClean = [bool]$summary.checks.consoleClean
        screenshot = [IO.Path]::GetFullPath($screenshotPath)
        summary = [IO.Path]::GetFullPath($summaryPath)
    })
}

$pass = $results.Count -eq $probes.Count -and @($results | Where-Object {
    -not $_.p133Audit -or -not $_.uiBounds -or -not $_.uiOverlap -or
    -not $_.uiTextFit -or -not $_.consoleClean
}).Count -eq 0
$report = [ordered]@{
    schemaVersion = "p133-combat-readability-v1"
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    pass = $pass
    uniqueMaps = @($results.map | Sort-Object -Unique).Count
    acceptance = [ordered]@{
        authoredSitesPerMap = 12
        maximumRouteStep = 0.05
        minimumDenseEnemyCount = 18
        maximumTurnPoseDegrees = 4.3
        footShadowGap = "0.025-0.110"
        maximumInteractionDiameter = 0.90
        persistentUiInteractionMarginPixels = 34
    }
    probes = $results
}
$reportPath = Join-Path $OutputDirectory "p133_combat_readability_matrix.json"
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding UTF8
$report | ConvertTo-Json -Depth 8

if (-not $pass) {
    throw "P13.3 combat readability matrix failed. Inspect $reportPath."
}
