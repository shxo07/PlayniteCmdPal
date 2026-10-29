[CmdletBinding()]
param ()

Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$solutionPath = Join-Path $repoRoot 'testExtension.sln'
$testProjectPath = Join-Path $repoRoot 'PlayniteCmdPal.Core.Tests\PlayniteCmdPal.Core.Tests.csproj'

if (-not (Test-Path -LiteralPath $solutionPath) -or -not (Test-Path -LiteralPath $testProjectPath)) {
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
        [Console]::Error.WriteLine("ERROR: {0} failed with exit code {1}." -f $Name, $exitCode)
        exit $exitCode
    }
}

Invoke-DotNetStep -Name '.NET SDK information' -Arguments @('--info')
Invoke-DotNetStep -Name 'Restore' -Arguments @('restore', $solutionPath)
Invoke-DotNetStep -Name 'Build (Release)' -Arguments @('build', $solutionPath, '--configuration', 'Release', '--no-restore')
Invoke-DotNetStep -Name 'Test core search (Release)' -Arguments @('test', $testProjectPath, '--configuration', 'Release', '--no-build', '--no-restore')

Write-Host ''
Write-Host 'All local CI checks passed.'
