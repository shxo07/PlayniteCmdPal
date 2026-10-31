# Command Palette search and launch smoke test (WERD-61)

These checks require Windows, the repository's .NET 10 SDK, Microsoft PowerToys Command Palette, the packaged extension, and the Playnite companion bridge. Use a small test library where possible. Do not record or share real game titles, IDs, paths, or catalog contents.

## 1. Automated checks

Run from PowerShell at the repository root:

```powershell
dotnet restore .\PlayniteCmdPal.sln
dotnet test .\PlayniteCmdPal.sln -c Release
dotnet build .\PlayniteCmdPal.sln -c Release
```

The core tests cover deterministic title ranking and validation of the stable-ID launch URI. The Playnite adapter/protocol tests use synthetic snapshots and do not require a personal library.

## 2. Install or update the Playnite bridge

If it is not already installed, build and package the companion plugin:

```powershell
.\scripts\package-playnite-plugin.ps1
```

Install `artifacts/playnite-plugin/PlayniteCmdPalBridge.pext` by dragging it onto the Playnite window and accepting the prompts, or use the Playnite `--installext` command documented in `docs/CATALOG-BRIDGE.md`. Restart Playnite and confirm the bridge creates:

```text
%LOCALAPPDATA%\PlayniteCmdPal\catalog-v1.json
```

Do not upload or commit this file; it contains private library metadata.

## 3. Deploy the Command Palette extension

1. Open `PlayniteCmdPal.sln` in Visual Studio on Windows with the required Windows App SDK/MSIX tooling.
2. Select the `x64` platform and `Debug` configuration.
3. Use **Build → Deploy** (Build alone does not register/update the packaged extension).
4. Open Command Palette and run **Reload → Reload Command Palette extensions**.
5. Open the PlayniteCmdPal command. Its page title should be **Playnite** and its search prompt should appear.

## 4. Search and launch

1. Search for a title known to exist in the local Playnite library.
2. Confirm exact, prefix, word-prefix, and substring matches appear in deterministic relevance order.
3. Confirm each result keeps its stable game identity and shows only available source, platform, and installation metadata.
4. If two games share a title, confirm the metadata and short ID suffix distinguish the results.
5. Select a result and press Enter. Confirm Playnite opens or is reused and starts that game using Playnite's configured launch behaviour. The palette dismissing is not sufficient evidence; verify the intended game actually starts.
6. Repeat with Playnite already running and fully closed (including its tray process).

## 5. Failure and refresh states

- Search for a nonexistent title and confirm a clear no-results row appears.
- With a valid empty test library, confirm the empty-library message and Enter-to-refresh action.
- With the bridge missing or the snapshot unavailable, confirm an actionable message; install/start the bridge and activate the refresh row.
- With a previously valid snapshot followed by a malformed or missing snapshot, confirm the adapter retains the last-known-good catalog and surfaces a warning rather than crashing.
- Confirm a snapshot older than 24 hours remains searchable with a stale-data warning.
- Confirm invalid/non-canonical game IDs are rejected by the automated tests and a missing/unregistered URI handler produces an actionable toast.

For missing/malformed-file tests, use a disposable test profile or back up and restore the snapshot. Never modify or share a real library export. The adapter's explicit refresh bypasses its normal 30-second read interval.

## 6. Report results

Record Windows, .NET SDK, Visual Studio, PowerToys Command Palette, and Playnite versions, plus the exact commands and outcomes. Separate automated results from manual integration checks. Do not claim the real launch path works until a game has been launched through the installed Playnite URI handler.
