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
$gameConstructor = $gameType.GetConstructor([type[]]@([string], [string], [bool], [string], [string], [Nullable[DateTime]]))
$snapshotConstructor = @($snapshotType.GetConstructors())[0]

# Synthetic data exercises both the frozen v1 contract and enriched v2
# serialization without reading any Playnite library.
$prefix = 'Example "quoted" \\ path ' + [char]10 + [char]0x00e9 + [char]::ConvertFromUtf32(0x1f3ae)
$name = $prefix + [char]0xd800 + ' end'
$expectedName = $prefix + [char]0xfffd + ' end'
$lastActivity = [DateTime]::SpecifyKind(
    [DateTime]::Parse('2026-10-29T18:45:00', [Globalization.CultureInfo]::InvariantCulture),
    [DateTimeKind]::Unspecified)
$firstGame = $gameConstructor.Invoke([object[]]@('11111111-1111-4111-8111-111111111111', $name, $true, 'Source', 'PC', $lastActivity))
$secondGame = $gameConstructor.Invoke([object[]]@('22222222-2222-4222-8222-222222222222', 'Second', $false, $null, $null, $null))
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
$legacyPath = Join-Path $directory 'catalog-v1.json'
$enrichedPath = Join-Path $directory 'catalog-v2.json'
try {
    # Publish both contracts, as the production plugin does.
    $publisherType::PublishAsync([string]$legacyPath, $snapshot, [Threading.CancellationToken]::None).GetAwaiter().GetResult()
    $publisherType::PublishAsync([string]$enrichedPath, $snapshot, [Threading.CancellationToken]::None, 2, $true).GetAwaiter().GetResult()

    $legacyJson = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($legacyPath)) | ConvertFrom-Json
    $enrichedBytes = [IO.File]::ReadAllBytes($enrichedPath)
    if ($enrichedBytes.Length -eq 0 -or $enrichedBytes[0] -ne [byte][char]'{') {
        throw 'Enriched snapshot is empty or has an unexpected UTF-8 BOM.'
    }
    $enrichedJsonText = [Text.Encoding]::UTF8.GetString($enrichedBytes)
    $enrichedJson = $enrichedJsonText | ConvertFrom-Json

    if ($legacyJson.schemaVersion -ne 1 -or @($legacyJson.games).Count -ne 2) {
        throw 'Legacy v1 snapshot envelope is invalid.'
    }
    if ($enrichedJson.schemaVersion -ne 2 -or @($enrichedJson.games).Count -ne 2) {
        throw 'Enriched v2 snapshot envelope is invalid.'
    }
    if ($null -ne $legacyJson.games[0].PSObject.Properties['lastActivity'] -or
        $null -ne $legacyJson.games[1].PSObject.Properties['lastActivity']) {
        throw 'Frozen v1 contract unexpectedly contains lastActivity.'
    }
    if ($null -eq $enrichedJson.games[0].PSObject.Properties['lastActivity'] -or
        $null -ne $enrichedJson.games[1].PSObject.Properties['lastActivity'] -or
        $enrichedJsonText -notmatch '"lastActivity"\s*:\s*"2026-10-29T18:45:00\.0000000"') {
        throw 'V2 lastActivity did not round-trip with its Unspecified DateTime representation.'
    }

    # Replace both files and verify the v1 compatibility contract remains frozen
    # while v2 continues to carry the enriched property.
    $games[1] = $gameConstructor.Invoke([object[]]@('22222222-2222-4222-8222-222222222222', 'Second updated', $false, $null, $null, $null))
    $snapshotArguments[1] = $games
    $replacement = $snapshotConstructor.Invoke($snapshotArguments)
    $publisherType::PublishAsync([string]$legacyPath, $replacement, [Threading.CancellationToken]::None).GetAwaiter().GetResult()
    $publisherType::PublishAsync([string]$enrichedPath, $replacement, [Threading.CancellationToken]::None, 2, $true).GetAwaiter().GetResult()

    $legacyJson = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($legacyPath)) | ConvertFrom-Json
    $enrichedJsonText = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($enrichedPath))
    $enrichedJson = $enrichedJsonText | ConvertFrom-Json
    if ($legacyJson.schemaVersion -ne 1 -or $legacyJson.games[1].name -ne 'Second updated' -or
        $null -ne $legacyJson.games[0].PSObject.Properties['lastActivity']) {
        throw 'V1 replacement did not preserve the frozen legacy contract.'
    }
    if ($enrichedJson.schemaVersion -ne 2 -or $enrichedJson.games[1].name -ne 'Second updated' -or
        $null -eq $enrichedJson.games[0].PSObject.Properties['lastActivity'] -or
        $enrichedJsonText -notmatch '"lastActivity"\s*:\s*"2026-10-29T18:45:00\.0000000"') {
        throw 'V2 replacement did not preserve the enriched lastActivity field.'
    }
    if (@(Get-ChildItem -LiteralPath $directory -Force).Count -ne 2) {
        throw 'The publisher left an unexpected temporary file behind.'
    }

    Write-Host 'Playnite bridge .NET Framework v1/v2 snapshot smoke passed (synthetic data, no System.Text.Json reference).'
} finally {
    if (Test-Path -LiteralPath $directory) {
        Remove-Item -LiteralPath $directory -Recurse -Force -ErrorAction SilentlyContinue
    }
}
