[CmdletBinding()]
param (
    [Parameter(Mandatory)] [string] $SnapshotAssembly,
    [Parameter(Mandatory)] [string] $PluginAssembly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Run in a clean Windows PowerShell 5.1 process: unlike pwsh, it uses the
# .NET Framework CLR used by Playnite. No Playnite installation is needed.
if ($PSVersionTable.PSEdition -ne 'Desktop') {
    throw 'This smoke test must run in Windows PowerShell 5.1 (.NET Framework), not pwsh.'
}

$snapshotPath = (Resolve-Path -LiteralPath $SnapshotAssembly -ErrorAction Stop).Path
$pluginPath = (Resolve-Path -LiteralPath $PluginAssembly -ErrorAction Stop).Path
$snapshotAssemblyObject = [Reflection.Assembly]::LoadFrom($snapshotPath)
$pluginMetadata = [Reflection.Assembly]::ReflectionOnlyLoadFrom($pluginPath)

foreach ($assembly in @($snapshotAssemblyObject, $pluginMetadata)) {
    $jsonReferences = @($assembly.GetReferencedAssemblies() | Where-Object { $_.Name -eq 'System.Text.Json' })
    if ($jsonReferences.Count -ne 0) {
        throw "Bridge assembly $($assembly.GetName().Name) still references System.Text.Json."
    }
}

$gameType = $snapshotAssemblyObject.GetType('PlayniteCmdPal.Snapshot.SnapshotGame', $true)
$snapshotType = $snapshotAssemblyObject.GetType('PlayniteCmdPal.Snapshot.CatalogSnapshot', $true)
$publisherType = $snapshotAssemblyObject.GetType('PlayniteCmdPal.Snapshot.SnapshotFilePublisher', $true)
$gameConstructor = $gameType.GetConstructor([type[]]@([string], [string], [bool], [string], [string]))
$snapshotConstructor = @($snapshotType.GetConstructors())[0]
$publisher = $publisherType.GetMethod('PublishAsync')

# Purely synthetic data exercises JSON escaping, non-ASCII characters, optional
# fields, and isolated UTF-16 surrogate replacement without reading any library.
$prefix = 'Example "quoted" \ path ' + [char]10 + [char]0x00e9 + [char]::ConvertFromUtf32(0x1f3ae)
$name = $prefix + [char]0xd800 + ' end'
$expectedName = $prefix + [char]0xfffd + ' end'
$firstGame = $gameConstructor.Invoke([object[]]@('11111111-1111-4111-8111-111111111111', $name, $true, 'Source', 'PC'))
$secondGame = $gameConstructor.Invoke([object[]]@('22222222-2222-4222-8222-222222222222', 'Second', $false, $null, $null))
$listType = [type]::GetType('System.Collections.Generic.List`1').MakeGenericType($gameType)
$games = [Activator]::CreateInstance($listType)
[void]$games.Add($firstGame)
[void]$games.Add($secondGame)

$timestamp = [DateTimeOffset]::Parse('2026-10-30T00:00:00Z', [Globalization.CultureInfo]::InvariantCulture)
$snapshotArguments = [object[]]::new(2)
$snapshotArguments[0] = $timestamp
$snapshotArguments[1] = $games
$snapshot = $snapshotConstructor.Invoke($snapshotArguments)

$directory = Join-Path ([IO.Path]::GetTempPath()) ('PlayniteCmdPal.NetFxSmoke.' + [guid]::NewGuid().ToString('N'))
$path = Join-Path $directory 'catalog-v1.json'
try {
    $arguments = [object[]]@($path, $snapshot, [Threading.CancellationToken]::None)
    # Exercise both first creation and atomic replacement through the actual
    # net462 publisher, which was the failing entry point in Playnite.
    $publisher.Invoke($null, $arguments).GetAwaiter().GetResult()
    $firstPublication = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($path)) | ConvertFrom-Json
    if ($firstPublication.games[1].name -ne 'Second') { throw 'First publication was invalid.' }

    $games[1] = $gameConstructor.Invoke([object[]]@('22222222-2222-4222-8222-222222222222', 'Second updated', $false, $null, $null))
    $snapshotArguments[1] = $games
    $arguments[1] = $snapshotConstructor.Invoke($snapshotArguments)
    $publisher.Invoke($null, $arguments).GetAwaiter().GetResult()
    $bytes = [IO.File]::ReadAllBytes($path)
    if ($bytes.Length -eq 0 -or $bytes[0] -ne [byte][char]'{') {
        throw 'Snapshot is empty or has an unexpected UTF-8 BOM.'
    }

    $jsonText = [Text.Encoding]::UTF8.GetString($bytes)
    if ($jsonText -notmatch '"generatedAtUtc"\s*:\s*"2026-10-30T00:00:00(\.0{1,7})?Z"') {
        throw 'Snapshot timestamp is not the expected UTC value.'
    }
    $json = $jsonText | ConvertFrom-Json
    if ($json.schemaVersion -ne 1 -or @($json.games).Count -ne 2) {
        throw 'Snapshot envelope is invalid.'
    }
    if ($json.games[0].id -ne '11111111-1111-4111-8111-111111111111' -or
        $json.games[0].name -cne $expectedName -or $json.games[0].isInstalled -ne $true -or
        $json.games[0].source -ne 'Source' -or $json.games[0].platform -ne 'PC') {
        throw 'First synthetic game did not round-trip.'
    }
    if ($json.games[1].id -ne '22222222-2222-4222-8222-222222222222' -or
        $json.games[1].name -ne 'Second updated' -or $json.games[1].isInstalled -ne $false -or
        $null -ne $json.games[1].PSObject.Properties['source'] -or
        $null -ne $json.games[1].PSObject.Properties['platform']) {
        throw 'Second synthetic game did not round-trip or optional fields were emitted.'
    }
    if (@($json.PSObject.Properties.Name).Count -ne 3 -or
        @($json.games[0].PSObject.Properties.Name).Count -ne 5 -or
        @($json.games[1].PSObject.Properties.Name).Count -ne 3) {
        throw 'The snapshot contained unexpected fields.'
    }
    if (@(Get-ChildItem -LiteralPath $directory -Force).Count -ne 1) {
        throw 'The publisher left an unexpected file behind.'
    }

    Write-Host 'Playnite bridge .NET Framework snapshot smoke passed (synthetic data, no System.Text.Json reference).'
} finally {
    if (Test-Path -LiteralPath $directory) {
        Remove-Item -LiteralPath $directory -Recurse -Force -ErrorAction SilentlyContinue
    }
}
