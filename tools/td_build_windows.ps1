param(
    [string]$OutputRoot = "E:/TD/output/builds/p1251_windows",
    [string]$UnityPath = "D:/unity/2022.3.12f1/Editor/Unity.exe",
    [string]$McpUrl = "http://127.0.0.1:8080/mcp",
    [string]$Version = "0.12.5",
    [ValidateRange(1, 2147483647)]
    [int]$BuildNumber = 1,
    [ValidateSet("Mono", "IL2CPP")]
    [string]$Backend = "Mono",
    [switch]$Development,
    [switch]$ForceBatchMode,
    [switch]$SkipSmoke,
    [ValidateRange(120, 3600)]
    [int]$BuildTimeoutSeconds = 1200,
    [ValidateRange(30, 1800)]
    [int]$SmokeTimeoutSeconds = 150,
    [ValidateRange(1, 20)]
    [float]$SmokeTimeScale = 16,
    [ValidateRange(0, 5000)]
    [int]$SmokeTechnicalIntegrity = 0,
    [switch]$CleanOutput
)

$ErrorActionPreference = "Stop"
$startedUtc = [DateTime]::UtcNow
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$outputFull = [IO.Path]::GetFullPath($OutputRoot)
$workspacePrefix = $projectRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
if (-not $outputFull.StartsWith($workspacePrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputRoot must stay inside the workspace: $projectRoot"
}

if ($CleanOutput -and (Test-Path -LiteralPath $outputFull)) {
    $resolvedOutput = (Resolve-Path -LiteralPath $outputFull).Path
    if (-not $resolvedOutput.StartsWith($workspacePrefix, [StringComparison]::OrdinalIgnoreCase) -or
        $resolvedOutput -eq $projectRoot) {
        throw "Refusing to clean unsafe output path: $resolvedOutput"
    }
    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}
New-Item -ItemType Directory -Path $outputFull -Force | Out-Null
$exePath = Join-Path $outputFull "EmberlineDefense.exe"
$dataPath = Join-Path $outputFull "EmberlineDefense_Data"
$buildResultPath = Join-Path $outputFull "build-result.json"
$buildLogPath = Join-Path $outputFull "unity-build.log"
$smokeReportPath = Join-Path $outputFull "standalone-smoke.json"
$playerLogPath = Join-Path $outputFull "standalone-player.log"
$auditPath = Join-Path $outputFull "p1251_build_audit.json"
$embeddedIconPath = Join-Path $outputFull "embedded-app-icon.png"

foreach ($path in @($buildResultPath, $smokeReportPath, $playerLogPath, $auditPath, $embeddedIconPath)) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Force
    }
}

function ConvertFrom-McpEvent {
    param([string]$Content)

    $dataLine = ($Content -split "`n" | Where-Object { $_ -like "data:*" } | Select-Object -First 1)
    if (-not $dataLine) {
        return $Content | ConvertFrom-Json
    }

    return $dataLine.Substring(5).Trim() | ConvertFrom-Json
}

function New-McpSession {
    param([string]$Url)

    $headers = @{
        Accept = "application/json, text/event-stream"
        "Content-Type" = "application/json"
    }
    $body = @{
        jsonrpc = "2.0"
        id = 1
        method = "initialize"
        params = @{
            protocolVersion = "2025-06-18"
            capabilities = @{}
            clientInfo = @{ name = "td-windows-builder"; version = "1.0" }
        }
    } | ConvertTo-Json -Depth 20
    $response = Invoke-WebRequest -Uri $Url -Method Post -Headers $headers -Body $body -UseBasicParsing -TimeoutSec 20
    $sessionId = $response.Headers["Mcp-Session-Id"]
    if ([string]::IsNullOrWhiteSpace($sessionId)) {
        throw "MCP initialize did not return a session id."
    }

    $headers["Mcp-Session-Id"] = $sessionId
    $notification = @{
        jsonrpc = "2.0"
        method = "notifications/initialized"
        params = @{}
    } | ConvertTo-Json -Depth 5
    try {
        Invoke-WebRequest -Uri $Url -Method Post -Headers $headers -Body $notification -UseBasicParsing -TimeoutSec 10 | Out-Null
    } catch {
    }

    return $sessionId
}

function Invoke-UnityCode {
    param(
        [string]$Url,
        [string]$SessionId,
        [string]$Code,
        [int]$TimeoutSeconds = 45
    )

    $headers = @{
        Accept = "application/json, text/event-stream"
        "Content-Type" = "application/json"
        "Mcp-Session-Id" = $SessionId
    }
    $body = @{
        jsonrpc = "2.0"
        id = 2
        method = "tools/call"
        params = @{
            name = "execute_code"
            arguments = @{
                action = "execute"
                code = $Code
                safety_checks = $true
                compiler = "auto"
            }
        }
    } | ConvertTo-Json -Depth 30
    $response = Invoke-WebRequest -Uri $Url -Method Post -Headers $headers -Body $body -UseBasicParsing -TimeoutSec $TimeoutSeconds
    return ConvertFrom-McpEvent $response.Content
}

function ConvertTo-CSharpLiteral {
    param([string]$Value)
    return '@"' + $Value.Replace('"', '""') + '"'
}

function Wait-ForFile {
    param(
        [string]$Path,
        [int]$TimeoutSeconds
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $Path) {
            return
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for $Path"
}

function Test-UnityProjectActive {
    $lockPath = Join-Path $projectRoot "Temp/UnityLockfile"
    if (Test-Path -LiteralPath $lockPath -PathType Leaf) {
        try {
            $stream = [IO.File]::Open(
                $lockPath,
                [IO.FileMode]::Open,
                [IO.FileAccess]::ReadWrite,
                [IO.FileShare]::None)
            $stream.Dispose()
        } catch {
            return $true
        }
    }

    $normalizedProject = $projectRoot.Replace('/', '\').TrimEnd('\')
    $matchingEditor = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" `
        -ErrorAction SilentlyContinue |
        Where-Object {
            -not [string]::IsNullOrWhiteSpace($_.CommandLine) -and
            $_.CommandLine.Replace('/', '\').IndexOf(
                $normalizedProject,
                [StringComparison]::OrdinalIgnoreCase) -ge 0
        } |
        Select-Object -First 1
    return $null -ne $matchingEditor
}

$revision = "unknown"
try {
    $revision = (& git -C $projectRoot rev-parse --short=12 HEAD 2>$null).Trim()
    $dirty = @(& git -C $projectRoot status --porcelain --untracked-files=no 2>$null).Count -gt 0
    if ($dirty) {
        $revision += "-dirty"
    }
} catch {
}

$usedMcp = $false
$projectWasActive = Test-UnityProjectActive
if (-not $ForceBatchMode -and $projectWasActive) {
    $queueDeadline = [DateTime]::UtcNow.AddSeconds(45)
    $lastQueueError = "Unity MCP was not attempted."
    while ([DateTime]::UtcNow -lt $queueDeadline -and -not $usedMcp) {
        try {
            Write-Output "[P12.5.1] Requesting Windows build through Unity MCP..."
            $sessionId = New-McpSession -Url $McpUrl
            $code = "return TD.Editor.TDReleaseBuilder.BuildWindowsForMcp(" +
                    "$(ConvertTo-CSharpLiteral $exePath), " +
                    "$(ConvertTo-CSharpLiteral $Version), $BuildNumber, " +
                    "$(([bool]$Development).ToString().ToLowerInvariant()), " +
                    "$(ConvertTo-CSharpLiteral $Backend), " +
                    "$(ConvertTo-CSharpLiteral $revision), " +
                    "$(ConvertTo-CSharpLiteral $buildResultPath));"
            $response = Invoke-UnityCode -Url $McpUrl -SessionId $sessionId -Code $code -TimeoutSeconds $BuildTimeoutSeconds
            $content = $response.result.structuredContent
            if ($null -eq $content -or $null -eq $content.PSObject.Properties["success"] -or
                -not [bool]$content.success -or [bool]$response.result.isError) {
                $detail = if ($null -eq $content) { "missing structuredContent" } else { $content | ConvertTo-Json -Depth 10 -Compress }
                throw $detail
            }
            $usedMcp = $true
            Write-Output "[P12.5.1] Unity MCP build call completed."
        } catch {
            $lastQueueError = $_.Exception.Message
            if (Test-Path -LiteralPath $buildResultPath) {
                try {
                    $completedBuild = Get-Content -LiteralPath $buildResultPath -Raw | ConvertFrom-Json
                    if ([bool]$completedBuild.passed -and (Test-Path -LiteralPath $exePath)) {
                        $usedMcp = $true
                        Write-Warning "Unity completed the build but the MCP response was interrupted; using the written build result."
                        break
                    }
                } catch {
                }
            }
            Write-Warning "Unity MCP build attempt failed: $lastQueueError"
            Start-Sleep -Seconds 2
        }
    }

    if (-not $usedMcp -and (Test-UnityProjectActive)) {
        throw "Could not queue the build through the running Unity editor: $lastQueueError"
    }
}

if (-not $usedMcp) {
    if (Test-UnityProjectActive) {
        throw "Refusing to start batch mode while this Unity project is already open."
    }
    if (-not (Test-Path -LiteralPath $UnityPath)) {
        throw "Unity executable not found: $UnityPath"
    }

    $batchArguments = @(
        "-batchmode",
        "-quit",
        "-projectPath", $projectRoot,
        "-executeMethod", "TD.Editor.TDReleaseBuilder.BuildWindowsBatch",
        "-tdOutput", $exePath,
        "-tdVersion", $Version,
        "-tdBuildNumber", $BuildNumber,
        "-tdDevelopment", ([bool]$Development).ToString().ToLowerInvariant(),
        "-tdBackend", $Backend,
        "-tdSourceRevision", $revision,
        "-tdResult", $buildResultPath,
        "-logFile", $buildLogPath
    )
    $unity = Start-Process -FilePath $UnityPath -ArgumentList $batchArguments -PassThru -WindowStyle Hidden
    if (-not $unity.WaitForExit($BuildTimeoutSeconds * 1000)) {
        Stop-Process -Id $unity.Id -Force
        throw "Unity batch build timed out after $BuildTimeoutSeconds seconds."
    }
    if ($unity.ExitCode -ne 0 -and -not (Test-Path -LiteralPath $buildResultPath)) {
        throw "Unity batch build exited with code $($unity.ExitCode). Inspect $buildLogPath"
    }
}

Wait-ForFile -Path $buildResultPath -TimeoutSeconds $BuildTimeoutSeconds
Write-Output "[P12.5.1] Build result received: $buildResultPath"
$build = Get-Content -LiteralPath $buildResultPath -Raw | ConvertFrom-Json
if (-not [bool]$build.passed -or -not (Test-Path -LiteralPath $exePath) -or -not (Test-Path -LiteralPath $dataPath)) {
    throw "Windows build failed. Inspect $buildResultPath"
}

$smoke = $null
$smokeExitCode = $null
if (-not $SkipSmoke) {
    Write-Output "[P12.5.1] Launching standalone full-mission smoke..."
    $smokeArguments = "-screen-fullscreen 0 -screen-width 1280 -screen-height 720 " +
                      "-logFile `"$playerLogPath`" --td-smoke-test " +
                      "--td-smoke-report `"$smokeReportPath`" " +
                      "--td-smoke-time-scale $SmokeTimeScale --td-smoke-timeout $SmokeTimeoutSeconds"
    if ($SmokeTechnicalIntegrity -gt 0) {
        $smokeArguments += " --td-smoke-technical-integrity $SmokeTechnicalIntegrity"
    }
    $player = Start-Process -FilePath $exePath -ArgumentList $smokeArguments -PassThru -WindowStyle Hidden
    $deadline = [DateTime]::UtcNow.AddSeconds($SmokeTimeoutSeconds + 30)
    while (-not $player.HasExited -and [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 500
        $player.Refresh()
    }
    if (-not $player.HasExited) {
        Stop-Process -Id $player.Id -Force
        throw "Standalone smoke timed out after $($SmokeTimeoutSeconds + 30) seconds."
    }
    $smokeExitCode = $player.ExitCode
    Wait-ForFile -Path $smokeReportPath -TimeoutSeconds 10
    $smoke = Get-Content -LiteralPath $smokeReportPath -Raw | ConvertFrom-Json
    Write-Output "[P12.5.1] Standalone smoke exited with code $smokeExitCode."
}

$exe = Get-Item -LiteralPath $exePath
$versionInfo = $exe.VersionInfo
$embeddedIconPass = $false
try {
    Add-Type -AssemblyName System.Drawing
    $embeddedIcon = [System.Drawing.Icon]::ExtractAssociatedIcon($exePath)
    if ($null -ne $embeddedIcon) {
        $iconBitmap = $embeddedIcon.ToBitmap()
        $iconBitmap.Save($embeddedIconPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $iconBitmap.Dispose()
        $embeddedIcon.Dispose()
        $embeddedIconPass = (Get-Item -LiteralPath $embeddedIconPath).Length -gt 0
    }
} catch {
    $embeddedIconPass = $false
}
$buildPass = [bool]$build.passed -and $exe.Length -gt 0 -and (Test-Path -LiteralPath $dataPath)
$smokePass = $SkipSmoke -or ($null -ne $smoke -and [bool]$smoke.passed -and $smokeExitCode -eq 0)
$runtimeIdentityPass = $SkipSmoke -or ($null -ne $smoke -and
                        $smoke.productName -eq "Emberline Defense" -and
                        $smoke.version -eq $Version -and
                        $smoke.sceneName -eq "EmberlineBootstrap")
$identityPass = $build.productName -eq "Emberline Defense" -and
                $build.companyName -eq "Emberline" -and
                $build.applicationIdentifier -eq "com.emberline.defense" -and
                $build.version -eq $Version -and $runtimeIdentityPass
$brandingPass = [bool]$build.iconConfigured -and
                [int]$build.iconSlotCount -eq 8 -and
                [bool]$build.startupBackgroundConfigured -and
                $embeddedIconPass
$audit = [ordered]@{
    schemaVersion = "p1251-build-audit-v1"
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    elapsedSeconds = [Math]::Round(([DateTime]::UtcNow - $startedUtc).TotalSeconds, 2)
    sourceRevision = $revision
    buildVia = if ($usedMcp) { "unity_mcp" } else { "unity_batchmode" }
    outputRoot = $outputFull
    executable = $exePath
    executableBytes = $exe.Length
    executableSha256 = (Get-FileHash -LiteralPath $exePath -Algorithm SHA256).Hash
    productName = $versionInfo.ProductName
    productVersion = $versionInfo.ProductVersion
    runtimeProductName = if ($null -ne $smoke) { $smoke.productName } else { $null }
    runtimeVersion = if ($null -ne $smoke) { $smoke.version } else { $null }
    runtimeScene = if ($null -ne $smoke) { $smoke.sceneName } else { $null }
    runtimeBackend = if ($null -ne $smoke) { $smoke.scriptingBackend } else { $null }
    technicalIntegrity = if ($null -ne $smoke) { [int]$smoke.technicalIntegrity } else { 0 }
    technicalAssistApplied = if ($null -ne $smoke) { [bool]$smoke.technicalAssistApplied } else { $false }
    buildPassed = $buildPass
    identityPassed = $identityPass
    brandingPassed = $brandingPass
    iconSlots = [int]$build.iconSlotCount
    embeddedIcon = $embeddedIconPath
    startupBackgroundConfigured = [bool]$build.startupBackgroundConfigured
    smokeSkipped = [bool]$SkipSmoke
    smokeExitCode = $smokeExitCode
    smokePassed = $smokePass
    hardPass = $buildPass -and $identityPass -and $brandingPass -and $smokePass
    artifacts = [ordered]@{
        buildResult = $buildResultPath
        buildLog = $buildLogPath
        smokeReport = $smokeReportPath
        playerLog = $playerLogPath
    }
}
$audit | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $auditPath -Encoding utf8
$audit | ConvertTo-Json -Depth 12
if (-not $audit.hardPass) {
    throw "P12.5.1 Windows build baseline failed. Inspect $auditPath"
}
