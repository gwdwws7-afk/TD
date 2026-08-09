param(
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [string]$OutputDirectory = "E:/TD/output/playtest/p134_audio_visual_input",
    [switch]$RefreshScripts,
    [switch]$ResumeExisting
)

$ErrorActionPreference = "Stop"
$runner = Join-Path $PSScriptRoot "td_mcp_playtest.ps1"
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$probes = @(
    @{ name = "l01_opening_1280"; level = 1; width = 1280; height = 720 },
    @{ name = "l09_split_1280"; level = 9; width = 1280; height = 720 },
    @{ name = "l13_hollow_1280"; level = 13; width = 1280; height = 720 },
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
            if ([bool]$existing.checks.p134Audit -and
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
        PrepareP134Combat = $true
        RunP134Audit = $true
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
            # The runner writes a summary before raising a failed acceptance check.
        }

        if (Test-Path -LiteralPath $summaryPath) {
            try {
                $summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
            } catch {
                $summary = $null
            }
        }
        if ($null -ne $summary -and [bool]$summary.checks.p134Audit) {
            break
        }

        $arguments.Remove("RefreshScripts")
        Start-Sleep -Seconds 2
    }

    if ($null -eq $summary) {
        throw "P13.4 probe did not produce a readable summary: $($probe.name)"
    }

    $stateText = [string]$summary.state.data.result
    $signalMatch = [regex]::Match(
        $stateText,
        "(?m)^p13\.4\.audit\.signalBudget=True \[active=([0-9]+),max=([0-9]+),suppressed=([0-9]+),duration=([0-9.]+),alpha=([0-9.]+)")
    $towerMatch = [regex]::Match(
        $stateText,
        "(?m)^p13\.4\.audit\.towerIdentity=True \[kinds=([0-9]+),charge=([0-9]+),projectile=([0-9]+),impact=([0-9]+),upgrade=([0-9]+)")
    $audioMatch = [regex]::Match(
        $stateText,
        "(?m)^p13\.4\.audit\.audio=True \[clips=([0-9]+)/([0-9]+),sources=([0-9]+)/([0-9]+)")
    $fxMatch = [regex]::Match(
        $stateText,
        "(?m)^p13\.4\.audit\.fxBudget=True \[active=([0-9]+)/([0-9]+),max=([0-9]+),suppressed=([0-9]+),duration=([0-9.]+),alpha=([0-9.]+)")

    $results.Add([pscustomobject]@{
        name = $probe.name
        level = $probe.level
        viewport = "$($probe.width)x$($probe.height)"
        p134Audit = [bool]$summary.checks.p134Audit
        activeSignals = if ($signalMatch.Success) { [int]$signalMatch.Groups[1].Value } else { -1 }
        maximumSignals = if ($signalMatch.Success) { [int]$signalMatch.Groups[2].Value } else { -1 }
        suppressedSignals = if ($signalMatch.Success) { [int]$signalMatch.Groups[3].Value } else { -1 }
        maximumSignalDuration = if ($signalMatch.Success) { [double]$signalMatch.Groups[4].Value } else { -1 }
        maximumSignalAlpha = if ($signalMatch.Success) { [double]$signalMatch.Groups[5].Value } else { -1 }
        towerKinds = if ($towerMatch.Success) { [int]$towerMatch.Groups[1].Value } else { 0 }
        chargeLanguages = if ($towerMatch.Success) { [int]$towerMatch.Groups[2].Value } else { 0 }
        projectileLanguages = if ($towerMatch.Success) { [int]$towerMatch.Groups[3].Value } else { 0 }
        impactLanguages = if ($towerMatch.Success) { [int]$towerMatch.Groups[4].Value } else { 0 }
        upgradeLanguages = if ($towerMatch.Success) { [int]$towerMatch.Groups[5].Value } else { 0 }
        audioClips = if ($audioMatch.Success) { [int]$audioMatch.Groups[1].Value } else { 0 }
        audioClipTarget = if ($audioMatch.Success) { [int]$audioMatch.Groups[2].Value } else { 0 }
        audioSources = if ($audioMatch.Success) { [int]$audioMatch.Groups[3].Value } else { 0 }
        maximumFx = if ($fxMatch.Success) { [int]$fxMatch.Groups[3].Value } else { -1 }
        suppressedFx = if ($fxMatch.Success) { [int]$fxMatch.Groups[4].Value } else { -1 }
        maximumFxDuration = if ($fxMatch.Success) { [double]$fxMatch.Groups[5].Value } else { -1 }
        maximumFxAlpha = if ($fxMatch.Success) { [double]$fxMatch.Groups[6].Value } else { -1 }
        uiBounds = [bool]$summary.checks.uiBounds
        uiOverlap = [bool]$summary.checks.uiOverlap
        uiTextFit = [bool]$summary.checks.uiTextFit
        consoleClean = [bool]$summary.checks.consoleClean
        screenshot = [IO.Path]::GetFullPath($screenshotPath)
        summary = [IO.Path]::GetFullPath($summaryPath)
    })
}

$pass = $results.Count -eq $probes.Count -and @($results | Where-Object {
    -not $_.p134Audit -or -not $_.uiBounds -or -not $_.uiOverlap -or
    -not $_.uiTextFit -or -not $_.consoleClean -or $_.towerKinds -ne 8 -or
    $_.chargeLanguages -ne 8 -or $_.projectileLanguages -ne 8 -or
    $_.impactLanguages -ne 8 -or $_.upgradeLanguages -ne 8 -or
    $_.maximumSignals -gt 12 -or $_.maximumFx -gt 32
}).Count -eq 0
$report = [ordered]@{
    schemaVersion = "p134-audio-visual-input-v1"
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    pass = $pass
    acceptance = [ordered]@{
        feedbackKinds = 8
        cinematicKinds = 4
        towerPresentationLanguages = 8
        maximumSignals = 12
        maximumSignalDuration = 1.05
        maximumSignalAlpha = 0.96
        maximumWorldFx = 32
        maximumWorldFxDuration = 0.90
        maximumWorldFxAlpha = 0.96
        inputModes = @("mouse", "keyboard", "gamepad")
    }
    probes = $results
}
$reportPath = Join-Path $OutputDirectory "p134_audio_visual_input_matrix.json"
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding UTF8
$report | ConvertTo-Json -Depth 8

if (-not $pass) {
    throw "P13.4 audio visual input matrix failed. Inspect $reportPath."
}
