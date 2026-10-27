# Research: Market and Feasibility

**Last reviewed:** 2026-10-27. Findings below distinguish documented facts from implementation checks that remain.

## Technical findings

- Microsoft documents how to generate, deploy, reload, and develop a Command Palette extension: https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension
- The current repository contains the generated solution `testExtension.sln` and project `testExtension/testExtension.csproj`; build and local deployment still need verification.
- Checked-in target framework is `net10.0-windows10.0.26100.0`; minimum supported Windows platform is `10.0.19041.0`; runtime identifiers are `win-x64;win-arm64`.
- The central package file pins `Microsoft.CommandPalette.Extensions` to `0.9.260303001`, `Microsoft.Windows.CsWinRT` to `2.2.0`, and `Shmuelie.WinRTServer` to `2.1.1`. Source: https://github.com/werdscopez7347/PlayniteCmdPal/blob/main/Directory.Packages.props
- Playnite documents `--start <gameId>` and `playnite://playnite/start/<gameId>`, plus `showgame` and `search` URI commands: https://api.playnite.link/docs/manual/advanced/cmdlineArguments.html
- Playnite's plugin SDK exposes `IPlayniteAPI.Database` and `StartGame(Guid)` in plugin context: https://api.playnite.link/docs/api/Playnite.SDK.IPlayniteAPI.html
- The Playnite library tutorial demonstrates enumerating games via `PlayniteApi.Database.Games`: https://api.playnite.link/docs/tutorials/extensions/library.html
- These plugin-context APIs do not by themselves establish a general-purpose external catalog API.
- PowerToys CmdPal source: https://github.com/microsoft/PowerToys/tree/main/src/modules/cmdpal
- Gallery requirements and current distribution options must be checked against the official guide before release: https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extension-gallery

## Current architecture recommendation

Keep Playnite authoritative. Use its documented launch-by-ID URI after a focused local smoke test. The selected catalog transport is a companion plugin writing a normalized read-only JSON snapshot; do not use direct database writes, UI automation, or IPC.

The launch mechanism and catalog transport decision are documented. Remaining work is implementation and the real-game local launch smoke test.

## Market

The differentiation is a keyboard-first search surface for Playnite's unified catalog and Playnite-configured launch workflow, not another storefront-specific launcher. The CmdPal gallery creates a distribution route but does not prove demand. Game Library Dock is an adjacent Steam-focused extension; its scope is not the same as Playnite's unified library: https://microsoft.github.io/CmdPal-Extensions/

Validate demand through a working demo and community feedback; do not equate gallery size, stars, or downloads with active users.

## Remaining implementation checks

- Build and deploy the checked-in template and record actual installed SDK/Visual Studio requirements.
- Test the documented Playnite launch URI with a real game ID.
- Implement the selected snapshot writer and reader against the documented contract.
- Confirm provider refresh behavior from the current generated code and current Microsoft docs.
- Check current packaging/gallery requirements before release.

## Sources

- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extension-gallery
- https://github.com/microsoft/PowerToys/tree/main/src/modules/cmdpal
- https://api.playnite.link/docs/manual/advanced/cmdlineArguments.html
- https://api.playnite.link/docs/api/Playnite.SDK.IPlayniteAPI.html
- https://api.playnite.link/docs/api/Playnite.SDK.IGameDatabase.html
- https://api.playnite.link/docs/tutorials/extensions/library.html
- https://api.playnite.link/docs/api/Playnite.SDK.Models.Game.html

## WERD-56 decision (2026-10-27)

Selected catalog transport: a companion Playnite plugin exports a normalized JSON snapshot; the out-of-process CmdPal extension reads it read-only. The official references reviewed document in-process database access but do not document a general external-process catalog endpoint. This is a finding about the reviewed documented API, not a claim that no third-party or undocumented interface exists.

The plugin reads PlayniteApi.Database.Games and exports only an allowlisted projection. Canonical path: %LOCALAPPDATA%\\PlayniteCmdPal\\catalog-v1.json. Use default per-user Local AppData permissions; CmdPal opens the file read-only. Write a complete temporary file in the same directory, flush and close it, then atomically replace the canonical file. Preserve the previous snapshot on write failure. No named pipe, network listener, database parsing, or UI automation.

Schema v1: schemaVersion integer 1, generatedAtUtc ISO-8601 UTC timestamp, and games array. Each record has id (Playnite database GUID in canonical D format), required name, optional source and platform, and required isInstalled. Do not export full SDK models, executable paths, or configured actions.

Refresh on plugin initialization and OnLibraryUpdated; also subscribe to game collection change/update events, debounce bursts, and re-enumerate the database on export. Bound file size and game count. Reject malformed top-level JSON and unsupported schema; skip invalid individual records; never replace last-known-good in-memory data with invalid content. Missing file means integration unavailable, not an empty library. An empty games array is a valid empty library. A valid snapshot older than 24 hours remains searchable but must be marked stale.

Playnite documents --start <gameId> and playnite://playnite/start/<gameId>. Use the URI as primary launch path, validate the ID as a GUID, and construct playnite://playnite/start/{id:D}. URI dispatch is not proof the game started. References: https://api.playnite.link/docs/manual/advanced/cmdlineArguments.html ; https://api.playnite.link/docs/api/Playnite.SDK.IPlayniteAPI.html ; https://api.playnite.link/docs/tutorials/extensions/library.html ; https://github.com/JosefNemec/Playnite/blob/master/source/Playnite/App/PlayniteApplication.cs

Manual check pending: this environment did not have access to the maintainer's installed Playnite instance or a real game ID. Do not claim local URI launch passed. Exact smoke-test steps are in docs/TECHNICAL-DESIGN.md.
