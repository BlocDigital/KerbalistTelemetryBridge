[CmdletBinding()]
param(
    [string]$Ksp2Root = "C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program 2",
    [switch]$Deploy
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$managed = Join-Path $Ksp2Root "KSP2_x64_Data\Managed"
$out = Join-Path $root ".build\KerbalistTelemetryBridge"
$dll = Join-Path $out "KerbalistTelemetryBridge.dll"

if (!(Test-Path $managed)) { throw "Managed directory not found: $managed" }

$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (!(Test-Path $csc)) { $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (!(Test-Path $csc)) { throw "Microsoft .NET Framework csc.exe not found." }

$refs = @(
    "mscorlib.dll", "System.dll", "System.Core.dll", "System.Net.Http.dll", "netstandard.dll",
    "UnityEngine.dll", "UnityEngine.CoreModule.dll", "Unity.Entities.dll", "KSPLogging.dll", "ReduxLib.dll", "SpaceWarp2.dll",
    "Assembly-CSharp.dll", "Newtonsoft.Json.dll"
) | ForEach-Object {
    $p = Join-Path $managed $_
    if (!(Test-Path $p)) { throw "Required assembly not found: $p" }
    $p
}

New-Item -ItemType Directory -Force -Path $out | Out-Null

$sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' -File | ForEach-Object { $_.FullName })
if ($sources.Count -eq 0) { throw "No C# source files found in: $(Join-Path $root 'src')" }

$compilerArgs = @('/nologo','/noconfig','/nostdlib+','/target:library','/optimize+','/debug-',('/out:' + $dll))
foreach ($r in $refs) { $compilerArgs += '/reference:' + $r }
$compilerArgs += $sources

& $csc $compilerArgs
if ($LASTEXITCODE -ne 0) { throw "Compilation failed with exit code $LASTEXITCODE" }

Copy-Item (Join-Path $root 'swinfo.json') $out -Force

$zip = Join-Path $root '.build\KerbalistTelemetryBridge.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip -CompressionLevel Optimal
Write-Host "Built: $zip"

if ($Deploy) {
    $mods = Join-Path $Ksp2Root 'mods'
    if (!(Test-Path $mods)) { throw "Redux mods directory not found: $mods" }
    $dest = Join-Path $mods 'KerbalistTelemetryBridge'
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Copy-Item (Join-Path $out '*') $dest -Force
    Write-Host "Deployed to: $dest"
}
