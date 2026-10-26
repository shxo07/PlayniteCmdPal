# Research: Market and Feasibility

**Last reviewed:** 2026-10-26. Findings below distinguish documented facts from implementation checks that remain.

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

Keep Playnite authoritative. Use its documented launch-by-ID interface after a focused local smoke test. For catalog retrieval, check for a documented external API; if none is documented, evaluate a companion Playnite plugin that writes a normalized read-only JSON snapshot. Do not use direct database writes or UI automation, and do not add IPC complexity without evidence.

The launch mechanism is documented. The remaining question is the catalog transport and the verification of our own integration, not whether the documented CLI/URI exists.

## Market

The differentiation is a keyboard-first search surface for Playnite's unified catalog and Playnite-configured launch workflow, not another storefront-specific launcher. The CmdPal gallery creates a distribution route but does not prove demand. Game Library Dock is an adjacent Steam-focused extension; its scope is not the same as Playnite's unified library: https://microsoft.github.io/CmdPal-Extensions/

Validate demand through a working demo and community feedback; do not equate gallery size, stars, or downloads with active users.

## Remaining implementation checks

- Build and deploy the checked-in template and record actual installed SDK/Visual Studio requirements.
- Test the documented Playnite launch URI with a real game ID.
- Choose and implement the catalog transport.
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
