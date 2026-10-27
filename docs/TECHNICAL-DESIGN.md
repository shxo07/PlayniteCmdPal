# Technical Design

**Status:** Implementation plan; generated template committed, build/deploy and Playnite integration still to be verified.  
**Last reviewed:** 2026-10-26.

## Current generated-template baseline

The current repository contains `testExtension.sln` and `testExtension/testExtension.csproj`. Do not regenerate the template.

Observed from checked-in project files:
- Target framework: `net10.0-windows10.0.26100.0`.
- Minimum/supported Windows platform: `10.0.19041.0`.
- Windows SDK package property: `10.0.26100.68-preview`.
- Runtime identifiers: `win-x64;win-arm64`.
- Build platforms: `x64;ARM64`.
- Central package management is enabled.
- `Microsoft.CommandPalette.Extensions` 0.9.260303001.
- `Microsoft.Windows.CsWinRT` 2.2.0.
- `Shmuelie.WinRTServer` 2.1.1.
- `Microsoft.Windows.SDK.BuildTools.MSIX` 1.7.20250829.1.
- Additional versions are pinned in `Directory.Packages.props`.

Sources: [project](../testExtension/testExtension.csproj), [package versions](../Directory.Packages.props), [shared build properties](../Directory.Build.props). These values describe the checked-in template; they do not prove that a clean build or local deployment succeeds.

## Stack and framework

- C# extension using the current official Microsoft Command Palette extension template.
- `Microsoft.CommandPalette.Extensions` is referenced directly. Use the exact generated project pattern and consult the current Microsoft guide before implementing providers, pages, result actions, or navigation.
- Keep the generated COM server/hosting, manifest, and MSIX packaging structure unless the official docs show a required change.
- Keep the Windows GitHub Actions build workflow aligned with the actual SDK/framework requirements established by local validation.
- Do not guess or casually upgrade template dependencies during MVP implementation.

References:
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extension-gallery
- https://github.com/microsoft/PowerToys/tree/main/src/modules/cmdpal

## Architecture and boundaries

1. **CmdPal presentation:** current-template command provider, search/results, actions, and clear unavailable/error states.
2. **Core:** game model, query normalization, deterministic ranking, and interfaces. No CmdPal or Playnite SDK types.
3. **Catalog adapter:** maps the selected read-only catalog transport into the core model.
4. **Playnite bridge/transport:** companion Playnite plugin exports a normalized JSON snapshot; CmdPal reads it read-only.

Playnite remains the source of truth for game identity, library metadata, and launch configuration. The extension must not independently recreate storefront launch logic.

## Search and model

- Preserve Playnite's stable game ID; never use the display title as identity.
- Match case-insensitively.
- Deterministic rank order: exact match, prefix match, word-prefix match, substring match; define stable tie-breaks.
- Handle duplicate display names and empty query/catalog explicitly.
- Keep search logic independently unit-testable.

## Playnite launch integration

Playnite documents `--start <gameId>` and `playnite://playnite/start/<gameId>`, as well as `showgame` and `search` URI commands:
https://api.playnite.link/docs/manual/advanced/cmdlineArguments.html

Use a documented URI/CLI request after one focused smoke test with a real game ID. The purpose of the test is to validate our chosen invocation and failure handling, not to re-prove that documented features exist. Do not launch the game's executable directly when Playnite should apply its configured launch behavior.

## Catalog integration decision

Playnite's plugin SDK exposes `PlayniteApi.Database.Games` to an in-process plugin. That does not by itself establish a general-purpose external-process catalog API:
- https://api.playnite.link/docs/api/Playnite.SDK.IPlayniteAPI.html
- https://api.playnite.link/docs/api/Playnite.SDK.IGameDatabase.html
- https://api.playnite.link/docs/tutorials/extensions/library.html

The catalog transport is selected by WERD-56: companion plugin plus atomic normalized JSON snapshot, using the contract below. Do not add named pipes or another IPC protocol unless implementation evidence proves the snapshot insufficient.

Rejected for production:
- Direct database writes.
- UI automation.
- Direct database parsing as the supported integration contract.
- Arbitrary shell commands or executable paths derived from untrusted catalog data.

## Proposed logical structure

Keep the generated project intact while the core boundary is introduced. Proposed code organization may be folders/projects in this solution or separate projects only when useful:
- Extension presentation/provider and result actions.
- Core game model and search.
- Catalog adapter and serialization contract.
- Unit/contract tests independent of Playnite.
- Manual local smoke tests for the actual Playnite launch.

The transport decision is made by WERD-56; this spike documents the contract but does not implement the companion plugin or reader.

## Security and reliability

- Treat catalog data as input; validate stable IDs and expected schema before launch.
- Never accept arbitrary shell commands over IPC.
- No network listener, telemetry, cloud dependency, or private game-library data in Git.
- Never write to Playnite's database.
- Distinguish missing catalog, stale catalog, malformed catalog, no matches, launch requested, and launch failure.
- Keep disk/network/process operations off the CmdPal UI thread.

## Testing and CI

- Core tests: ranking order, case normalization, duplicates, tie-breaks, empty query/catalog.
- Adapter/contract tests: valid, missing, malformed, stale, and empty catalog data.
- Manual integration: real Playnite library and launch-by-ID behavior.
- Normal CI must not require Playnite or personal library data.
- Windows CI builds the generated solution on pull requests and default-branch pushes; add test execution when test projects exist.

## Developer setup

See [DEVELOPER-SETUP.md](DEVELOPER-SETUP.md). Key loop:
1. Record `dotnet --info` and actual Visual Studio version/components.
2. Run `dotnet restore .\testExtension.sln` and `dotnet build .\testExtension.sln -c Debug`.
3. In Visual Studio use **Build > Deploy** to register the generated MSIX package.
4. Reload Command Palette extensions and debug using the generated launch settings.
5. Record the successful exact steps in this guide.

## Open decisions

- Validate the companion-plugin snapshot writer and CmdPal reader against the selected WERD-56 contract.
- Tested minimum PowerToys/Visual Studio/.NET SDK versions.
- Optional alias/fuzzy matching beyond title matching.
- Packaging and gallery submission route.
- Repository license.

## WERD-56: selected catalog adapter contract

Decision: a companion Playnite plugin exports a normalized JSON snapshot; the out-of-process CmdPal extension reads it read-only. The reviewed official references document IPlayniteAPI.Database and in-process library access, but do not document a general external-process catalog endpoint.

Evidence:
- IPlayniteAPI.Database: https://api.playnite.link/docs/api/Playnite.SDK.IPlayniteAPI.html
- Library enumeration and change events (ItemCollectionChanged, ItemUpdated, OnLibraryUpdated): https://api.playnite.link/docs/tutorials/extensions/library.html
- Launch URI/CLI: https://api.playnite.link/docs/manual/advanced/cmdlineArguments.html
- URI routing source: https://github.com/JosefNemec/Playnite/blob/master/source/Playnite/App/PlayniteApplication.cs

Snapshot v1 path: %LOCALAPPDATA%\PlayniteCmdPal\catalog-v1.json. This is per-user Local AppData, not the Playnite database directory. Preserve default user ACLs; no elevation. Example schema:

```json
{
  "schemaVersion": 1,
  "generatedAtUtc": "2026-10-27T12:00:00Z",
  "games": [{
    "id": "64f78cd1-0b00-4271-a9e1-89abc55f66cc",
    "name": "Example Game",
    "source": "Optional source",
    "platform": "Optional platform",
    "isInstalled": true
  }]
}
```

The id is the Playnite database GUID in canonical D format and sole identity key. `name` must be a non-empty string and `isInstalled` a Boolean; `source` and `platform` are optional strings. Treat titles as display/search data, never identity. Export only this allowlisted projection, never full SDK models, executable paths, configured actions, or private library data. Store the snapshot under the current user's Local AppData with inherited per-user permissions; do not elevate. CmdPal opens it read-only.

Writer refreshes on plugin initialization and after `OnLibraryUpdated`; also subscribe to game collection-change/update events for direct edits. Debounce bursts, serialize writes, and re-enumerate the database for each export. Write a complete unique temporary file in the same directory, flush and close it, then atomically replace the canonical file (or move it into place on first creation). On failure preserve the previous snapshot. Reader rejects snapshots larger than 32 MiB or containing more than 100,000 game records; require schemaVersion 1, a valid ISO-8601 UTC `generatedAtUtc`, and a `games` array. Reject malformed top-level JSON, missing/invalid required envelope fields, and unsupported schema versions without replacing last-known-good in-memory data. Skip invalid individual records and report them; if the file is missing and no cached snapshot exists, report integration unavailable rather than an empty library. An empty `games` array is valid. If a prior valid snapshot is cached, retain it when a later read is missing or invalid and surface the integration error. Valid snapshots older than 24 hours remain searchable with explicit stale status. Parse off the CmdPal UI thread.

Launch: validate the selected ID as a GUID and open playnite://playnite/start/{id:D}. Do not derive executable paths or shell-interpolate snapshot data. URI dispatch is not confirmation that the game started.

Local smoke test pending; not performed in this environment. On the maintainer's Windows machine, obtain a real game's GUID through Playnite's plugin API or an existing local diagnostic (do not inspect or parse Playnite's internal database files for this purpose). In PowerShell, run the following and enter that GUID when prompted:

```powershell
$gameId = Read-Host 'Enter a real Playnite game GUID (do not commit or paste it into chat)'
$parsedId = [guid]::Empty
if (-not [guid]::TryParse($gameId, [ref]$parsedId)) { throw 'Invalid game GUID' }
$uri = "playnite://playnite/start/$($parsedId.ToString('D'))"
Write-Host 'Dispatching Playnite URI'
Start-Process $uri
```

Confirm the intended game actually starts through Playnite; successful `Start-Process` dispatch alone is not a pass. Record Playnite version, standard/portable install mode, whether Playnite was already running, and observed outcome. If URI dispatch fails, check protocol registration before trying `& '<Playnite.exe path>' --start $($parsedId.ToString('D'))` with the same ID. Do not commit or share the game ID.
