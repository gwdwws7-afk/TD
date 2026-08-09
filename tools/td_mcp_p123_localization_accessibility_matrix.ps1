param(
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [string]$OutputRoot = "E:/TD/output/playtest/p123_matrix",
    [switch]$RefreshScripts
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$runs = New-Object System.Collections.Generic.List[object]
$resolutions = @(
    @{ Width = 960; Height = 540; Name = "960x540" },
    @{ Width = 1920; Height = 1080; Name = "1920x1080" }
)
$languages = @("English", "Chinese")
$surfaces = @("Campaign", "Formation", "Profile", "Settings")
$refreshPending = $RefreshScripts.IsPresent

foreach ($resolution in $resolutions) {
    foreach ($language in $languages) {
        foreach ($surface in $surfaces) {
            $isLastRun = $resolution.Name -eq $resolutions[-1].Name -and
                         $language -eq $languages[-1] -and
                         $surface -eq $surfaces[-1]
            $stem = "p123_$($resolution.Name)_$($language.ToLowerInvariant())_$($surface.ToLowerInvariant())"
            $screenshot = Join-Path $OutputRoot "$stem.png"
            $summary = Join-Path $OutputRoot "$stem.json"
            $arguments = @{
                McpUrl = $McpUrl
                LevelIndex = 9
                DurationSeconds = 1
                ViewportWidth = $resolution.Width
                ViewportHeight = $resolution.Height
                P123Language = $language
                ScreenshotPath = $screenshot
                SummaryPath = $summary
                SkipAutoBuild = $true
                SkipStartWave = $true
                RunP123Audit = $true
                UnityReadyTimeoutSeconds = 90
            }
            if (-not $isLastRun) {
                $arguments.KeepPlaying = $true
            }
            switch ($surface) {
                "Settings" { $arguments.PrepareP123Settings = $true }
                "Formation" { $arguments.PrepareP123Formation = $true }
                "Profile" { $arguments.PrepareP123Profile = $true }
                default { $arguments.PrepareP123Campaign = $true }
            }
            if ($refreshPending) {
                $arguments.RefreshScripts = $true
                $refreshPending = $false
            }

            $completed = $false
            Start-Sleep -Seconds 3
            for ($attempt = 1; $attempt -le 5 -and -not $completed; $attempt++) {
                try {
                    & "$PSScriptRoot/td_mcp_playtest.ps1" @arguments | Out-Null
                    $completed = $true
                } catch {
                    if ($attempt -ge 5) {
                        throw
                    }

                    Start-Sleep -Seconds 5
                }
            }
            $result = Get-Content -LiteralPath $summary -Raw | ConvertFrom-Json
            $runs.Add([pscustomobject]@{
                resolution = $resolution.Name
                language = $language
                surface = $surface
                passed = -not ($result.checks.PSObject.Properties | Where-Object { $_.Value -eq $false })
                screenshot = $screenshot
                summary = $summary
                p123Audit = $result.checks.p123Audit
                uiBounds = $result.checks.uiBounds
                uiOverlap = $result.checks.uiOverlap
                uiTextFit = $result.checks.uiTextFit
                consoleClean = $result.checks.consoleClean
            })
        }
    }
}

$index = [ordered]@{
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    runs = $runs
    passed = ($runs | Where-Object { -not $_.passed }).Count -eq 0
}
$indexPath = Join-Path $OutputRoot "p123_matrix_index.json"
$index | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $indexPath -Encoding UTF8
$index | ConvertTo-Json -Depth 8
