
$ErrorActionPreference = "Stop"
$unity = "D:/unity/2022.3.12f1/Editor/Data"
$bclDir = "$unity/UnityReferenceAssemblies/unity-4.8-api"
$csc = "$unity/DotNetSdkRoslyn/csc.dll"
$dotnet = "$unity/NetCoreRuntime/dotnet.exe"
$root = "E:/TD"
$nunit = "$root/Library/PackageCache/com.unity.ext.nunit@1.0.6/net35/unity-custom/nunit.framework.dll"

$refs = New-Object System.Collections.Generic.List[string]
foreach ($n in @("mscorlib.dll","System.dll","System.Core.dll","Facades/netstandard.dll")) {
  $p = Join-Path $bclDir $n
  if (Test-Path $p) { $refs.Add("/reference:$p") }
}
Get-ChildItem "$unity/Managed/UnityEngine/*.dll" | Where-Object { $_.Name -ne "UnityEngine.dll" } | ForEach-Object { $refs.Add("/reference:" + $_.FullName) }
Get-ChildItem "$root/Library/ScriptAssemblies/*.dll" | Where-Object { $_.Name -notmatch "^TD\.(Game|Tests)" } | ForEach-Object { $refs.Add("/reference:" + $_.FullName) }

$gated = @("TDGameManager.P124.cs","TDGameManager.P1252.cs","TDGameManager.P1253.cs","TDGameManager.P1254.cs","TDGameManager.P134.cs","TDGameManager.P135.cs","TDStandaloneSmokeProbe.cs","TDP1254StandaloneProbe.cs","TDBalanceSimulator.cs","TDCampaignProgression.P1254.cs")
$allGame = Get-ChildItem "$root/Assets/Scripts/TowerDefense/*.cs" | ForEach-Object { $_.FullName }
$srcRelease = $allGame | Where-Object { $gated -notcontains (Split-Path $_ -Leaf) }
$srcTests = Get-ChildItem "$root/Assets/Scripts/TD.Tests/*.cs" | ForEach-Object { $_.FullName }

function Compile([string[]]$sources, [string]$defines, [string]$out, [string[]]$extraRefs) {
  $a = @($csc, "/nologo", "/target:library", "/nowarn:CS0649,CS0414,CS8981", "/langversion:9.0")
  if ($defines) { $a += "/define:$defines" }
  foreach ($r in $refs) { $a += $r }
  foreach ($r in $extraRefs) { $a += $r }
  foreach ($s in $sources) { $a += $s }
  $a += "/out:$out"
  $t = & $dotnet $a 2>&1
  $errs = $t | Where-Object { $_ -match "error CS" }
  if ($errs) { $errs | Select-Object -First 15 | ForEach-Object { Write-Host $_ } }
  return $LASTEXITCODE
}

Write-Host "=== A. Release path (gated excluded) ==="
$rc1 = Compile $srcRelease $null "$env:TEMP/td_rel.dll" @()
Write-Host "exit=$rc1"
Write-Host "=== B. Full source WITH TD_AUTOMATION ==="
$rc2 = Compile $allGame "TD_AUTOMATION" "$env:TEMP/td_auto.dll" @()
Write-Host "exit=$rc2"
Write-Host "=== C. Tests ==="
$rc3 = Compile $srcTests "UNITY_EDITOR" "$env:TEMP/td_tests.dll" @("/reference:$env:TEMP/td_rel.dll", "/reference:$nunit")
Write-Host "exit=$rc3"
if (($rc1 -ne 0) -or ($rc2 -ne 0) -or ($rc3 -ne 0)) { Write-Host "FAILED"; exit 1 } else { Write-Host "ALL CLEAN" }
