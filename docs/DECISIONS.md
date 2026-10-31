# Architecture Decision Log

## ADR-001: Playnite remains the source of truth
**Status:** Accepted  
Use Playnite's unified library and configured launch behavior rather than integrating each storefront independently.

## ADR-002: C# and official CmdPal template
**Status:** Accepted; generated-template validation completed  
Use the generated C# template and its current package versions. The checked-in target framework is `net10.0-windows10.0.26100.0`; the central package version for `Microsoft.CommandPalette.Extensions` is `0.9.260303001`. The template build/deployment validation is complete. This validates the extension scaffold and local development baseline, not the finished Playnite catalog/search/launch integration or release packaging.

## ADR-003: No direct database writes or UI automation
**Status:** Accepted  
Never write to Playnite data. Do not use UI automation as the production integration.

## ADR-004: Use Playnite's documented launch interface
**Status:** Direction accepted; running- and closed-instance real-game smoke tests passed (user-reported)

Playnite documents `playnite://playnite/start/<gameId>` and `--start <gameId>`: https://api.playnite.link/docs/manual/advanced/cmdlineArguments.html. Prefer the URI as the primary launch request; use the CLI form only as a fallback if URI dispatch fails in the supported setup. Validate the selected stable ID as a GUID and format it as canonical D form before constructing the URI. Dispatching a URI is not proof the game actually started. The user reports that the URI launched an intended Steam-installed game with a normal installation of Playnite 10.63 already running on 2026-10-29. The user also reports that, with Playnite fully closed, including its system-tray process, the same URI reopened Playnite and then launched the selected game. CLI fallback remains untested. See [the smoke test](TECHNICAL-DESIGN.md#manual-uri-smoke-test).

## ADR-005: Companion-plugin JSON snapshot for catalog access
**Status:** Accepted; implementation present; Windows CI, CodeQL, and the .NET Framework snapshot smoke test passed; maintainer-reported real-library verification is documented.

The reviewed official Playnite SDK documentation exposes `IPlayniteAPI.Database` and demonstrates `PlayniteApi.Database.Games` enumeration in an in-process plugin, but does not document a general external-process catalog endpoint. This conclusion is limited to the reviewed documented API; it is not a claim that no third-party or undocumented interface exists.

Use a small companion Playnite plugin to export an allowlisted, normalized JSON snapshot. The out-of-process CmdPal extension reads that file only. Canonical path: `%LOCALAPPDATA%\PlayniteCmdPal\catalog-v1.json`. Keep it in the current user's Local AppData with inherited per-user permissions; neither component needs elevation, and the reader opens the file read-only. Do not export full SDK models, executable paths, configured actions, or private library data into the repository.

Schema v1 contains `schemaVersion`, `generatedAtUtc`, and `games`. Each game has Playnite's stable GUID `id` in canonical D format, required non-empty `name`, required Boolean `isInstalled`, and optional string `source` and `platform`. The GUID is the identity key; title is display/search data only.

Refresh on plugin initialization and `OnLibraryUpdated`, plus debounced game collection-change/update events. Serialize writes and re-enumerate the database for each export. Write a complete temporary file in the same directory, flush and close it, then atomically replace the canonical file (or move it into place on first creation). A failed export must leave the prior snapshot intact.

The reader rejects snapshots larger than 32 MiB or containing more than 100,000 game records. It rejects malformed top-level JSON, missing/invalid required envelope fields, and unsupported schema versions; skips invalid individual records while reporting them; and never replaces last-known-good in-memory data with invalid input. If the file is missing and no cached snapshot exists, report integration unavailable rather than an empty library. An empty `games` array is a valid empty library. If a prior valid snapshot is cached, retain it when a later read is missing or invalid and surface the integration error. A valid snapshot older than 24 hours remains usable but must be marked stale. This ADR is the canonical contract; [Technical Design](TECHNICAL-DESIGN.md) links to it and [Catalog Bridge](CATALOG-BRIDGE.md) documents the implemented API, cache policy, packaging, and manual verification checklist.

Implementation cache policy: the reader revalidates on first use and at most once per 30 seconds; `RefreshAsync` bypasses that interval and concurrent reads coalesce. A valid empty array replaces the previous library. Missing/invalid later files retain last-known-good data and surface an issue; with no prior valid snapshot the result is unavailable. Staleness is recomputed from `generatedAtUtc` on every result, including cached results, and begins at exactly 24 hours. The v1 parser also rejects duplicate JSON properties, unknown v1 fields, non-UTC timestamps, and JSON deeper than 16 levels. The writer debounces requests by 750 ms, serializes exports, and coalesces changes during an export into one follow-up export.

**Trade-offs:** A documented external catalog API would avoid a companion plugin, but none was found in the official API reviewed. A snapshot keeps the reader independent of Playnite's internal storage and works without a live connection, at the cost of installing a plugin and potentially stale data. Direct database parsing couples consumers to undocumented storage; UI automation is fragile; IPC adds lifecycle and security complexity without demonstrated need. Revisit the decision if an official external interface or evidence of insufficient snapshot freshness emerges.

Do not write Playnite's database, parse its internal files as the supported contract, automate its UI, start a network listener, or add named-pipe IPC without new evidence that the snapshot is insufficient.

Evidence:
- [Playnite command-line and URI arguments](https://api.playnite.link/docs/manual/advanced/cmdlineArguments.html)
- [IPlayniteAPI](https://api.playnite.link/docs/api/Playnite.SDK.IPlayniteAPI.html)
- [IGameDatabase](https://api.playnite.link/docs/api/Playnite.SDK.IGameDatabase.html)
- [Playnite library extension tutorial](https://api.playnite.link/docs/tutorials/extensions/library.html)
- [Playnite URI routing source](https://github.com/JosefNemec/Playnite/blob/master/source/Playnite/App/PlayniteApplication.cs)

## ADR-006: Clock/timer extension is separate
**Status:** Accepted  
Clock and timer functionality is out of scope for this repository and MVP.

## ADR-007: Project license and release policy
**Status:** MIT license and initial release policy accepted; dependency and redistribution audit completed; distribution verification pending  
PlayniteCmdPal-authored material is licensed under the MIT License in the repository root `LICENSE`. Microsoft PowerToys template source retains its separate MIT notice in `third-party/Microsoft-PowerToys-LICENSE.txt`; third-party dependencies and other materials retain their own terms. Contributions are intended under the project MIT terms, subject to third-party rights and notice preservation.

Use Semantic Versioning; before 1.0.0, use `0.y.z` and increment the minor version for incompatible changes. Publish version-tagged releases through GitHub Releases. Use the release description as the release notes; defer a separate `CHANGELOG.md` until release volume justifies it. Mark preview versions as prereleases using SemVer prerelease identifiers. Third-party notices travel inside the artifacts rather than only in the repository: `third-party/THIRD-PARTY-NOTICES.md` and `third-party/Microsoft-PowerToys-LICENSE.txt` are copied to the extension build/publish output by `PlayniteCmdPal/PlayniteCmdPal.csproj`, the notice file is copied to the bridge plugin publish output by `PlayniteCmdPal.PlaynitePlugin/PlayniteCmdPal.PlaynitePlugin.csproj`, and `scripts/package-playnite-plugin.ps1` refuses to package a `.pext` without it. The dependency and redistribution audit is recorded in [Licensing and release readiness](LICENSING-AND-RELEASE.md); its reproducible inventory generator is `scripts/licensing/New-DependencyInventory.ps1`. Before public distribution, close that document's open review items (Windows SDK projection redistributability, the preview Windows SDK targeting pack, .NET runtime notice shipping, unused central package versions), verify the original project artwork, and validate the MSIX identity and signing certificate.
