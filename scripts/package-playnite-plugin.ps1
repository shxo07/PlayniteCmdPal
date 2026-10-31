[CmdletBinding()]
param (
    [string] $Configuration = 'Release',
    [string] $OutputRoot = (Join-Path $PSScriptRoot '..\artifacts\playnite-plugin')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repoRoot 'PlayniteCmdPal.PlaynitePlugin\PlayniteCmdPal.PlaynitePlugin.csproj'
$publishDir = Join-Path $OutputRoot 'publish'
$packagePath = Join-Path $OutputRoot 'PlayniteCmdPalBridge.pext'

if (Test-Path -LiteralPath $publishDir) { Remove-Item -LiteralPath $publishDir -Recurse -Force }
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
# Do not leave an old package looking like the output of a failed build/smoke test.
if (Test-Path -LiteralPath $packagePath) { Remove-Item -LiteralPath $packagePath -Force }
if (Test-Path -LiteralPath ($packagePath + '.zip')) { Remove-Item -LiteralPath ($packagePath + '.zip') -Force }

& dotnet publish $project --configuration $Configuration --output $publishDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$manifest = Join-Path $publishDir 'extension.yaml'
$module = Join-Path $publishDir 'PlayniteCmdPal.PlaynitePlugin.dll'
$snapshot = Join-Path $publishDir 'PlayniteCmdPal.Snapshot.dll'
if (-not (Test-Path -LiteralPath $manifest) -or -not (Test-Path -LiteralPath $module) -or -not (Test-Path -LiteralPath $snapshot)) {
    throw 'The publish output is missing extension.yaml, the plugin module, or the snapshot assembly.'
}
if (Test-Path -LiteralPath (Join-Path $publishDir 'System.Text.Json.dll')) {
    throw 'Unexpected legacy System.Text.Json.dll in publish output. Clean the project bin/obj directories and package again.'
}

# Test the published net462 assembly on .NET Framework, not only the net10
# assembly used by the adapter unit tests. Playnite does not supply System.Text.Json.
$frameworkPowerShell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$frameworkSmoke = Join-Path $repoRoot 'scripts\test-playnite-snapshot-netfx.ps1'
if (-not (Test-Path -LiteralPath $frameworkPowerShell)) {
    throw 'Windows PowerShell 5.1 is required for the .NET Framework bridge smoke test.'
}
Write-Host 'Testing the published bridge on .NET Framework...'
& $frameworkPowerShell -NoProfile -ExecutionPolicy Bypass -File $frameworkSmoke -SnapshotAssembly $snapshot -PluginAssembly $module
if ($LASTEXITCODE -ne 0) { throw "The .NET Framework bridge smoke test failed with exit code $LASTEXITCODE." }

Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath ($packagePath + '.zip')
Move-Item -LiteralPath ($packagePath + '.zip') -Destination $packagePath
Write-Host "Created $packagePath"
Write-Host 'Install locally by dragging the .pext onto the main Playnite window, or with Playnite.DesktopApp.exe --installext <path-to.pext>. Restart Playnite after installation. Do not commit the package or publish output.'
