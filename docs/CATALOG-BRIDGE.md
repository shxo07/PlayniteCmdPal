# Playnite Catalog Bridge

This document describes the WERD-60 implementation of the snapshot transport accepted in [ADR-005](DECISIONS.md#adr-005-companion-plugin-json-snapshot-for-catalog-access). It does not change the Command Palette search UI; WERD-61 consumes `IReadOnlyPlayniteCatalog`.

## Components

- `PlayniteCmdPal.Snapshot` (`net462` and `net10.0`): shared v1 contract, allowlisted serializer, atomic file publisher, and debounced export coordinator. It has no Playnite or CmdPal SDK dependency.
- `PlayniteCmdPal.Playnite` (`net10.0`): strict parser and read-only adapter/cache mapping snapshots to `PlayniteCmdPal.Core.GameEntry`.
- `PlayniteCmdPal.PlaynitePlugin` (`net462`): companion Playnite generic plugin that projects `PlayniteApi.Database.Games` and publishes the snapshot.
- `PlayniteCmdPal.Playnite.Tests`: synthetic-file adapter/protocol tests. `scripts/test-playnite-snapshot-netfx.ps1` additionally exercises the actual `net462` writer/publisher in Windows PowerShell 5.1. Neither requires Playnite, PowerToys, or a personal library.

The canonical file is `%LOCALAPPDATA%\PlayniteCmdPal\catalog-v1.json`. The plugin creates the directory with inherited per-user permissions. The adapter opens the file read-only with `FileShare.ReadWrite | FileShare.Delete`; it never creates, repairs, or deletes the snapshot.

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

The parser rejects input over 32 MiB, JSON deeper than 16 levels, unsupported schema versions, malformed envelopes, and arrays over 100,000 records. Invalid individual records and later duplicate IDs are skipped and counted; the first valid record for an ID wins. An empty `games` array is a valid empty library.

## Publication and refresh

The plugin requests an export on application start, database open, `OnLibraryUpdated`, game install/uninstall lifecycle events, game collection changes, relevant game updates, and source/platform metadata changes. Requests are debounced by 750 ms and serialized. A request arriving during an export schedules one follow-up export, so the final state is not lost. Each export re-enumerates the database; it does not patch an old snapshot from event payloads.

Projection runs on Playnite's `IMainViewAPI.UIDispatcher` because SDK database objects are affinity-sensitive. Serialization, flush, and file replacement run in the background. The exporter uses .NET Framework's built-in JSON writer and does not reference `System.Text.Json`; the `net10.0` adapter uses its framework's `System.Text.Json` for parsing. Publication is limited to 32 MiB while bytes are written. The publisher writes a complete same-directory temporary file, flushes it to disk, closes it, then uses `File.Replace` (or `File.Move` on first creation). A failed or canceled export leaves the previous snapshot intact and removes only its own temporary file. If the library exceeds 100,000 records, the plugin refuses to publish a truncated catalog.

## Adapter cache policy

- First `GetAsync` loads the file. Later `GetAsync` calls revalidate at most once per 30 seconds; between attempts they return the current in-memory result without file I/O.
- `RefreshAsync` bypasses the interval and is the explicit reconnect/retry trigger. Concurrent callers share one in-flight read.
- A valid snapshot replaces the cache, including a valid empty library.
- A missing, unreadable, malformed, unsupported, oversized, or over-limit later file never replaces last-known-good data. The result remains available if a valid snapshot was previously loaded and carries the current `Issue`.
- With no previous valid snapshot, missing/malformed input is `IsAvailable == false`, not an empty library.
- A snapshot at least 24 hours old is `IsStale == true` but remains searchable. Staleness is recomputed from `generatedAtUtc` on every result, including cached results.
- `SkippedRecordCount` reports invalid records skipped from the last valid snapshot. `IsFromCache` distinguishes cached/retained results from a fresh successful read.
- All adapter file I/O and parsing are asynchronous/background work. Pre-canceled calls return a canceled task; canceling one caller does not cancel a shared in-flight read. Callers must still avoid blocking on the task from the CmdPal UI thread.

There is no file watcher, network listener, IPC, or Playnite database parsing. Reconnection is an explicit refresh or the next interval-based `GetAsync` after the bridge publishes a file.

## Build and package the plugin

On Windows with the repository's .NET 10 SDK and .NET Framework 4.6.2 targeting pack:

```powershell
dotnet build .\testExtension.sln -c Release
dotnet test .\PlayniteCmdPal.Playnite.Tests\PlayniteCmdPal.Playnite.Tests.csproj -c Release --no-build
.\scripts\package-playnite-plugin.ps1
```

The packaging script first runs a synthetic .NET Framework smoke test against the published assemblies using Windows PowerShell 5.1, then creates `artifacts/playnite-plugin/PlayniteCmdPalBridge.pext`. In Playnite 10.63, install it locally by dragging the `.pext` onto the main Playnite window and confirming the install/restart prompts, or by running `Playnite.DesktopApp.exe --installext <path-to.pext>`. For development you can instead register `artifacts/playnite-plugin/publish` under Settings → For developers → External extensions → Add, enable it, and restart Playnite. The top-level Add-ons… window manages installed add-ons (this plugin appears under Installed → Generic); it does not provide an install-from-file button. The Menu → Extensions submenu only lists extension-provided commands. Build output and `.pext` files are not committed.

## Validation status

The Linux workspace ran Tree-sitter syntax checks over the C#/PowerShell files and XML/solution consistency checks. It could not run `dotnet build`, `dotnet test`, or `scripts/ci.ps1` because no .NET SDK/PowerShell toolchain is installed. Syntax checks are not compilation or runtime evidence.

A maintainer reported that `scripts/ci.ps1` and the packaging script passed on Windows at commit `398044e`. The installed 0.1.0 bridge subsequently loaded, but export failed with an assembly-load exception for `System.Text.Json` 9.0.0.0 or one of its dependencies. Version 0.1.1 removes that dependency from the export path and adds .NET Framework smoke coverage. At `9ece548`, Windows CI reached the new smoke step but PowerShell reflection rejected a wrapped path argument before publication; the smoke invocation was subsequently corrected.

A maintainer subsequently reported that the default snapshot exists and the verifier introduced in `68ecd29` read four records through the adapter with no skipped records, staleness, or issue. An immediate second read used the cache; an explicit refresh read the file and returned the same IDs.

A later manual Playnite session verified the live bridge behaviours: renaming a game refreshed the snapshot while preserving its ID; adding and removing a temporary game added and removed the corresponding record; changing source/platform metadata refreshed the matching record; and, with Playnite closed and the snapshot moved aside, a fresh verifier process reported `Available: False`, `Game count: 0`, `Skipped records: 0`, `Stale: False`, `Issue: Missing`. Restarting Playnite recreated the snapshot successfully. These are maintainer-reported manual results; the Playnite/Windows versions were not recorded, and no personal library data was committed. This extends real-library evidence to event-driven refresh and missing-file recovery, but does not by itself prove metadata parity for every field or that no partial file can ever be observed.

A maintainer subsequently ran `scripts/package-playnite-plugin.ps1` on Windows. The published-bridge .NET Framework smoke test passed using synthetic data (with no `System.Text.Json` reference), and the script created `artifacts/playnite-plugin/PlayniteCmdPalBridge.pext`. The complete `dotnet build` and `dotnet test` summaries from that run have not yet been reviewed independently, so their outcomes remain unconfirmed here.

## Troubleshooting a missing snapshot

Playnite writes host loading errors to `playnite.log`, but messages and exceptions emitted by extension loggers go to `extensions.log`. For a normal per-user installation, both are under `%APPDATA%\Playnite`; portable installations and custom user-data directories use their Playnite data root instead.

Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\diagnose-playnite-bridge.ps1` from the repository root to inspect the installed files, snapshot state, and recent bridge log entries. The diagnostic does not print catalog contents, but logs can contain personal information; review and redact them before sharing.

## Manual Playnite verification still required

Automated tests use synthetic snapshots only. The following read-only verifier uses the real `PlayniteCatalogAdapter` and prints aggregate status and counts, not game names, IDs, or file contents:

```powershell
dotnet run --project .\tools\CatalogVerifier\CatalogVerifier.csproj --configuration Release
```

It checks an initial read, an immediate cached read, and an explicit refresh; valid empty libraries are supported. A differing ID set between the two reads can also mean the library changed concurrently. This check does not compare metadata with Playnite or demonstrate an event-driven refresh.

Before closing WERD-60's real-library acceptance criterion:

1. Install the packaged bridge in Playnite 10.63 or the supported version being tested.
2. Start Playnite with a small test library containing duplicate display names and at least one installed and one uninstalled game.
3. Confirm `%LOCALAPPDATA%\PlayniteCmdPal\catalog-v1.json` appears and contains only the allowlisted fields.
4. Run the verifier above or use the WERD-61 page; compare stable IDs, names, source/platform, and install state locally with Playnite without sharing library data.
5. Add/rename/remove a game and change source/platform metadata; confirm the snapshot refreshes after the debounce and that no partial file is observable.
6. Close Playnite, delete the snapshot, and confirm the adapter reports unavailable on a fresh process. Restart Playnite and confirm explicit refresh reconnects.
7. Record the Playnite/Windows versions and observed outcomes without committing game IDs, titles, or library exports.

The existing launch-by-ID URI smoke test is separate evidence from WERD-56; it does not verify this catalog bridge.
