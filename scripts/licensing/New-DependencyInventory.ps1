<#
.SYNOPSIS
    Generates a reproducible dependency inventory for the projects that produce
    PlayniteCmdPal release artifacts.

.DESCRIPTION
    Reads the NuGet restore output (obj/project.assets.json) of the shipped projects and
    their target frameworks, then enriches each resolved package with the license
    metadata declared by its .nuspec in the NuGet global-packages folder. Packages are
    classified by how they reach the build: runtime components copied into a release
    artifact, build-only tooling, test-only, or developer tooling.

    The output is a JSON artifact for the licensing audit documented in
    docs/LICENSING-AND-RELEASE.md. The artifact records what was resolved, not a legal
    conclusion; see docs/LICENSING-AND-RELEASE.md for the assessment and for the release
    checklist that consumes it.

    Run this from a Windows machine with the .NET 10 SDK and network access to nuget.org:

        pwsh -File scripts/licensing/New-DependencyInventory.ps1

    Restore the solution first (this script restores by default). Add -FailOnReview to
    make the run fail (exit code 3) when any resolved package has a missing, ambiguous,
    non-allowlisted or acceptance-required license.

.NOTES
    The script does not build, publish, or inspect a publish directory; use the generated
    inventory together with an inspection of the actual publish/MSIX contents, because
    trimming, single-file publishing and explicit ExcludeAssets change what ships.
#>
[CmdletBinding()]
param (
    # Configuration restored by this script. Restore is configuration-agnostic, but the
    # value is recorded so the inventory can be tied to a release build.
    [string] $Configuration = 'Release',

    # Output path, relative to the repository root unless rooted.
    [string] $OutputPath = 'artifacts/dependency-inventory.json',

    # Reuse the existing obj/project.assets.json files instead of restoring.
    [switch] $SkipRestore,

    # Exit with code 3 when any resolved package requires licensing review.
    [switch] $FailOnReview,

    # NuGet global-packages folder. Defaults to NUGET_PACKAGES or ~/.nuget/packages.
    [string] $NuGetPackagesPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$solutionPath = Join-Path $repoRoot 'PlayniteCmdPal.sln'

# Licenses that this project has assessed as compatible with MIT distribution. Anything
# else is reported for review rather than assumed acceptable.
$allowedLicenseExpressions = @('MIT', 'Apache-2.0', 'BSD-2-Clause', 'BSD-3-Clause', 'ISC', 'MS-PL', 'Zlib')

# Projects that produce, or are intended to produce, a release artifact. Test and
# developer-tool projects are inventoried too so that build-time dependencies are not
# mistaken for redistributed ones.
$targetProjects = @(
    [ordered]@{
        Path     = 'PlayniteCmdPal/PlayniteCmdPal.csproj'
        Role     = 'cmdpal-extension'
        Shipped  = $true
        Artifact = 'MSIX (win-x64/win-arm64, self-contained)'
    },
    [ordered]@{
        Path     = 'PlayniteCmdPal.PlaynitePlugin/PlayniteCmdPal.PlaynitePlugin.csproj'
        Role     = 'playnite-plugin'
        Shipped  = $true
        Artifact = '.pext (net462)'
    },
    [ordered]@{
        Path     = 'PlayniteCmdPal.Core.Tests/PlayniteCmdPal.Core.Tests.csproj'
        Role     = 'test'
        Shipped  = $false
        Artifact = $null
    },
    [ordered]@{
        Path     = 'PlayniteCmdPal.Playnite.Tests/PlayniteCmdPal.Playnite.Tests.csproj'
        Role     = 'test'
        Shipped  = $false
        Artifact = $null
    },
    [ordered]@{
        Path     = 'tools/CatalogVerifier/CatalogVerifier.csproj'
        Role     = 'developer-tool'
        Shipped  = $false
        Artifact = $null
    }
)

# Classification precedence: a package used in more than one place takes its most
# distribution-relevant classification.
$classificationRank = @{
    'shipped-runtime' = 4
    'shipped-build'   = 3
    'test-only'       = 2
    'developer-tool'  = 1
    'unclassified'    = 0
}

function Write-Step {
    param ([Parameter(Mandatory)][string] $Message)
    Write-Host "==> $Message"
}

function Get-MetadataValue {
    param (
        [Parameter(Mandatory)] $MetadataNode,
        [Parameter(Mandatory)][string] $Name
    )

    $node = $MetadataNode.SelectSingleNode("*[local-name()='$Name']")
    if ($null -eq $node) { return $null }
    $value = $node.InnerText
    if ([string]::IsNullOrWhiteSpace($value)) { return $null }
    return $value.Trim()
}

function Get-NuspecMetadata {
    param (
        [Parameter(Mandatory)][string] $PackagesRoot,
        [Parameter(Mandatory)][string] $Id,
        [Parameter(Mandatory)][string] $Version
    )

    $result = [ordered]@{
        id                       = $Id
        version                  = $Version
        licenseType              = $null
        licenseExpression        = $null
        licenseFile              = $null
        licenseUrl               = $null
        authors                  = $null
        copyright                = $null
        projectUrl               = $null
        requireLicenseAcceptance = $false
        metadataSource           = $null
        metadataError            = $null
    }

    $packageFolder = Join-Path (Join-Path $PackagesRoot $Id.ToLowerInvariant()) $Version
    if (-not (Test-Path -LiteralPath $packageFolder)) {
        $result.metadataError = "Package folder not found: $packageFolder"
        return $result
    }

    $nuspec = Get-ChildItem -LiteralPath $packageFolder -Filter '*.nuspec' -File -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -eq $nuspec) {
        $result.metadataError = "No .nuspec found in $packageFolder"
        return $result
    }

    try {
        $document = New-Object System.Xml.XmlDocument
        $document.PreserveWhitespace = $false
        $document.Load($nuspec.FullName)
    }
    catch {
        $result.metadataError = "Could not parse $($nuspec.FullName): $($_.Exception.Message)"
        return $result
    }

    $metadataNode = $document.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']")
    if ($null -eq $metadataNode) {
        $result.metadataError = "No package/metadata element in $($nuspec.FullName)"
        return $result
    }

    $licenseNode = $metadataNode.SelectSingleNode("*[local-name()='license']")
    if ($null -ne $licenseNode) {
        $licenseType = $licenseNode.GetAttribute('type')
        $licenseValue = $licenseNode.InnerText
        if (-not [string]::IsNullOrWhiteSpace($licenseType)) {
            $result.licenseType = $licenseType.Trim().ToLowerInvariant()
        }
        if (-not [string]::IsNullOrWhiteSpace($licenseValue)) {
            switch ($result.licenseType) {
                'expression' { $result.licenseExpression = $licenseValue.Trim() }
                'file' { $result.licenseFile = $licenseValue.Trim() }
            }
        }
    }

    $result.licenseUrl = Get-MetadataValue -MetadataNode $metadataNode -Name 'licenseUrl'
    $result.authors = Get-MetadataValue -MetadataNode $metadataNode -Name 'authors'
    $result.copyright = Get-MetadataValue -MetadataNode $metadataNode -Name 'copyright'
    $result.projectUrl = Get-MetadataValue -MetadataNode $metadataNode -Name 'projectUrl'

    $acceptance = Get-MetadataValue -MetadataNode $metadataNode -Name 'requireLicenseAcceptance'
    if ($null -ne $acceptance) {
        $result.requireLicenseAcceptance = ($acceptance -eq 'true')
    }

    $result.metadataSource = (ConvertTo-PackagesRelativePath -PackagesRoot $PackagesRoot -Path $nuspec.FullName)
    return $result
}

function ConvertTo-PackagesRelativePath {
    param (
        [Parameter(Mandatory)][string] $PackagesRoot,
        [Parameter(Mandatory)][string] $Path
    )

    $root = $PackagesRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if ($Path.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
        return ($Path.Substring($root.Length) -replace '\\', '/')
    }
    return (Split-Path $Path -Leaf)
}

function ConvertTo-RepoRelativePath {
    param ([Parameter(Mandatory)][string] $Path)

    $full = [System.IO.Path]::GetFullPath($Path)
    $root = $repoRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if ($full.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
        return ($full.Substring($root.Length) -replace '\\', '/')
    }
    return ($full -replace '\\', '/')
}

if (-not (Test-Path -LiteralPath $solutionPath)) {
    [Console]::Error.WriteLine("ERROR: Solution not found at $solutionPath.")
    exit 1
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    [Console]::Error.WriteLine('ERROR: The .NET 10 SDK is required, but dotnet was not found on PATH.')
    exit 127
}

if ([string]::IsNullOrWhiteSpace($NuGetPackagesPath)) {
    if (-not [string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) {
        $NuGetPackagesPath = $env:NUGET_PACKAGES
    }
    else {
        $NuGetPackagesPath = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages'
    }
}
$NuGetPackagesPath = [System.IO.Path]::GetFullPath($NuGetPackagesPath)
if (-not (Test-Path -LiteralPath $NuGetPackagesPath)) {
    [Console]::Error.WriteLine("ERROR: NuGet global-packages folder not found at $NuGetPackagesPath.")
    exit 1
}

if (-not $SkipRestore) {
    Write-Step "Restoring $solutionPath"
    & dotnet restore $solutionPath
    if ($LASTEXITCODE -ne 0) {
        [Console]::Error.WriteLine("ERROR: dotnet restore failed with exit code $LASTEXITCODE.")
        exit $LASTEXITCODE
    }
}

$sdkVersion = "$(& dotnet --version 2>$null)".Trim()
$projectRecords = New-Object System.Collections.Generic.List[object]
$packageRecords = @{}
$reviewRequired = New-Object System.Collections.Generic.List[string]

foreach ($target in $targetProjects) {
    $projectPath = Join-Path $repoRoot $target.Path
    if (-not (Test-Path -LiteralPath $projectPath)) {
        [Console]::Error.WriteLine("ERROR: Project not found: $($target.Path)")
        exit 1
    }

    [xml] $projectXml = Get-Content -LiteralPath $projectPath -Raw
    $directPackageIds = @(
        $projectXml.SelectNodes("//*[local-name()='PackageReference']") |
            ForEach-Object { $_.GetAttribute('Include') } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    )

    $assetsPath = Join-Path (Split-Path $projectPath -Parent) 'obj/project.assets.json'
    if (-not (Test-Path -LiteralPath $assetsPath)) {
        [Console]::Error.WriteLine("ERROR: Restore output not found at $($target.Path)/obj/project.assets.json. Run the script without -SkipRestore.")
        exit 1
    }

    $assets = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json
    $targetKeys = @($assets.targets.PSObject.Properties.Name)

    # Prefer the framework-only target for the package graph; RID-specific targets are
    # still scanned because self-contained runtime packs only appear there.
    $targetsByFramework = @{}
    foreach ($key in $targetKeys) {
        if ($key -match '^(?<framework>[^/]+)(/(?<rid>.+))?$') {
            $framework = $Matches['framework']
            $rid = if ($Matches.ContainsKey('rid')) { $Matches['rid'] } else { $null }
            if (-not $targetsByFramework.ContainsKey($framework)) {
                $targetsByFramework[$framework] = New-Object System.Collections.Generic.List[object]
            }
            $targetsByFramework[$framework].Add([ordered]@{
                    Key  = $key
                    Rid  = $rid
                })
        }
    }

    $projectFrameworks = New-Object System.Collections.Generic.List[object]
    foreach ($framework in ($targetsByFramework.Keys | Sort-Object)) {
        $frameworkTargets = $targetsByFramework[$framework]
        $frameworkPackages = New-Object System.Collections.Generic.List[object]
        $frameworkRids = New-Object System.Collections.Generic.List[string]

        foreach ($frameworkTarget in $frameworkTargets) {
            if ($null -ne $frameworkTarget.Rid) {
                if (-not $frameworkRids.Contains($frameworkTarget.Rid)) { $frameworkRids.Add($frameworkTarget.Rid) }
            }

            $targetNode = $assets.targets.PSObject.Properties[$frameworkTarget.Key].Value
            foreach ($packageProperty in $targetNode.PSObject.Properties) {
                $packageKey = $packageProperty.Name
                if ($packageKey -notmatch '^(?<id>[^/]+)/(?<version>.+)$') { continue }
                $packageId = $Matches['id']
                $packageVersion = $Matches['version']
                $library = $packageProperty.Value

                $assetKinds = New-Object System.Collections.Generic.List[string]
                foreach ($kind in @('compile', 'runtime', 'native', 'runtimeTargets', 'build', 'buildMultiTargeting', 'contentFiles', 'frameworkAssemblies')) {
                    $kindProperty = $library.PSObject.Properties[$kind]
                    if ($null -ne $kindProperty) { $assetKinds.Add($kind) }
                }

                $hasRuntimeAssets = @('runtime', 'native', 'runtimeTargets') |
                    Where-Object { $assetKinds.Contains($_) } |
                    Measure-Object |
                    Select-Object -ExpandProperty Count
                $hasRuntimeAssets = ($hasRuntimeAssets -gt 0)

                $recordKey = "$packageId/$packageVersion"
                if (-not $packageRecords.ContainsKey($recordKey)) {
                    $packageRecords[$recordKey] = [ordered]@{
                        id                = $packageId
                        version           = $packageVersion
                        classification    = 'unclassified'
                        direct            = $false
                        runtimeAssets     = $false
                        assetKinds        = (New-Object System.Collections.Generic.List[string])
                        usedBy            = (New-Object System.Collections.Generic.List[object])
                        license           = (Get-NuspecMetadata -PackagesRoot $NuGetPackagesPath -Id $packageId -Version $packageVersion)
                        reviewStatus      = 'ok'
                        reviewNotes       = (New-Object System.Collections.Generic.List[string])
                    }
                }

                $record = $packageRecords[$recordKey]
                foreach ($kind in $assetKinds) {
                    if (-not $record.assetKinds.Contains($kind)) { $record.assetKinds.Add($kind) }
                }
                if ($hasRuntimeAssets) { $record.runtimeAssets = $true }
                if ($directPackageIds -contains $packageId) { $record.direct = $true }

                if ($target.Shipped) {
                    $classification = if ($hasRuntimeAssets) { 'shipped-runtime' } else { 'shipped-build' }
                }
                else {
                    $classification = if ($target.Role -eq 'test') { 'test-only' } else { 'developer-tool' }
                }

                if ($classificationRank[$classification] -gt $classificationRank[$record.classification]) {
                    $record.classification = $classification
                }

                $record.usedBy.Add([ordered]@{
                        project        = $target.Path
                        role           = $target.Role
                        framework      = $framework
                        rid            = $frameworkTarget.Rid
                        classification = $classification
                        assetKinds     = @($assetKinds | Sort-Object)
                    })

                if (-not $frameworkPackages.Contains($recordKey)) { $frameworkPackages.Add($recordKey) }
            }
        }

        $projectFrameworks.Add([ordered]@{
                framework = $framework
                rids      = @($frameworkRids | Sort-Object)
                packages  = @($frameworkPackages | Sort-Object)
            })
    }

    $projectRecords.Add([ordered]@{
            project           = $target.Path
            role              = $target.Role
            shipped           = $target.Shipped
            artifact          = $target.Artifact
            directPackages    = @($directPackageIds | Sort-Object)
            targetFrameworks  = @($projectFrameworks)
            restoreOutputPath = (ConvertTo-RepoRelativePath -Path $assetsPath)
        })
}

$orderedPackages = New-Object System.Collections.Generic.List[object]
foreach ($recordKey in ($packageRecords.Keys | Sort-Object)) {
    $record = $packageRecords[$recordKey]
    $license = $record.license

    $notes = $record.reviewNotes
    $requiresReview = $false

    if ($null -ne $license.metadataError) {
        $notes.Add("License metadata unavailable: $($license.metadataError)")
        $requiresReview = $true
    }
    elseif ([string]::IsNullOrWhiteSpace($license.licenseExpression) -and [string]::IsNullOrWhiteSpace($license.licenseFile)) {
        if ([string]::IsNullOrWhiteSpace($license.licenseUrl)) {
            $notes.Add('No license expression, license file, or license URL in the package metadata.')
        }
        else {
            $notes.Add("License is expressed only as a URL: $($license.licenseUrl). Confirm the actual terms.")
        }
        $requiresReview = $true
    }
    elseif (-not [string]::IsNullOrWhiteSpace($license.licenseExpression) -and
        ($allowedLicenseExpressions -notcontains $license.licenseExpression)) {
        $notes.Add("License expression '$($license.licenseExpression)' is not on the assessed list ($($allowedLicenseExpressions -join ', ')).")
        $requiresReview = $true
    }
    elseif (-not [string]::IsNullOrWhiteSpace($license.licenseFile)) {
        # A nuspec license-file path is not itself evidence that the file was read,
        # identified, or assessed. Never mark file-only metadata as approved.
        $notes.Add("Package declares a license file ('$($license.licenseFile)'); inspect the referenced file and record an explicit assessment before treating this package as cleared.")
        $requiresReview = $true
    }

    if ($license.requireLicenseAcceptance) {
        $notes.Add('Package sets requireLicenseAcceptance; confirm that the acceptance obligation is satisfied for redistribution.')
        $requiresReview = $true
    }

    if ($record.classification -eq 'shipped-runtime' -and -not [string]::IsNullOrWhiteSpace($license.licenseFile)) {
        $notes.Add("License is a file in the package ($($license.licenseFile)); reproduce the notice in the distribution.")
    }

    if ($requiresReview) {
        $record.reviewStatus = 'review'
        $reviewRequired.Add("$($record.id) $($record.version) [$($record.classification)]: $($notes -join ' ')" )
    }

    $orderedPackages.Add([ordered]@{
            id                = $record.id
            version           = $record.version
            classification    = $record.classification
            direct            = $record.direct
            runtimeAssets     = $record.runtimeAssets
            assetKinds        = @($record.assetKinds | Sort-Object)
            license           = $license
            reviewStatus      = $record.reviewStatus
            reviewNotes       = @($notes)
            usedBy            = @($record.usedBy)
        })
}

$summary = [ordered]@{}
foreach ($classification in @('shipped-runtime', 'shipped-build', 'test-only', 'developer-tool', 'unclassified')) {
    $summary[$classification] = @($orderedPackages | Where-Object { $_.classification -eq $classification }).Count
}
$summary['packagesRequiringReview'] = @($orderedPackages | Where-Object { $_.reviewStatus -eq 'review' }).Count
$summary['shippedPackagesRequiringReview'] = @(
    $orderedPackages |
        Where-Object { $_.reviewStatus -eq 'review' -and $_.classification -like 'shipped-*' }
).Count

$inventory = [ordered]@{
    schemaVersion       = 1
    generatedBy         = 'scripts/licensing/New-DependencyInventory.ps1'
    generatedAtUtc      = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    configuration       = $Configuration
    dotnetSdkVersion    = if ([string]::IsNullOrWhiteSpace($sdkVersion)) { $null } else { $sdkVersion }
    nugetPackagesFolder = $NuGetPackagesPath
    allowedLicenses     = $allowedLicenseExpressions
    projects            = @($projectRecords)
    packages            = @($orderedPackages)
    summary             = $summary
    limitations         = @(
        'Reflects the resolved NuGet graph recorded in obj/project.assets.json; it does not inspect a published MSIX or .pext layout.',
        'Trimming, single-file publishing, ExcludeAssets, and implicit framework references (for example the .NET runtime pack) change what actually ships; cross-check the publish output.',
        'License values come from package metadata, not from a legal review; see docs/LICENSING-AND-RELEASE.md.'
    )
}

$json = $inventory | ConvertTo-Json -Depth 12
$json = ($json -replace "`r`n", "`n") + "`n"

if (-not [System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath = Join-Path $repoRoot $OutputPath
}
$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = Split-Path $outputFullPath -Parent
if (-not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}
[System.IO.File]::WriteAllText($outputFullPath, $json, (New-Object System.Text.UTF8Encoding($false)))

Write-Step "Wrote $outputFullPath"
Write-Host ("Packages: {0} shipped-runtime, {1} shipped-build, {2} test-only, {3} developer-tool, {4} unclassified" -f `
        $summary['shipped-runtime'], $summary['shipped-build'], $summary['test-only'], $summary['developer-tool'], $summary['unclassified'])
Write-Host ("Review required: {0} package(s), {1} of them shipped" -f $summary['packagesRequiringReview'], $summary['shippedPackagesRequiringReview'])
foreach ($item in $reviewRequired) {
    Write-Host "  - $item"
}

if ($summary['shippedPackagesRequiringReview'] -gt 0) {
    Write-Warning 'At least one redistributed package needs a licensing decision. Record it in docs/LICENSING-AND-RELEASE.md before releasing.'
}

if ($FailOnReview -and $summary['packagesRequiringReview'] -gt 0) {
    exit 3
}

exit 0
