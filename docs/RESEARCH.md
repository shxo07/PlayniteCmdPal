# Research: Market and Feasibility

**Last reviewed:** 2026-10-30. Findings below distinguish documented facts from implementation checks that remain.

## Technical findings

- Microsoft documents how to generate, deploy, reload, and develop a Command Palette extension: https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension
- The current repository contains the generated solution `testExtension.sln` and project `testExtension/testExtension.csproj`; build and local deployment are recorded in WERD-57.
- Checked-in target framework is `net10.0-windows10.0.26100.0`; minimum supported Windows platform is `10.0.19041.0`; runtime identifiers are `win-x64;win-arm64`.
- The central package file pins `Microsoft.CommandPalette.Extensions` to `0.9.260303001`, `Microsoft.Windows.CsWinRT` to `2.2.0`, and `Shmuelie.WinRTServer` to `2.1.1`. Source: https://github.com/werdscopez7347/PlayniteCmdPal/blob/main/Directory.Packages.props
- Playnite documents `--start <gameId>` and `playnite://playnite/start/<gameId>`, plus `showgame` and `search` URI commands: https://api.playnite.link/docs/manual/advanced/cmdlineArguments.html
- Playnite's plugin SDK exposes `IPlayniteAPI.Database` and `StartGame(Guid)` in plugin context: https://api.playnite.link/docs/api/Playnite.SDK.IPlayniteAPI.html
- The Playnite library tutorial demonstrates enumerating games via `PlayniteApi.Database.Games`: https://api.playnite.link/docs/tutorials/extensions/library.html
- These plugin-context APIs do not by themselves establish a general-purpose external catalog API.
- PowerToys CmdPal source: https://github.com/microsoft/PowerToys/tree/main/src/modules/cmdpal
- Gallery requirements and current distribution options must be checked against the official guide before release: https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extension-gallery

## Current architecture recommendation

Keep Playnite authoritative. Use its documented launch-by-ID URI; real-game smoke tests with Playnite running and fully closed have been reported successful. The selected catalog transport is a companion plugin writing a normalized read-only JSON snapshot; do not use direct database writes, UI automation, or IPC.

The launch mechanism and catalog transport decision are documented. The snapshot writer and reader remain to be implemented and tested.

## Market

The differentiation is a keyboard-first search surface for Playnite's unified catalog and Playnite-configured launch workflow, not another storefront-specific launcher. The CmdPal gallery creates a distribution route but does not prove demand. Game Library Dock is an adjacent Steam-focused extension; its scope is not the same as Playnite's unified library: https://microsoft.github.io/CmdPal-Extensions/

Validate demand through a working demo and community feedback; do not equate gallery size, stars, or downloads with active users.

## Remaining implementation checks

- Build and deploy the checked-in template and record actual installed SDK/Visual Studio requirements.
- Investigate CLI fallback separately if needed; it was not covered by the URI smoke tests.
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

The plugin reads PlayniteApi.Database.Games and exports only an allowlisted projection. Canonical path: %LOCALAPPDATA%\PlayniteCmdPal\catalog-v1.json. Use default per-user Local AppData permissions; CmdPal opens the file read-only. Write a complete temporary file in the same directory, flush and close it, then atomically replace the canonical file. Preserve the previous snapshot on write failure. No named pipe, network listener, database parsing, or UI automation.

Schema v1: `schemaVersion` integer 1, `generatedAtUtc` ISO-8601 UTC timestamp, and `games` array. Each record has `id` (Playnite GUID in canonical D format and sole identity key), non-empty `name`, Boolean `isInstalled`, and optional string `source` and `platform`. Do not export full SDK models, executable paths, configured actions, or private library data.

Refresh on plugin initialization and `OnLibraryUpdated`; also subscribe to game collection-change/update events, debounce bursts, serialize writes, and re-enumerate the database on export. Write a complete temporary file in the same directory, flush and close it, then atomically replace the canonical file (or move it into place on first creation); preserve the previous snapshot on write failure. Reject snapshots larger than 32 MiB or containing more than 100,000 game records. Reject malformed top-level JSON, missing/invalid envelope fields, and unsupported schema versions without replacing last-known-good in-memory data. Skip invalid individual records and report them. If the file is missing and no valid snapshot is cached, report integration unavailable, not an empty library; an empty `games` array is valid. If a prior valid snapshot is cached, retain it when a later read is missing or invalid and surface the integration error. Valid snapshots older than 24 hours remain searchable but must be marked stale.

Playnite documents --start <gameId> and playnite://playnite/start/<gameId>. Use the URI as primary launch path, validate the ID as a GUID, and construct playnite://playnite/start/{id:D}. URI dispatch is not proof the game started. References: https://api.playnite.link/docs/manual/advanced/cmdlineArguments.html ; https://api.playnite.link/docs/api/Playnite.SDK.IPlayniteAPI.html ; https://api.playnite.link/docs/tutorials/extensions/library.html ; https://github.com/JosefNemec/Playnite/blob/master/source/Playnite/App/PlayniteApplication.cs

Manual check (user-reported, 2026-10-29): the documented URI launched the intended Steam-installed game on a normal installation of Playnite 10.63 with Playnite already running. The user also reports that, with Playnite fully closed, including its system-tray process, the same URI reopened Playnite and then launched the selected game. CLI fallback was not tested. Steps and scope are in docs/TECHNICAL-DESIGN.md.
