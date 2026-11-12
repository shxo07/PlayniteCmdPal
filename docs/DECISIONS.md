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

Use a small companion Playnite plugin to export an allowlisted, normalized JSON snapshot. The out-of-process CmdPal extension reads those files only. Keep `catalog-v1.json` as the frozen legacy contract for older readers and `catalog-v2.json` as the canonical enriched contract. V2 carries optional `lastActivity`, `coverArtworkId`, and `iconArtworkId`; the current plugin publishes v2, while the reader accepts a valid v1 snapshot as a legacy fallback when v2 is absent. Keep snapshots in the current user's Local AppData with inherited per-user permissions; neither component needs elevation, and the reader opens files read-only. Do not export full SDK models, executable paths, configured actions, or private library data into the repository.

Schema v1 remains frozen and contains `schemaVersion`, `generatedAtUtc`, and `games`; each game has Playnite's stable GUID `id` in canonical D format, required non-empty `name`, required Boolean `isInstalled`, and optional string `source` and `platform`. Schema v2 adds optional `lastActivity`, `coverArtworkId`, and `iconArtworkId`. The current reader accepts both and chooses the valid snapshot with the newest `generatedAtUtc` (preferring v2 on a tie), so legacy-only installations remain readable. Safe producer rollback must follow ADR-009: stop the producer, verify the legacy v1 snapshot, and explicitly retire stale v2 before relying on v1 fallback. The GUID is the identity key; title is display/search data only.

Refresh on plugin initialization and `OnLibraryUpdated`, plus debounced game collection-change/update events. Relevant game updates include `LastActivity` and artwork-reference changes; artwork bytes are also periodically revalidated. Serialize writes and re-enumerate the database for each export. Write a complete temporary file in the same directory, flush and close it, then atomically replace the canonical file (or move it into place on first creation). A failed export must leave the prior snapshot intact.

The reader rejects snapshots larger than 32 MiB or containing more than 100,000 game records. It rejects malformed top-level JSON, missing/invalid required envelope fields, and unsupported schema versions; skips invalid individual records while reporting them; and never replaces last-known-good in-memory data with invalid input. If the file is missing and no cached snapshot exists, report integration unavailable rather than an empty library. An empty `games` array is a valid empty library. If a prior valid snapshot is cached, retain it when a later read is missing or invalid and surface the integration error. A valid snapshot older than 24 hours remains usable but must be marked stale. This ADR is the canonical contract; [Technical Design](TECHNICAL-DESIGN.md) links to it and [Catalog Bridge](CATALOG-BRIDGE.md) documents the implemented API, cache policy, packaging, and manual verification checklist.

Implementation cache policy: the reader revalidates on first use and at most once per 30 seconds; `RefreshAsync` bypasses that interval and concurrent reads coalesce. A valid empty array replaces the previous library. Missing/invalid later files retain last-known-good data and surface an issue; with no prior valid snapshot the result is unavailable. Staleness is recomputed from `generatedAtUtc` on every result, including cached results, and begins at exactly 24 hours. The parser accepts v1 and v2 with separate strict allowlists, rejects duplicate JSON properties, unknown fields, non-UTC timestamps, and JSON deeper than 16 levels. The writer debounces requests by 750 ms, serializes exports, and coalesces changes during an export into one follow-up export.

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


## ADR-008: Local favorites preferences

**Status:** Accepted for WERD-75

Pinned game IDs belong to the Command Palette extension, not to Playnite's library. Store them in `%LOCALAPPDATA%\PlayniteCmdPal\favorites-v1.json`, scoped to the current Windows user and independent of Playnite's standard or portable install path. The file contains only a schema version and stable Playnite game IDs; it contains no game metadata and is never synchronized to a cloud service.

Write a complete versioned document to a uniquely named temporary file in the same directory, flush it, then replace the destination. A malformed JSON document is treated as an empty preference set and is repaired on the next pin/unpin mutation. An unsupported future schema is not overwritten, to avoid destroying data written by a newer extension version. Missing game IDs remain saved but are omitted from the visible Favorites page until they reappear in the Playnite catalog. The store is SDK-independent and uses an injected file path in tests.

This feature must not read or write Playnite database files, alter the catalog snapshot, or change the companion plugin.

---

## ADR-009: Bridge-owned artwork cache and schema v2

**Status:** Accepted; implementation and verification in review

Use a bridge-owned local cache populated only from canonical, game-owned Playnite database file references resolved through the documented SDK. Export only content-derived opaque SHA-256 identifiers; never export original image references, URLs, or filesystem paths. Schema v1 remains immutable and readable; schema v2 adds optional `coverArtworkId` and `iconArtworkId` fields. Malformed optional artwork identifiers are nulled independently without dropping otherwise valid game metadata.

The cache is bounded per generation, and a single producer owns publication. A producer-side periodic revalidation export is required because bytes can change while a database reference remains unchanged. Cache publication is coordinated with the atomic v2 snapshot; cleanup failure must retain transaction state and block a subsequent generation until recovery succeeds. The reader's returned local path is a short-lived locator, not a lease: consumers accept missing artwork after generation replacement and must re-resolve against the current snapshot.

The threat model excludes a malicious process running as the same Windows user. Reparse-point and normalized-path checks are defense-in-depth, not a claim of race-proof containment against same-user path replacement. Transport signature checks do not prove successful decoding; WERD-80 owns decoding and image fallback. A surviving v2 snapshot remains authoritative after producer downgrade; safe rollback requires the owner to stop the producer, verify the v1 snapshot, and explicitly retire the stale v2 snapshot before v1 fallback can occur.

The complete trust boundary, limits, migration, refresh, cleanup, and failure contract is recorded in [Artwork transport](ARTWORK-TRANSPORT.md).


## ADR-010: Recently Played presentation and refresh
**Status:** Accepted for WERD-74

Recently Played is a read-only view over Playnite's authoritative LastActivity value. Sort by UTC instant, treating DateTimeKind.Unspecified as local machine time because that is the documented .NET conversion policy used by this implementation. Display only a relative-age badge; do not display or expose an absolute timestamp, timezone, UTC offset, or hover tooltip. Relative units are truncated to whole minutes (<1 hour), hours (<1 day), days (<7 days), weeks (<30 days), months (<365 days), then years; values under one minute read just now, and future values use in ... wording.

Keep one Refresh Playnite catalog action in the top-level Recently Played command's More commands menu (Ctrl+K). Do not attach duplicate refresh actions to individual game rows or status rows. While the page is open, poll the adapter cache every 10 seconds so relative labels can age; the adapter's normal GetAsync policy limits disk revalidation to once per 30 seconds. An explicit refresh bypasses that interval. The companion plugin periodically re-exports every 30 seconds because Playnite may not emit a reliable LastActivity update event when a game exits.

## ADR-011: One conservative direct-launch fallback, not a ranked global provider

**Status:** Accepted for the WERD-86 prototype; installed-host validation pending (2026-11-12).

The pinned SDK (`Microsoft.CommandPalette.Extensions` 0.9.260303001, including its toolkit) exposes `ICommandProvider.FallbackCommands`, `IFallbackCommandItem`, and `IFallbackHandler.UpdateQuery`, not a general global-query/ranking provider or relative placement API. Microsoft's overview describes a no-other-match contract. Inspection of PowerToys v0.102.3101 (`e84973c4f3724eeedd34d836559dac32734c3e22`) instead shows enabled fallbacks updated independently of normal matches and ordinary nonempty fallbacks appended in a separate Fallbacks section. This proves source-level coexistence for that version, not runtime verification of the user's installed host or a cross-version placement guarantee. Exact APIs, documentation discrepancies, immutable source references, and the unexecuted desktop probe are recorded in [Global search investigation](CMDPAL-GLOBAL-SEARCH.md).

Prototype one ordinary fallback with the matched title, **Open with Playnite** subtitle/action, and the saved Playnite game icon. Reuse `ArtworkIconLoader` and the existing opaque icon-only transport: publish a neutral symbol immediately, update the `Icon` property asynchronously, and never let decoding delay query completion or launch. Missing/invalid artwork keeps the neutral symbol; covers are not substitutes. Reset/cancel artwork on query change, invocation, and disposal; obsolete generations cannot publish an icon over the current match.

Never publish a top-level command for every game or duplicate the Search games command as a generic fallback. Only a unique ordinal case-insensitive full-title match with a canonical stable ID and a healthy, non-stale, complete snapshot is eligible. Do not choose between equal titles using relevance, install state, or page filters. Re-read the snapshot on invocation, require the same unique ID, and reuse `LaunchGameCommand`; never retarget an old row to a replacement ID with the same title. Query changes/disposal invalidate retained commands. No new transport or change to stale-catalog browsing is authorized.

CmdPal owns display, enablement, ranking, and ordering. Follow-up source inspection found its native per-fallback **Include in the Global result** checkbox under **Command Palette Settings → Extensions → Playnite → Fallback commands → Open a game with Playnite**. Recommend that user-controlled preference for main Results: the host partitions and scores the existing fallback there rather than duplicating it. External fallbacks are excluded by default. This is a supported host configuration path, not an extension-owned placement API or permission to force first place.

Do not read/modify private host settings at runtime, access host assemblies, automate its UI, or intercept another provider. If the user's host lacks that preference or suppresses fallbacks when another provider matches, accept that limitation: **Search games → title → explicitly select the game → Enter** remains the supported alternative and the ambiguity/recovery UX. The maintainer's reported Fallbacks visibility does not verify main Results, icon rendering, or actual game launch; those desktop checks remain pending. A snapshot refresh cannot prove live Playnite state or game-start success; do not claim either.
