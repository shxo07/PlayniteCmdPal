# Architecture Decision Log

## ADR-001: Playnite remains the source of truth
**Status:** Accepted  
Use Playnite's unified library and configured launch behavior rather than integrating each storefront independently.

## ADR-002: C# and official CmdPal template
**Status:** Accepted; build/deploy validation in progress  
Use the generated C# template and its current package versions. The checked-in target framework is `net10.0-windows10.0.26100.0`; the central package version for `Microsoft.CommandPalette.Extensions` is `0.9.260303001`. Record clean-build and local-deploy results in WERD-57 before calling the setup verified.

## ADR-003: No direct database writes or UI automation
**Status:** Accepted  
Never write to Playnite data. Do not use UI automation as the production integration.

## ADR-004: Use Playnite's documented launch interface
**Status:** Direction accepted; real-game smoke test pending  
Playnite documents `playnite://playnite/start/<gameId>` and `--start <gameId>`: https://api.playnite.link/docs/manual/advanced/cmdlineArguments.html. Prefer the URI as the primary launch request; use the CLI form only as a fallback if URI dispatch fails in the supported setup. Validate the selected stable ID as a GUID and format it as canonical D form before constructing the URI. Dispatching a URI is not proof the game actually started. Record the Playnite version, install mode, whether Playnite was already running, and observed result after the local smoke test.

## ADR-005: Companion-plugin JSON snapshot for catalog access
**Status:** Accepted; implementation pending  
The reviewed official Playnite SDK documentation exposes `IPlayniteAPI.Database` and demonstrates `PlayniteApi.Database.Games` enumeration in an in-process plugin, but does not document a general external-process catalog endpoint. This conclusion is limited to the reviewed documented API; it is not a claim that no third-party or undocumented interface exists.

Use a small companion Playnite plugin to export an allowlisted, normalized JSON snapshot. The out-of-process CmdPal extension reads that file only. Canonical path: `%LOCALAPPDATA%\PlayniteCmdPal\catalog-v1.json`. Keep it in the current user's Local AppData with inherited per-user permissions; neither component needs elevation, and the reader opens the file read-only. Do not export full SDK models, executable paths, configured actions, or private library data into the repository.

Schema v1 contains `schemaVersion`, `generatedAtUtc`, and `games`. Each game has Playnite's stable GUID `id` in canonical D format, required non-empty `name`, required Boolean `isInstalled`, and optional string `source` and `platform`. The GUID is the identity key; title is display/search data only.

Refresh on plugin initialization and `OnLibraryUpdated`, plus debounced game collection-change/update events. Serialize writes and re-enumerate the database for each export. Write a complete temporary file in the same directory, flush and close it, then atomically replace the canonical file (or move it into place on first creation). A failed export must leave the prior snapshot intact.

The reader rejects snapshots larger than 32 MiB or containing more than 100,000 game records. It rejects malformed top-level JSON, missing/invalid required envelope fields, and unsupported schema versions; skips invalid individual records while reporting them; and never replaces last-known-good in-memory data with invalid input. If the file is missing and no cached snapshot exists, report integration unavailable rather than an empty library. An empty `games` array is a valid empty library. If a prior valid snapshot is cached, retain it when a later read is missing or invalid and surface the integration error. A valid snapshot older than 24 hours remains usable but must be marked stale. See the full contract in [Technical Design](TECHNICAL-DESIGN.md).

Do not write Playnite's database, parse its internal files as the supported contract, automate its UI, start a network listener, or add named-pipe IPC without new evidence that the snapshot is insufficient.

Evidence:
- [Playnite command-line and URI arguments](https://api.playnite.link/docs/manual/advanced/cmdlineArguments.html)
- [IPlayniteAPI](https://api.playnite.link/docs/api/Playnite.SDK.IPlayniteAPI.html)
- [IGameDatabase](https://api.playnite.link/docs/api/Playnite.SDK.IGameDatabase.html)
- [Playnite library extension tutorial](https://api.playnite.link/docs/tutorials/extensions/library.html)
- [Playnite URI routing source](https://github.com/JosefNemec/Playnite/blob/master/source/Playnite/App/PlayniteApplication.cs)
- Tracking: [WERD-56](https://linear.app/werdscopez7347/issue/WERD-56/spike-finalize-the-supported-playnite-catalog-and-launch-integration)

## ADR-006: Clock/timer extension is separate
**Status:** Accepted  
Clock and timer functionality is out of scope for this repository and MVP.
