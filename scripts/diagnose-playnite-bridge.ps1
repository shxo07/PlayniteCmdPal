[CmdletBinding()]
param ()

# Read-only diagnostic for the WERD-60 Playnite bridge. It prints paths, file names/sizes,
# add-on state, and matching logs (including exception details), but not catalog contents.
# Review log output for personal information before sharing it.

$ErrorActionPreference = 'Continue'
$manifestId = 'PlayniteCmdPalBridge_9E4CF37F-EF29-495F-9E83-4EA1A389ABD8'
$logPattern = 'PlayniteCmdPal|9E4CF37F|Failed to load plugin|Loaded plugin|Opening db |Failed to open library database|Failed to execute OnApplicationStarted|catalog export|System\.Text\.Json|System\.Memory|System\.Runtime\.CompilerServices\.Unsafe|Microsoft\.Bcl\.AsyncInterfaces|System\.Threading\.Tasks\.Extensions|System\.IO\.Pipelines|System\.Text\.Encodings\.Web|FileNotFoundException|FileLoadException|BadImageFormatException|TypeLoadException|MissingMethodException'

$roots = New-Object 'System.Collections.Generic.List[string]'

function Add-Root {
    param ([string] $Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return
    }

    try {
        $full = [System.IO.Path]::GetFullPath($Path)
    } catch {
        return
    }

    if ((Test-Path -LiteralPath $full -PathType Container) -and (-not $roots.Contains($full))) {
        $roots.Add($full)
    }
}

function Redact {
    param ([string] $Text)

    if ($null -eq $Text) {
        return ''
    }

    $result = $Text
    if ($env:USERPROFILE) {
        $result = $result.Replace($env:USERPROFILE, '<profile>')
    }
    if ($env:USERNAME) {
        $result = $result.Replace($env:USERNAME, '<user>')
    }
    return $result
}

function Write-Line {
    param ([string] $Text = '')
    Write-Output (Redact $Text)
}

Add-Root (Join-Path $env:APPDATA 'Playnite')
Add-Root (Join-Path $env:LOCALAPPDATA 'Playnite')

$processNames = @('Playnite.DesktopApp', 'Playnite.FullscreenApp')
foreach ($processName in $processNames) {
    $process = Get-Process -Name $processName -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $process) {
        continue
    }

    try {
        $exe = $process.Path
        if ($exe) {
            Add-Root (Split-Path -Parent $exe)
            Write-Line "Playnite process: $processName"
            Write-Line "Playnite exe: $exe"
            try {
                Write-Line ('Playnite file version: ' + $process.MainModule.FileVersionInfo.ProductVersion)
            } catch {
                Write-Line 'Playnite file version: unavailable'
            }
        }
    } catch {
        Write-Line "Playnite process path unavailable for $processName"
    }
}

try {
    $cimProcesses = Get-CimInstance Win32_Process -Filter "Name='Playnite.DesktopApp.exe' OR Name='Playnite.FullscreenApp.exe'" -ErrorAction Stop
    foreach ($cimProcess in $cimProcesses) {
        if (-not $cimProcess.CommandLine) {
            continue
        }

        $match = [regex]::Match($cimProcess.CommandLine, '--userdatadir\s+(?:"([^"]+)"|(\S+))')
        if ($match.Success) {
            $userDataDir = $match.Groups[1].Value
            if (-not $userDataDir) {
                $userDataDir = $match.Groups[2].Value
            }
            if ($userDataDir) {
                Add-Root $userDataDir
                Write-Line "Custom Playnite user data dir: $userDataDir"
            }
        }
    }
} catch {
    Write-Line 'Could not inspect Playnite process command line for --userdatadir.'
}

$programFilesX86 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
if ($programFilesX86) {
    Add-Root (Join-Path $programFilesX86 'Playnite')
}
if ($env:ProgramFiles) {
    Add-Root (Join-Path $env:ProgramFiles 'Playnite')
}

Write-Line ''
Write-Line 'Candidate Playnite data roots:'
if ($roots.Count -eq 0) {
    Write-Line '  none found'
} else {
    foreach ($root in $roots) {
        Write-Line "  $root"
    }
}

$bridgeDir = $null
foreach ($root in $roots) {
    $extensionsRoot = Join-Path $root 'Extensions'
    if (-not (Test-Path -LiteralPath $extensionsRoot -PathType Container)) {
        continue
    }

    $namedDirs = Get-ChildItem -LiteralPath $extensionsRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like 'PlayniteCmdPalBridge*' }

    foreach ($dir in $namedDirs) {
        if (Test-Path -LiteralPath (Join-Path $dir.FullName 'extension.yaml')) {
            $bridgeDir = $dir.FullName
            break
        }
    }

    if (-not $bridgeDir) {
        $manifests = Get-ChildItem -LiteralPath $extensionsRoot -Filter 'extension.yaml' -Recurse -ErrorAction SilentlyContinue |
            Where-Object {
                $content = Get-Content -LiteralPath $_.FullName -Raw -ErrorAction SilentlyContinue
                $content -and ($content -match [regex]::Escape($manifestId))
            } |
            Select-Object -First 1

        if ($manifests) {
            $bridgeDir = Split-Path -Parent $manifests.FullName
        }
    }

    if ($bridgeDir) {
        break
    }
}

Write-Line ''
if ($bridgeDir) {
    Write-Line "Bridge install dir: $bridgeDir"
    $files = Get-ChildItem -LiteralPath $bridgeDir -File -ErrorAction SilentlyContinue |
        Select-Object Name, Length |
        Format-Table -AutoSize |
        Out-String
    Write-Output $files

    $manifestPath = Join-Path $bridgeDir 'extension.yaml'
    if (Test-Path -LiteralPath $manifestPath) {
        Write-Line 'extension.yaml:'
        Get-Content -LiteralPath $manifestPath -ErrorAction SilentlyContinue | ForEach-Object { Write-Line $_ }
    }

    $requiredFiles = @(
        'extension.yaml',
        'PlayniteCmdPal.PlaynitePlugin.dll',
        'PlayniteCmdPal.Snapshot.dll'
    )
    foreach ($requiredFile in $requiredFiles) {
        $requiredPath = Join-Path $bridgeDir $requiredFile
        Write-Line ("Required file {0}: {1}" -f $requiredFile, (Test-Path -LiteralPath $requiredPath))
    }

    $assemblyFiles = @(
        'PlayniteCmdPal.PlaynitePlugin.dll',
        'PlayniteCmdPal.Snapshot.dll',
        'System.Text.Json.dll',
        'System.Memory.dll',
        'System.Runtime.CompilerServices.Unsafe.dll',
        'System.Buffers.dll',
        'System.Threading.Tasks.Extensions.dll',
        'Microsoft.Bcl.AsyncInterfaces.dll',
        'System.Text.Encodings.Web.dll'
    )
    Write-Line 'Bridge assembly versions (legacy System.* dependencies may be absent):'
    foreach ($assemblyFile in $assemblyFiles) {
        $assemblyPath = Join-Path $bridgeDir $assemblyFile
        if (Test-Path -LiteralPath $assemblyPath) {
            try {
                $assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName($assemblyPath)
                Write-Line ("  {0}: {1}" -f $assemblyFile, $assemblyName.Version)
            } catch {
                Write-Line ("  {0}: version unavailable" -f $assemblyFile)
            }
        } else {
            Write-Line ("  {0}: missing" -f $assemblyFile)
        }
    }
} else {
    Write-Line 'Bridge install dir: NOT FOUND in candidate roots.'
}

Write-Line ''
foreach ($root in $roots) {
    $configPath = Join-Path $root 'config.json'
    if (Test-Path -LiteralPath $configPath) {
        try {
            $config = Get-Content -LiteralPath $configPath -Raw -ErrorAction Stop | ConvertFrom-Json
            $disabledProperty = $config.PSObject.Properties['DisabledPlugins']
            $disabled = $false
            if ($disabledProperty -and $disabledProperty.Value) {
                $disabled = @($disabledProperty.Value) -contains $manifestId
            }
            Write-Line "$configPath"
            Write-Line "  DisabledPlugins contains bridge: $disabled"
        } catch {
            Write-Line "$configPath could not be parsed."
        }
    }

    $safeStartFlag = Join-Path $root 'safestart.flag'
    if (Test-Path -LiteralPath $safeStartFlag) {
        Write-Line "Safe startup flag present: $safeStartFlag"
    }
}

$snapshotDir = Join-Path $env:LOCALAPPDATA 'PlayniteCmdPal'
$snapshotPath = Join-Path $snapshotDir 'catalog-v1.json'
Write-Line ''
Write-Line "Snapshot path: $snapshotPath"
Write-Line ('Snapshot exists: ' + (Test-Path -LiteralPath $snapshotPath))
Write-Line ('Snapshot directory exists: ' + (Test-Path -LiteralPath $snapshotDir -PathType Container))

if (Test-Path -LiteralPath $snapshotPath) {
    $snapshotItem = Get-Item -LiteralPath $snapshotPath
    Write-Line ('Snapshot length: ' + $snapshotItem.Length)
    Write-Line ('Snapshot last write UTC: ' + $snapshotItem.LastWriteTimeUtc.ToString('o'))

    try {
        $snapshot = Get-Content -LiteralPath $snapshotPath -Raw -ErrorAction Stop | ConvertFrom-Json
        $schemaProperty = $snapshot.PSObject.Properties['schemaVersion']
        $timestampProperty = $snapshot.PSObject.Properties['generatedAtUtc']
        $gamesProperty = $snapshot.PSObject.Properties['games']
        Write-Line ('schemaVersion: ' + $(if ($schemaProperty) { $schemaProperty.Value } else { '<missing>' }))
        Write-Line ('generatedAtUtc: ' + $(if ($timestampProperty) { $timestampProperty.Value } else { '<missing>' }))
        Write-Line ('gameCount: ' + $(if ($gamesProperty) { @($gamesProperty.Value).Count } else { '<missing>' }))
    } catch {
        Write-Line 'Snapshot JSON could not be parsed.'
    }
} else {
    $tempFiles = Get-ChildItem -LiteralPath $snapshotDir -Force -ErrorAction SilentlyContinue |
        Select-Object Name, Length |
        Format-Table -AutoSize |
        Out-String
    Write-Output $tempFiles
}

$logs = foreach ($root in $roots) {
    $logPath = Join-Path $root 'playnite.log'
    if (Test-Path -LiteralPath $logPath) {
        Get-Item -LiteralPath $logPath
    }
}

$latestLog = $logs | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
Write-Line ''
if ($latestLog) {
    Write-Line "Newest Playnite log: $($latestLog.FullName)"
    $logMatches = Select-String -LiteralPath $latestLog.FullName -Pattern $logPattern -ErrorAction SilentlyContinue |
        Select-Object -Last 120

    if ($logMatches) {
        foreach ($logMatch in $logMatches) {
            Write-Line $logMatch.Line
        }
    } else {
        Write-Line 'No matching bridge/assembly log lines found.'
    }
} else {
    Write-Line 'No playnite.log found in candidate roots.'
}

# Playnite routes extension loggers (names containing #) to extensions.log, not
# playnite.log. Preserve exception/stack lines belonging to our logger without
# printing unrelated extensions' entries.
$extensionLogs = foreach ($root in $roots) {
    $extensionLogPath = Join-Path $root 'extensions.log'
    if (Test-Path -LiteralPath $extensionLogPath -PathType Leaf) {
        Get-Item -LiteralPath $extensionLogPath
    }
}

$latestExtensionLog = $null
if ($latestLog) {
    # Prefer the extensions log next to the active host log over an unrelated
    # installation whose extensions log happened to be modified more recently.
    $pairedPath = Join-Path $latestLog.DirectoryName 'extensions.log'
    $latestExtensionLog = $extensionLogs | Where-Object { $_.FullName -eq $pairedPath } | Select-Object -First 1
}
if (-not $latestExtensionLog) {
    $latestExtensionLog = $extensionLogs | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
}
Write-Line ''
if ($latestExtensionLog) {
    Write-Line "Newest Playnite extensions log: $($latestExtensionLog.FullName)"
    try {
        $text = Get-Content -LiteralPath $latestExtensionLog.FullName -Raw -ErrorAction Stop
        $entries = @()
        if (-not [string]::IsNullOrEmpty($text)) {
            $entryStart = '(?m)(?=^\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}\|)'
            $bridgeLogger = '(?m)^\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}\|[^|]*\|PlayniteCmdPal\.PlaynitePlugin#[^:\r\n]*:'
            $entries = @([regex]::Split($text, $entryStart) |
                Where-Object { $_ -match $bridgeLogger } |
                Select-Object -Last 5)
        }

        if ($entries.Count -gt 0) {
            foreach ($entry in $entries) {
                Write-Line ($entry.TrimEnd())
            }
        } else {
            Write-Line 'No bridge entries found in extensions.log.'
        }
    } catch {
        Write-Line 'Could not read or parse extensions.log.'
    }
} else {
    Write-Line 'No extensions.log found in candidate roots.'
}
