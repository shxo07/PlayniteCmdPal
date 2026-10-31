[CmdletBinding()]
param(
    [string]$Fork = 'shxo07/PlayniteCmdPal',
    [string]$Upstream = 'werdscopez7347/PlayniteCmdPal',
    [switch]$Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Git {
    param([string[]]$Arguments)
    $output = & git @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
    return $output
}

$inside = Invoke-Git @('rev-parse', '--is-inside-work-tree')
if ($inside -ne 'true') { throw 'Run this script from a clone of your fork.' }

$originUrl = (Invoke-Git @('remote', 'get-url', 'origin')).Trim()
if ($originUrl -notmatch [regex]::Escape($Fork)) {
    throw "origin points to '$originUrl', not the expected fork '$Fork'."
}

$upstreamUrl = "https://github.com/$Upstream.git"
$remotes = Invoke-Git @('remote')
if ($remotes -notcontains 'upstream') {
    if ($Apply) { Invoke-Git @('remote', 'add', 'upstream', $upstreamUrl) | Out-Null }
    else { Write-Host "Would add upstream: $upstreamUrl" }
} elseif ($Apply) {
    $existing = (Invoke-Git @('remote', 'get-url', 'upstream')).Trim()
    if ($existing -ne $upstreamUrl) { throw "upstream already points to '$existing'." }
}

if ($Apply) {
    Invoke-Git @('fetch', 'upstream', '--prune') | Out-Null
    Invoke-Git @('fetch', 'origin', '--prune') | Out-Null
} else {
    Write-Host 'Dry run only. Re-run with -Apply to fetch and create missing fork branches.'
}

$upstreamRefs = Invoke-Git @('ls-remote', '--heads', $upstreamUrl)
$forkRefs = Invoke-Git @('ls-remote', '--heads', "https://github.com/$Fork.git")
$forkBranches = @{}
foreach ($line in $forkRefs) {
    if ($line -match '^([0-9a-f]+)\s+refs/heads/(.+)$') { $forkBranches[$Matches[2]] = $Matches[1] }
}

$created = 0; $skipped = 0; $diverged = 0
foreach ($line in $upstreamRefs) {
    if ($line -notmatch '^([0-9a-f]+)\s+refs/heads/(.+)$') { continue }
    $sha = $Matches[1]; $branch = $Matches[2]
    if ($forkBranches.ContainsKey($branch)) {
        if ($forkBranches[$branch] -eq $sha) {
            Write-Host "Already current: $branch"
            $skipped++
        } else {
            Write-Warning "Diverged; leaving fork branch untouched: $branch"
            $diverged++
        }
        continue
    }

    if ($Apply) {
        # Non-force push of a fetched upstream commit. Never checks out or changes your worktree.
        Invoke-Git @('push', 'origin', "${sha}:refs/heads/$branch") | Out-Null
        Write-Host "Created: $branch"
    } else {
        Write-Host "Would create: $branch -> $sha"
    }
    $created++
}

Write-Host "Summary: $created branch(es) to create, $skipped already current, $diverged diverged branch(es) left untouched."
if (-not $Apply) { Write-Host 'No remote branches were changed.' }
