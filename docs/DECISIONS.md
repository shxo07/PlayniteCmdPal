# Architecture Decision Log

## ADR-001: Playnite remains the source of truth
**Status:** Accepted  
Use Playnite's unified library and configured launch behavior rather than integrating each storefront independently.

## ADR-002: C# and official CmdPal template
**Status:** Accepted; build/deploy validation in progress  
Use the generated C# template and its current package versions. The checked-in target framework is `net10.0-windows10.0.26100.0`; the central package version for `Microsoft.CommandPalette.Extensions` is `0.9.260303001`. Record clean-build and local-deploy results before calling the setup verified.

## ADR-003: No direct database writes or UI automation
**Status:** Accepted  
Never write to Playnite data. Do not use UI automation as the production integration.

## ADR-004: Use Playnite's documented launch interface
**Status:** Direction accepted; local smoke test pending  
Official docs list `--start <gameId>` and `playnite://playnite/start/<gameId>`: https://api.playnite.link/docs/manual/advanced/cmdlineArguments.html. Use the documented URI/CLI after a focused test with a real game ID. The test validates our invocation and error handling, not whether the documented feature exists.

## ADR-005: Catalog transport remains open
**Status:** Open  
The Playnite SDK documents in-process plugin access to `PlayniteApi.Database.Games`, but that does not itself prove an external-process catalog API. First check for a documented external interface. If none exists, evaluate a companion Playnite plugin that exports a normalized read-only JSON snapshot atomically. Do not default to named-pipe IPC; add it only if evidence shows the snapshot is insufficient.

## ADR-006: Clock/timer extension is separate
**Status:** Accepted  
Clock and timer functionality is out of scope for this repository and MVP.

## ADR-007: Companion-plugin JSON snapshot for catalog access
**Status:** Accepted; implementation pending

The reviewed official SDK docs expose IPlayniteAPI.Database and demonstrate in-process PlayniteApi.Database.Games enumeration, but do not document a general external-process catalog endpoint. Use a small companion Playnite plugin to export a read-only normalized JSON snapshot to %LOCALAPPDATA%\\PlayniteCmdPal\\catalog-v1.json. CmdPal reads this file only. Never write Playnite's database, parse its internal files as a supported contract, automate its UI, or add named-pipe IPC without new evidence.

Schema v1 has schemaVersion, generatedAtUtc, and games array; each game has stable GUID id, required name and isInstalled, and optional source/platform. Refresh on plugin initialization, OnLibraryUpdated, and debounced game collection changes. Write through a same-directory temporary file and atomic replace. Missing and malformed snapshots are errors, not empty libraries; valid snapshots older than 24 hours remain usable with explicit stale status. Full contract: docs/TECHNICAL-DESIGN.md.

ADR-004 clarification: use playnite://playnite/start/{gameId} as primary launch request and --start <gameId> only as fallback if needed. Validate the stable GUID. URI dispatch is not proof the game started. Real-game local smoke test remains pending.
