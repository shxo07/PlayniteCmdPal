[CmdletBinding()]
param ()

Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$solutionPath = Join-Path $repoRoot 'PlayniteCmdPal.sln'
$testProjectPath = Join-Path $repoRoot 'PlayniteCmdPal.Core.Tests\PlayniteCmdPal.Core.Tests.csproj'
$adapterTestProjectPath = Join-Path $repoRoot 'PlayniteCmdPal.Playnite.Tests\PlayniteCmdPal.Playnite.Tests.csproj'

if (-not (Test-Path -LiteralPath $solutionPath) -or -not (Test-Path -LiteralPath $testProjectPath) -or -not (Test-Path -LiteralPath $adapterTestProjectPath)) {
    [Console]::Error.WriteLine('ERROR: Expected solution or test project was not found relative to scripts/ci.ps1.')
    exit 1
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    [Console]::Error.WriteLine('ERROR: .NET SDK 10 is required, but dotnet was not found on PATH.')
    exit 127
}

function Invoke-DotNetStep {
    param (
        [Parameter(Mandatory)]
        [string] $Name,

        [Parameter(Mandatory)]
        [string[]] $Arguments
    )

    Write-Host ''
    Write-Host "==> $Name"
    Write-Host ('dotnet ' + ($Arguments -join ' '))

    & dotnet @Arguments
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        [Console]::Error.WriteLine("ERROR: $Name failed with exit code $exitCode.")
        exit $exitCode
    }
}

Invoke-DotNetStep -Name '.NET SDK information' -Arguments @('--info')
Invoke-DotNetStep -Name 'Restore' -Arguments @('restore', $solutionPath)
Invoke-DotNetStep -Name 'Build (Release)' -Arguments @('build', $solutionPath, '--configuration', 'Release', '--no-restore')
Invoke-DotNetStep -Name 'Test core search (Release)' -Arguments @('test', $testProjectPath, '--configuration', 'Release', '--no-build', '--no-restore')
Invoke-DotNetStep -Name 'Test Playnite adapter (Release)' -Arguments @('test', $adapterTestProjectPath, '--configuration', 'Release', '--no-build', '--no-restore')

$frameworkPowerShell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$frameworkSmoke = Join-Path $repoRoot 'scripts\test-playnite-snapshot-netfx.ps1'
$pluginBuildDir = Join-Path $repoRoot 'PlayniteCmdPal.PlaynitePlugin\bin\Release\net462'
if (-not (Test-Path -LiteralPath $frameworkPowerShell)) {
    [Console]::Error.WriteLine('ERROR: Windows PowerShell 5.1 is required for the .NET Framework bridge smoke test.')
    exit 127
}

Write-Host ''
Write-Host '==> Test Playnite snapshot publication on .NET Framework (synthetic data)'
& $frameworkPowerShell -NoProfile -ExecutionPolicy Bypass -File $frameworkSmoke -SnapshotAssembly (Join-Path $pluginBuildDir 'PlayniteCmdPal.Snapshot.dll') -PluginAssembly (Join-Path $pluginBuildDir 'PlayniteCmdPal.PlaynitePlugin.dll')
if ($LASTEXITCODE -ne 0) {
    [Console]::Error.WriteLine("ERROR: .NET Framework snapshot smoke test failed with exit code $LASTEXITCODE.")
    exit $LASTEXITCODE
}

Write-Host ''
Write-Host 'All local CI checks passed.'
