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


## 6. Duplicate extension-entry investigation (blocking until manually verified)

The maintainer-reported symptom is two **extension entries** when searching for `Playnite` in Command Palette: one has no description and opens the working extension; the other has a description but does nothing. This is not a duplicate-game/catalog-record issue.

### Checked-in registration facts

The current source has one `uap3:Extension` with one `uap3:AppExtension` named `com.microsoft.commandpalette` (ID `ID`), one COM class registration, and one `CommandItem` returned by `PlayniteCmdPalCommandsProvider.TopLevelCommands()`. The manifest identity is `Name=PlayniteCmdPal`, `Publisher=CN=PlayniteCmdPal`, `Version=0.0.1.0`. The manifest app-extension `DisplayName`, `CommandProvider.DisplayName`, and `CommandItem.Title` are all `Playnite`. The description is present; there is no source evidence that it creates a second registration.

The `dev` baseline uses the same package identity and version and also declares one Command Palette app extension and one command item; its visible names are `PlayniteCmdPal`. The WERD-61 branch changes the visible names to `Playnite`, not the package identity, version, AppExtension count, or command count. This source inspection does **not** prove what Windows has registered or what Command Palette has cached. The duplicate has not yet been reproduced or cleared on a clean Windows install.

### Reproduce, record, and cleanly reinstall

Before changing the installation, record the PowerToys/Command Palette version (PowerToys Settings → General → About), the screenshot of both results, and the installed package records:

```powershell
Get-AppxPackage -Name PlayniteCmdPal |
    Select-Object Name, PackageFullName, PackageFamilyName, Publisher, Version, InstallLocation |
    Format-List
```

If elevated access is available, also check whether the package is registered for other users:

```powershell
Get-AppxPackage -AllUsers -Name PlayniteCmdPal |
    Select-Object Name, PackageFullName, PackageFamilyName, Publisher, Version, InstallLocation |
    Format-List
```

Then perform a current-user clean reinstall (this removes the installed Command Palette extension package; it does not remove the separate Playnite companion plugin or the Playnite library):

1. Exit PowerToys from its notification-area menu so Command Palette is no longer running.
2. In PowerShell, inspect the matching package records again. If more than one record is returned, record each full package name before removal.
3. Remove the current-user registration:
   ```powershell
   Get-AppxPackage -Name PlayniteCmdPal | Remove-AppxPackage
   ```
4. Confirm that `Get-AppxPackage -Name PlayniteCmdPal` returns no current-user package.
5. In Visual Studio, select the `x64` / `Debug` configuration and use **Build → Deploy** for the packaged project. Do not use the unpackaged launch profile.
6. Confirm that exactly one current-user package is registered and record its full identity, publisher, version, and install location with the first command above.
7. Start PowerToys and Command Palette. Run **Reload → Reload Command Palette extensions**.
8. Search for `Playnite`. Record the number of extension entries, whether each has a description, and whether the single remaining entry opens the search page and returns results.

Microsoft's extension-development guidance explicitly requires **Build → Deploy** (not Build alone) and a Command Palette extension reload after package changes. The PowerShell package cmdlets are documented by Microsoft. See the links in the repository PR discussion.

### Duplicate-result mitigation in WERD-61

The packaged application's ordinary Windows app identity is now displayed as **Command Palette Extension Host** in both the package-level `Properties/DisplayName` and application `uap:VisualElements/DisplayName`. The actual `uap3:AppExtension` remains named **Playnite**, as does the Command Palette provider and command item. This separates the host application's Start/app-catalog label from the extension's Command Palette label, so searching for Playnite should surface the extension rather than the host app as a second, inert result.

After deploying this change, reload Command Palette extensions and search for `Playnite`. Confirm there is exactly one result and that it opens the dynamic game-search page. Also search for `Command Palette Extension Host` to confirm the host app has only its distinct label if it is surfaced in the palette. This is a targeted mitigation based on the identical package path and differing activation behavior; it still requires the manual Windows check below and should not be described as verified fixed until that check passes.

### Interpretation

- If the clean reinstall leaves one registered package and one usable entry, the previous duplicate was consistent with stale package registration or discovery state. Record the before/after package records and exact PowerToys version; do not claim the root cause is proven beyond that evidence.
- If one registered package still produces two entries after the explicit extension reload, preserve the screenshot and package records. Investigate Command Palette's package-catalog discovery/cache behavior and extension activation logs before changing manifest descriptions or filtering catalog results.
- If multiple package identities or publishers are present, compare their full names, publishers, versions, and install locations before removing any remaining package. Do not delete unrelated packages.

The clean reinstall sequence above is a reproducible diagnostic procedure, **not a completed verification**. Do not mark the duplicate-entry blocker fixed until the observed two-entry state is reproduced and the post-cleanup state is verified on Windows.

## 7. Report results


Record Windows, .NET SDK, Visual Studio, PowerToys Command Palette, and Playnite versions, plus the exact commands and outcomes. Separate automated results from manual integration checks. Do not claim the real launch path works until a game has been launched through the installed Playnite URI handler.
