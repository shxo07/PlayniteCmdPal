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

& dotnet publish $project --configuration $Configuration --output $publishDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$manifest = Join-Path $publishDir 'extension.yaml'
$module = Join-Path $publishDir 'PlayniteCmdPal.PlaynitePlugin.dll'
if (-not (Test-Path -LiteralPath $manifest) -or -not (Test-Path -LiteralPath $module)) {
    throw 'The publish output is missing extension.yaml or the plugin module.'
}

if (Test-Path -LiteralPath $packagePath) { Remove-Item -LiteralPath $packagePath -Force }
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath ($packagePath + '.zip')
Move-Item -LiteralPath ($packagePath + '.zip') -Destination $packagePath
Write-Host "Created $packagePath"
Write-Host 'Install locally by dragging the .pext onto the main Playnite window, or with Playnite.DesktopApp.exe --installext <path-to.pext>. Restart Playnite after installation. Do not commit the package or publish output.'
