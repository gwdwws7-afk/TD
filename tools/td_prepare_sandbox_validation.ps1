param(
    [Parameter(Mandatory = $true)]
    [string]$InstallerPath,
    [string]$OutputRoot = "E:/TD/output/release/p1252/sandbox_validation_bundle",
    [string]$ExpectedVersion = "0.12.5",
    [string]$ExpectedSignerThumbprint = "",
    [switch]$RequireTrustedSignature,
    [switch]$Launch,
    [ValidateRange(100, 5000)]
    [int]$TechnicalIntegrity = 1000,
    [ValidateRange(60, 600)]
    [int]$SmokeTimeoutSeconds = 240,
    [ValidateRange(1, 20)]
    [float]$SmokeTimeScale = 16,
    [ValidateRange(5, 60)]
    [int]$LaunchTimeoutMinutes = 20
)

$ErrorActionPreference = "Stop"
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$workspacePrefix = $projectRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
$installerFull = [IO.Path]::GetFullPath($InstallerPath)
$outputFull = [IO.Path]::GetFullPath($OutputRoot)
foreach ($path in @($installerFull, $outputFull)) {
    if (-not $path.StartsWith($workspacePrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Sandbox bundle paths must stay inside the workspace: $path"
    }
}
if (-not (Test-Path -LiteralPath $installerFull -PathType Leaf)) {
    throw "Installer does not exist: $installerFull"
}
if (Test-Path -LiteralPath $outputFull) {
    $resolvedOutput = (Resolve-Path -LiteralPath $outputFull).Path
    if (-not $resolvedOutput.StartsWith($workspacePrefix, [StringComparison]::OrdinalIgnoreCase) -or
        $resolvedOutput -eq $projectRoot) {
        throw "Refusing to clean unsafe sandbox bundle path: $resolvedOutput"
    }
    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}

$bundleWorkspace = Join-Path $outputFull "workspace"
$bundleTools = Join-Path $bundleWorkspace "tools"
$bundlePackage = Join-Path $bundleWorkspace "package"
$bundleOutput = Join-Path $bundleWorkspace "output"
$runnerPath = Join-Path $bundleTools "run-clean-machine-validation.ps1"
$validatorSource = Join-Path $PSScriptRoot "td_validate_installer.ps1"
$validatorTarget = Join-Path $bundleTools "td_validate_installer.ps1"
$installerTarget = Join-Path $bundlePackage ([IO.Path]::GetFileName($installerFull))
$sandboxConfigPath = Join-Path $outputFull "Emberline-P12.5.2-Clean-Machine.wsb"
$bundleAuditPath = Join-Path $outputFull "p1252_sandbox_bundle.json"
$sandboxExecutable = "$env:SystemRoot/System32/WindowsSandbox.exe"

New-Item -ItemType Directory -Path $bundleTools, $bundlePackage, $bundleOutput -Force |
    Out-Null
Copy-Item -LiteralPath $validatorSource -Destination $validatorTarget -Force
Copy-Item -LiteralPath $installerFull -Destination $installerTarget -Force

function ConvertTo-SingleQuotedLiteral {
    param([string]$Value)

    return "'" + $Value.Replace("'", "''") + "'"
}

$runnerLines = @(
    '$ErrorActionPreference = "Stop"',
    '$workspaceRoot = "C:/EmberlineValidation"',
    '$outputRoot = Join-Path $workspaceRoot "output"',
    '$resultPath = Join-Path $outputRoot "p1252_install_validation.json"',
    '$consolePath = Join-Path $outputRoot "sandbox-console.log"',
    '$statusPath = Join-Path $outputRoot "sandbox-exit-code.txt"',
    '$errorPath = Join-Path $outputRoot "sandbox-error.txt"',
    '$validatorArguments = @{',
    '    InstallerPath = ' + (ConvertTo-SingleQuotedLiteral (
        "C:/EmberlineValidation/package/$([IO.Path]::GetFileName($installerTarget))")),
    '    OutputRoot = $outputRoot',
    '    ExpectedVersion = ' + (ConvertTo-SingleQuotedLiteral $ExpectedVersion),
    '    ExpectedSignerThumbprint = ' + (ConvertTo-SingleQuotedLiteral $ExpectedSignerThumbprint),
    "    TechnicalIntegrity = $TechnicalIntegrity",
    "    SmokeTimeoutSeconds = $SmokeTimeoutSeconds",
    "    SmokeTimeScale = $SmokeTimeScale",
    '    CleanMachine = $true'
)
if ($RequireTrustedSignature) {
    $runnerLines += '    RequireTrustedSignature = $true'
}
$runnerLines += @(
    '}',
    '$exitCode = 1',
    'try {',
    '    & (Join-Path $workspaceRoot "tools/td_validate_installer.ps1") @validatorArguments *>&1 |',
    '        Tee-Object -FilePath $consolePath',
    '    $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json',
    '    if (-not [bool]$result.cleanMachineRequested -or -not [bool]$result.passed) {',
    '        throw "Clean-machine report did not pass."',
    '    }',
    '    $exitCode = 0',
    '} catch {',
    '    $_.Exception.ToString() | Set-Content -LiteralPath $errorPath -Encoding utf8',
    '} finally {',
    '    $exitCode | Set-Content -LiteralPath $statusPath -Encoding ascii',
    '    Start-Process "$env:SystemRoot/System32/shutdown.exe" -ArgumentList @("/s", "/t", "15", "/f") -WindowStyle Hidden',
    '}'
)
$runnerLines | Set-Content -LiteralPath $runnerPath -Encoding utf8

$hostFolderXml = [System.Security.SecurityElement]::Escape($bundleWorkspace)
$sandboxXml = @"
<Configuration>
  <MappedFolders>
    <MappedFolder>
      <HostFolder>$hostFolderXml</HostFolder>
      <SandboxFolder>C:\EmberlineValidation</SandboxFolder>
      <ReadOnly>false</ReadOnly>
    </MappedFolder>
  </MappedFolders>
  <Networking>Disable</Networking>
  <ClipboardRedirection>Disable</ClipboardRedirection>
  <PrinterRedirection>Disable</PrinterRedirection>
  <AudioInput>Disable</AudioInput>
  <VideoInput>Disable</VideoInput>
  <MemoryInMB>4096</MemoryInMB>
  <LogonCommand>
    <Command>powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\EmberlineValidation\tools\run-clean-machine-validation.ps1"</Command>
  </LogonCommand>
</Configuration>
"@
$sandboxXml | Set-Content -LiteralPath $sandboxConfigPath -Encoding utf8

$bundleAudit = [ordered]@{
    schemaVersion = "p1252-sandbox-bundle-v1"
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    installer = $installerTarget
    installerSha256 = (Get-FileHash -LiteralPath $installerTarget -Algorithm SHA256).Hash
    expectedVersion = $ExpectedVersion
    expectedSignerThumbprint = $ExpectedSignerThumbprint
    requireTrustedSignature = [bool]$RequireTrustedSignature
    technicalIntegrity = $TechnicalIntegrity
    smokeTimeoutSeconds = $SmokeTimeoutSeconds
    smokeTimeScale = $SmokeTimeScale
    sandboxConfig = $sandboxConfigPath
    windowsSandboxAvailable = Test-Path -LiteralPath $sandboxExecutable -PathType Leaf
    launchRequested = [bool]$Launch
    launched = $false
    validationReport = Join-Path $bundleOutput "p1252_install_validation.json"
    validationPassed = $false
    error = ""
}

if ($Launch) {
    if (-not $bundleAudit.windowsSandboxAvailable) {
        $bundleAudit.error = "Windows Sandbox is not installed or enabled on this host."
    } else {
        Start-Process -FilePath $sandboxExecutable -ArgumentList $sandboxConfigPath | Out-Null
        $bundleAudit.launched = $true
        $deadline = (Get-Date).AddMinutes($LaunchTimeoutMinutes)
        $statusPath = Join-Path $bundleOutput "sandbox-exit-code.txt"
        while ((Get-Date) -lt $deadline -and -not (Test-Path -LiteralPath $statusPath)) {
            Start-Sleep -Seconds 5
        }
        if (-not (Test-Path -LiteralPath $statusPath)) {
            $bundleAudit.error = "Windows Sandbox validation timed out."
        } else {
            $sandboxExitCode = [int](Get-Content -LiteralPath $statusPath -Raw)
            if ($sandboxExitCode -eq 0 -and
                (Test-Path -LiteralPath $bundleAudit.validationReport -PathType Leaf)) {
                $validation = Get-Content -LiteralPath $bundleAudit.validationReport -Raw |
                    ConvertFrom-Json
                $bundleAudit.validationPassed =
                    [bool]$validation.cleanMachineRequested -and [bool]$validation.passed
            }
            if (-not $bundleAudit.validationPassed) {
                $bundleAudit.error = "Windows Sandbox validation failed; inspect the bundle output."
            }
        }
    }
}

$bundleAudit | ConvertTo-Json -Depth 8 |
    Set-Content -LiteralPath $bundleAuditPath -Encoding utf8
$bundleAudit | ConvertTo-Json -Depth 8

if ($Launch -and -not $bundleAudit.validationPassed) {
    throw "P12.5.2 clean-machine validation did not pass. Inspect $bundleAuditPath"
}
