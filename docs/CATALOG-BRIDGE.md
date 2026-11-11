# Playnite Catalog Bridge

> Artwork transport and the schema v2 trust boundary are specified in [Artwork transport](ARTWORK-TRANSPORT.md). The v1 contract below remains unchanged for legacy catalogs. New producers publish v2.

This document describes the catalog snapshot transport accepted in [ADR-005](DECISIONS.md#adr-005-companion-plugin-json-snapshot-for-catalog-access). It does not change the Command Palette search UI; the presentation layer consumes `IReadOnlyPlayniteCatalog`.

## Components

- `PlayniteCmdPal.Snapshot` (`net462` and `net10.0`): shared v1 contract, allowlisted serializer, atomic file publisher, and debounced export coordinator. It has no Playnite or CmdPal SDK dependency.
- `PlayniteCmdPal.Playnite` (`net10.0`): strict parser and read-only adapter/cache mapping snapshots to `PlayniteCmdPal.Core.GameEntry`.
- `PlayniteCmdPal.PlaynitePlugin` (`net462`): companion Playnite generic plugin that projects `PlayniteApi.Database.Games` and publishes the snapshot.
- `PlayniteCmdPal.Playnite.Tests`: synthetic-file adapter/protocol tests. `scripts/test-playnite-snapshot-netfx.ps1` additionally exercises the actual `net462` writer/publisher in Windows PowerShell 5.1. Neither requires Playnite, PowerToys, or a personal library.

The canonical file is `%LOCALAPPDATA%\PlayniteCmdPal\catalog-v2.json`. The older `%LOCALAPPDATA%\PlayniteCmdPal\catalog-v1.json` is retained only as a legacy fallback when v2 is absent; it is not the active producer output. The plugin creates the directory with inherited per-user permissions. The adapter opens the file read-only with `FileShare.ReadWrite | FileShare.Delete`; it never creates, repairs, or deletes the snapshot. Artwork transport, v2 fields, cache lifecycle, and rollout policy are specified in [Artwork transport](ARTWORK-TRANSPORT.md).

## Schema v1

```json
{
  "schemaVersion": 1,
  "generatedAtUtc": "2026-10-30T00:00:00.0000000Z",
  "games": [
    {
      "id": "11111111-1111-4111-8111-111111111111",
      "name": "Example",
      "isInstalled": true,
      "source": "Steam",
      "platform": "PC"
    }
  ]
}
```

`generatedAtUtc` is an ISO-8601 UTC timestamp; fractional seconds are optional, and non-UTC offsets are rejected. `id` is Playnite's stable GUID in lowercase canonical D format and is the only identity key. Empty GUIDs, non-canonical IDs, blank names, missing required fields, wrong field types, duplicate JSON properties, and unknown v1 fields are invalid. `source` and `platform` are optional strings or null; the plugin joins multiple platform names with `, ` in ordinal order. No executable paths, launch actions, icons, or full SDK models are exported.

The parser rejects input over 32 MiB, JSON deeper than 16 levels, unsupported schema versions, malformed envelopes, and arrays over 100,000 records. Invalid individual records and later duplicate IDs are skipped and counted; the first valid record for an ID wins. In v2, malformed optional artwork identifiers are nulled individually; valid game identity and metadata are retained. An empty `games` array is a valid empty library.

## Schema v2 enrichment: recently played

Schema v2 retains the optional `coverArtworkId` and `iconArtworkId` fields described in [Artwork transport](ARTWORK-TRANSPORT.md), and adds optional `lastActivity`. The value is serialized with the round-trip .NET `DateTime` format (`O`) and preserves its `DateTime.Kind`; v1 remains frozen and never contains it. Missing play history is represented by an omitted or null value. The reader interprets `Unspecified` values as local machine time when ordering and displaying them; the UI formats all values in the local display zone using a 24-hour `yyyy-MM-dd HH:mm` format, without a timezone label or UTC offset. It does not use `Modified` as a substitute.

The Recently played page sorts by descending UTC instant, then title using `OrdinalIgnoreCase`, title using `Ordinal`, and stable Playnite game ID using `Ordinal`. Search filters game rows only; status, warning, refresh, and recovery rows remain visible. The adapter re-reads v2 up to three times at 25 ms intervals when v1 appears newer, covering the normal v1-then-v2 publication window before accepting a v1 rollback.

## Publication and refresh

The plugin requests an export on application start, database open, `OnLibraryUpdated`, game install/uninstall lifecycle events, game collection changes, relevant game updates (including `LastActivity` and artwork-reference changes), and source/platform metadata changes. Requests are debounced by 750 ms and serialized. A request arriving during an export schedules one follow-up export, so the final state is not lost. Each export re-enumerates the database; it does not patch an old snapshot from event payloads.

Projection runs on Playnite's `IMainViewAPI.UIDispatcher` because SDK database objects are affinity-sensitive. Serialization, flush, and file replacement run in the background. The exporter uses .NET Framework's built-in JSON writer and does not reference `System.Text.Json`; the `net10.0` adapter uses its framework's `System.Text.Json` for parsing. Publication is limited to 32 MiB while bytes are written. The publisher writes a complete same-directory temporary file, flushes it to disk, closes it, then uses `File.Replace` (or `File.Move` on first creation). A failed or canceled export leaves the previous snapshot intact and removes only its own temporary file. If the library exceeds 100,000 records, the plugin refuses to publish a truncated catalog.

## Adapter cache policy

- First `GetAsync` loads the file. Later `GetAsync` calls revalidate at most once per 30 seconds; between attempts they return the current in-memory result without file I/O.
- `RefreshAsync` bypasses the interval and is the explicit reconnect/retry trigger. Concurrent callers share one in-flight read.
- A valid snapshot replaces the cache, including a valid empty library. The adapter prefers the newer generated timestamp across v1/v2; ties prefer v2.
- A missing, unreadable, malformed, unsupported, oversized, or over-limit later file never replaces last-known-good data. The result remains available if a valid snapshot was previously loaded and carries the current `Issue`.
- With no previous valid snapshot, missing/malformed input is `IsAvailable == false`, not an empty library.
- A snapshot at least 24 hours old is `IsStale == true` but remains searchable. Staleness is recomputed from `generatedAtUtc` on every result, including cached results.
- `SkippedRecordCount` reports invalid records skipped from the last valid snapshot. `IsFromCache` distinguishes cached/retained results from a fresh successful read.
- All adapter file I/O and parsing are asynchronous/background work. Pre-canceled calls return a canceled task; canceling one caller does not cancel a shared in-flight read. Callers must still avoid blocking on the task from the CmdPal UI thread.

There is no file watcher, network listener, IPC, or Playnite database parsing. Reconnection is an explicit refresh or the next interval-based `GetAsync` after the bridge publishes a file.

## Build and package the plugin

On Windows with the repository's .NET 10 SDK and .NET Framework 4.6.2 targeting pack:

```powershell
dotnet build .\PlayniteCmdPal.sln -c Release
dotnet test .\PlayniteCmdPal.Playnite.Tests\PlayniteCmdPal.Playnite.Tests.csproj -c Release --no-build
.\scripts\package-playnite-plugin.ps1
```

The packaging script first runs a synthetic .NET Framework smoke test against the published assemblies using Windows PowerShell 5.1, then creates `artifacts/playnite-plugin/PlayniteCmdPalBridge.pext`. In Playnite 10.63, install it locally by dragging the `.pext` onto the main Playnite window and confirming the install/restart prompts, or by running `Playnite.DesktopApp.exe --installext <path-to.pext>`. For development you can instead register `artifacts/playnite-plugin/publish` under Settings → For developers → External extensions → Add, enable it, and restart Playnite. The top-level Add-ons… window manages installed add-ons (this plugin appears under Installed → Generic); it does not provide an install-from-file button. The Menu → Extensions submenu only lists extension-provided commands. Build output and `.pext` files are not committed.

## Validation status

**Automated verification:** WERD-74 adds deterministic tests for the v1/v2 publication interleaving, initially missing v2, timestamp-kind handling, search/status composition, empty-state classification, and the combined v1/v2 .NET Framework smoke path. Run Windows CI against the final merged pull-request head before merge; earlier green runs do not validate subsequent commits.

**Manual Playnite verification remains:** CI does not run inside a live Playnite host or inspect a user's library. Install the updated companion plugin in the user's Playnite environment and verify that real cover/icon references are copied into the bridge-owned cache, v2 `lastActivity` is populated and the Recently played page displays the expected local 24-hour timestamp without a timezone suffix, the snapshot is refreshed after game/artwork changes, missing artwork remains non-fatal, and the previous cache remains usable if publication is interrupted. Do not share or commit personal game names, IDs, or library exports.

## Troubleshooting a missing snapshot

Playnite writes host loading errors to `playnite.log`, but messages and exceptions emitted by extension loggers go to `extensions.log`. For a normal per-user installation, both are under `%APPDATA%\Playnite`; portable installations and custom user-data directories use their Playnite data root instead.

Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\diagnose-playnite-bridge.ps1` from the repository root to inspect the installed files, snapshot state, and recent bridge log entries. The diagnostic does not print catalog contents, but logs can contain personal information; review and redact them before sharing.

## Manual Playnite verification still required

Automated tests use synthetic snapshots only. The following read-only verifier uses the real `PlayniteCatalogAdapter` and prints aggregate status and counts, not game names, IDs, or file contents:

```powershell
dotnet run --project .\tools\CatalogVerifier\CatalogVerifier.csproj --configuration Release
```

It checks an initial read, an immediate cached read, and an explicit refresh; valid empty libraries are supported. A differing ID set between the two reads can also mean the library changed concurrently. This check does not compare metadata with Playnite or demonstrate an event-driven refresh.

Remaining real-library verification:

1. Install the packaged bridge in Playnite 10.63 or the supported version being tested.
2. Start Playnite with a small test library containing duplicate display names and at least one installed and one uninstalled game.
3. Confirm `%LOCALAPPDATA%\PlayniteCmdPal\catalog-v2.json` appears and contains only the allowlisted fields.
4. Run the verifier above or use the Command Palette page; compare stable IDs, names, source/platform, and install state locally with Playnite without sharing library data.
5. Add/rename/remove a game and change source/platform metadata; confirm the snapshot refreshes after the debounce and that no partial file is observable.
6. Close Playnite and back up both snapshot files outside the bridge directory. With both `catalog-v2.json` and `catalog-v1.json` absent, confirm a fresh adapter process reports unavailable. Next, leave v2 absent and place a known-valid v1 fixture at `catalog-v1.json`; confirm a fresh adapter process reads it through the legacy fallback. Restore the backed-up files (or remove the fixture and let Playnite regenerate v2) before restarting Playnite, then confirm explicit refresh reconnects.
7. Record the Playnite/Windows versions and observed outcomes without committing game IDs, titles, or library exports.

The existing launch-by-ID URI smoke test is separate from catalog bridge verification and does not verify this bridge.


### Recently Played freshness

The companion plugin periodically re-exports the catalog every 30 seconds. This is intentional: Playnite can update a game's `LastActivity` when a game exits without delivering a corresponding game-update event to every plugin subscriber. The periodic export ensures the schema-v2 snapshot eventually reflects that activity. The Recently Played page exposes one **Refresh Playnite catalog** action in its top-level More commands menu; it re-reads the latest published snapshot without adding a duplicate refresh row to the results.
