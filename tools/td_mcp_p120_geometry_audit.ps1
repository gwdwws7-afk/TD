param(
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [string]$OutputDirectory = "E:/TD/output/playtest/p120_geometry_audit",
    [switch]$RefreshScripts
)

$ErrorActionPreference = "Stop"
$runner = Join-Path $PSScriptRoot "td_mcp_playtest.ps1"
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$probes = @(
    @{ level = 1; map = "grayline_junction" },
    @{ level = 5; map = "ashfall_depot" },
    @{ level = 9; map = "split_switch_canyon" },
    @{ level = 13; map = "hollow_kiln_basin" },
    @{ level = 17; map = "last_ember_terminus" },
    @{ level = 20; map = "last_ember_terminus_boss" }
)

$results = @()
for ($index = 0; $index -lt $probes.Count; $index++) {
    $probe = $probes[$index]
    $slug = "l$($probe.level.ToString('00'))_$($probe.map)"
    $arguments = @{
        McpUrl = $McpUrl
        LevelIndex = $probe.level
        DurationSeconds = 0
        ViewportWidth = 1280
        ViewportHeight = 720
        SkipAutoBuild = $true
        SkipStartWave = $true
        RunP120GeometryAudit = $true
        PreserveCampaignProgress = $true
        ScreenshotPath = (Join-Path $OutputDirectory "$slug.png").Replace("\", "/")
        SummaryPath = Join-Path $OutputDirectory "$slug.json"
    }
    if ($RefreshScripts -and $index -eq 0) {
        $arguments.RefreshScripts = $true
    }

    $result = & $runner @arguments | ConvertFrom-Json
    $results += [ordered]@{
        level = $probe.level
        map = $probe.map
        screenshot = [IO.Path]::GetFullPath((Join-Path $OutputDirectory "$slug.png"))
        summary = [IO.Path]::GetFullPath((Join-Path $OutputDirectory "$slug.json"))
        geometry = [bool]$result.checks.p120GeometryAudit
        uiBounds = [bool]$result.checks.uiBounds
        uiOverlap = [bool]$result.checks.uiOverlap
        uiTextFit = [bool]$result.checks.uiTextFit
        consoleClean = [bool]$result.checks.consoleClean
    }
}

$pass = @($results | Where-Object {
    -not ($_.geometry -and $_.uiBounds -and $_.uiOverlap -and $_.uiTextFit -and $_.consoleClean)
}).Count -eq 0
$index = [ordered]@{
    schemaVersion = "p120-geometry-audit-v1"
    pass = $pass
    uniqueMaps = @($results.map | ForEach-Object { $_ -replace "_boss$", "" } | Sort-Object -Unique).Count
    probes = $results
}
$indexPath = Join-Path $OutputDirectory "p120_geometry_index.json"
$index | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $indexPath -Encoding UTF8
$index | ConvertTo-Json -Depth 8

if (-not $pass) {
    throw "P12.0 geometry audit failed. Inspect $indexPath."
}
