# Technical Design

**Status:** Generated CmdPal template build and local deployment validated on Windows. The running- and closed-instance launch URI smoke tests passed. The catalog adapter/bridge implementation is present. Windows CI and CodeQL passed, and the maintainer reports successful packaging and real Playnite library checks; see [Catalog Bridge](CATALOG-BRIDGE.md) for the evidence and remaining gaps.
**Last reviewed:** 2026-11-12.

## Current generated-template baseline

The current repository contains `PlayniteCmdPal.sln` and `PlayniteCmdPal/PlayniteCmdPal.csproj`. Do not regenerate the template.

Observed from checked-in project files:
- Target framework: `net10.0-windows10.0.26100.0`.
- Minimum/supported Windows platform: `10.0.19041.0`.
- Windows SDK package property: `10.0.26100.68-preview`.
- Runtime identifiers: `win-x64;win-arm64`.
- Build platforms: `x64;ARM64`.
- Central package management is enabled.
- `Microsoft.CommandPalette.Extensions` 0.9.260303001.
- `Microsoft.Windows.CsWinRT` 2.2.0.
- `Shmuelie.WinRTServer` 2.1.1.
- `Microsoft.Windows.SDK.BuildTools.MSIX` 1.7.20250829.1.
- Additional versions are pinned in `Directory.Packages.props`.

Sources: [project](../PlayniteCmdPal/PlayniteCmdPal.csproj), [package versions](../Directory.Packages.props), [shared build properties](../Directory.Build.props). These values describe the checked-in template; local restore/build/deployment validation is recorded below.

## Template validation update (2026-10-27)

The generated project was inspected without regeneration. Its current source pins `net10.0-windows10.0.26100.0`, Windows SDK package property `10.0.26100.68-preview`, minimum/supported platform `10.0.19041.0`, and RIDs `win-x64;win-arm64`. Shared build platforms are `x64;ARM64`. Central package versions are recorded in `Directory.Packages.props`; the primary extension packages are `Microsoft.CommandPalette.Extensions` 0.9.260303001, `Microsoft.Windows.CsWinRT` 2.2.0, `Shmuelie.WinRTServer` 2.1.1, and `Microsoft.Windows.SDK.BuildTools.MSIX` 1.7.20250829.1. Maintainer-reported SDK: .NET SDK 10.0.403. The solution metadata reports VS 17.13.35507.96 as generator environment, not as a verified minimum.

The initial build failure was `APPX1685` because `x-generate` selected default language `en-AE`. The source manifest now explicitly declares `<Resource Language="en-US"/>`; this valid language tag avoids dependence on the build machine locale while keeping manifest validation enabled. On the maintainer's Windows 10.0.19044 machine with .NET SDK 10.0.403, restore succeeded and x64/ARM64 builds succeeded both with and without `-p:DefaultLanguage=en-US` (all build commands exit code 0). Visual Studio package deployment succeeded; Command Palette was reloaded, the extension opened, and the placeholder page was confirmed. Exact Visual Studio version/workloads and PowerToys version were not recorded. See [developer setup](DEVELOPER-SETUP.md) for commands and detailed results.

Generated lifecycle inspected: the `IExtension` implementation returns a single `CommandProvider` for `ProviderType.Commands`; its provider returns a cached top-level command; the placeholder page is a static `ListPage` whose `GetItems()` returns one placeholder and which does not implement dynamic search. Microsoft documents `DynamicListPage.UpdateSearchText(oldSearch, newSearch)` for reactive filtering and `RaiseItemsChanged()` to notify the host after data changes. The official guide requires **Build > Deploy**, then **Reload > Reload Command Palette extensions**; the packaged launch profile is required, not the unpackaged profile.

## Stack and framework

- C# extension using the current official Microsoft Command Palette extension template.
- `Microsoft.CommandPalette.Extensions` is referenced directly. Use the exact generated project pattern and consult the current Microsoft guide before implementing providers, pages, result actions, or navigation.
- Keep the generated COM server/hosting, manifest, and MSIX packaging structure unless the official docs show a required change.
- Windows CI is defined in `.github/workflows/ci.yml`; it installs .NET 10 and invokes the shared `scripts/ci.ps1` checks. It does not require Playnite or private library data.
- Do not guess or casually upgrade template dependencies during MVP implementation.

References:
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extension-gallery
- https://github.com/microsoft/PowerToys/tree/main/src/modules/cmdpal

## Architecture and boundaries

1. **CmdPal presentation:** current-template command provider, search/results, actions, and clear unavailable/error states.
2. **Core:** game model, query normalization, deterministic ranking, and interfaces. No CmdPal or Playnite SDK types.
3. **Catalog adapter:** `PlayniteCmdPal.Playnite` strictly parses the read-only JSON snapshot, maps it into `PlayniteCmdPal.Core.GameEntry`, and owns the in-memory cache policy.
4. **Playnite bridge/transport:** `PlayniteCmdPal.PlaynitePlugin` exports an atomic, normalized JSON snapshot; `PlayniteCmdPal.Snapshot` holds the shared v1 DTOs, serializer, publisher, and export coordinator without SDK dependencies. See [ADR-005](DECISIONS.md#adr-005-companion-plugin-json-snapshot-for-catalog-access) for the canonical contract and [Catalog Bridge](CATALOG-BRIDGE.md) for implementation details.

Playnite remains the source of truth for game identity, library metadata, and launch configuration. The extension must not independently recreate storefront launch logic.

## Search and model

- Preserve Playnite's stable game ID; never use the display title as identity.
- Match case-insensitively.
- Deterministic rank order: exact match, prefix match, word-prefix match, substring match; stable ordinal tie-breaks include source, install state, icon reference, and platform. `GameEntry.Platform` carries the optional platform label separately from source.
- Handle duplicate display names and empty query/catalog explicitly.
- Keep search logic independently unit-testable.

### Installed-game fallback result (WERD-86)

The provider's `TopLevelCommands()` contains exactly one **Playnite** command. That command opens one `DynamicListPage` with in-page Search games, Recently played, and Favorites view selectors. The page owns one catalog reader, favorites store, artwork loader, and refresh action; changing views reuses those shared services rather than creating sibling pages. The provider still returns one stable `FallbackCommandItem` from `FallbackCommands()` and updates that item's title, command, and icon for the current query. The pinned SDK has a query callback on fallback items, but no provider-level method for injecting query-aware root results. The implementation does not put per-game commands in the root list or copy every game into separate fallback registrations.

The fallback uses `InstalledGameMatch` to scan the healthy catalog and find one unique case-insensitive exact title after trimming the query. Only `IsInstalled == true` is eligible. Uninstalled or unknown-installation entries are excluded; they do not make a same-title installed entry ambiguous. Two installed IDs with the same exact title are ambiguous and produce no direct-launch row. Invalid IDs, conflicting duplicate records, partial/unhealthy catalogs, and stale data fail closed. The unified page retains full-catalog search, local Favorites filtering/pin/unpin refresh, and Recently Played's authoritative newest-first `LastActivity` ordering, duplicate-title disambiguation, relative-time badges, and 10-second visible-page refresh polling. Search filters remain available in Search games. One page-level **Refresh Playnite catalog** context action force-refreshes the shared catalog and invalidates artwork; unavailable/empty-state messages point to that action rather than adding per-row refresh commands.

The fallback's visible title is the saved game title, its subtitle is only **Open with Playnite**, and its icon is the saved Playnite game icon or neutral fallback. No source, platform, install-state, or `PC` label is appended. `ArtworkIconLoader` resolves the icon reference asynchronously, outside the synchronous host query callback. Icon failure does not disable a valid result; a query change, launch, or disposal cancels/version-guards obsolete image work. Repeated or overlapping query callbacks cannot publish stale titles/icons, and SDK property notifications run outside the state lock so a host listener can re-enter safely.

Before launching, force-refresh the snapshot and require that the same canonical ID is still the unique exact-title match among installed games. Invocation cannot retarget another same-title ID. Dispatch reuses `LaunchGameCommand` and the stable `playnite://playnite/start/{id}` URI. The host owns placement: by default the result appears in the separate **Fallbacks** section, while an Apps match may remain in regular Results above it. The extension does not promote fallbacks, modify other providers, or promise adjacency. See [the global-search design and installed-host checklist](CMDPAL-GLOBAL-SEARCH.md).

## Playnite launch integration

Playnite documents `--start <gameId>` and `playnite://playnite/start/<gameId>`, as well as `showgame` and `search` URI commands:
https://api.playnite.link/docs/manual/advanced/cmdlineArguments.html

Use the documented URI as the primary launch request; a focused smoke test with a real game ID passed as described below. The purpose of the test is to validate our chosen invocation, not to re-prove that documented features exist. Do not launch the game's executable directly when Playnite should apply its configured launch behavior.

### Manual URI smoke test

1. On a Windows machine with Playnite installed, choose an installed game safe to start and obtain its **Playnite game GUID** (not a store ID) from a trusted Playnite-provided source, such as an existing game shortcut if it exposes the ID. Keep the ID and library details private.
2. With Playnite running, open PowerShell and enter the real game GUID when prompted:
   ```powershell
   $id = [guid](Read-Host 'Playnite game GUID')
   Start-Process "playnite://playnite/start/$($id.ToString('D'))"
   ```
3. Confirm that the intended game actually starts via Playnite. A successful `Start-Process` only shows that Windows accepted the request; it does not establish that the game launched.
4. Record the Playnite version, install mode, whether Playnite was running, and the observed outcome without committing the game ID or personal library data. If it fails, record the error and distinguish URI registration/dispatch from Playnite or game-launch failures.

**Result (user-reported, 2026-10-29):** With a normal installation of Playnite 10.63 already running, the URI test launched the intended Steam-installed game successfully. The user also reports that, with Playnite fully closed, including its system-tray process, the same URI reopened Playnite and then launched the selected game. These observations cover one local installation and game; they do not establish behavior across all systems or games. CLI fallback remains untested.

## Catalog integration decision

Playnite's plugin SDK exposes `PlayniteApi.Database.Games` to an in-process plugin. That does not by itself establish a general-purpose external-process catalog API:
- https://api.playnite.link/docs/api/Playnite.SDK.IPlayniteAPI.html
- https://api.playnite.link/docs/api/Playnite.SDK.IGameDatabase.html
- https://api.playnite.link/docs/tutorials/extensions/library.html

The accepted architecture uses a companion plugin that exports an atomic, normalized JSON snapshot. [ADR-005](DECISIONS.md#adr-005-companion-plugin-json-snapshot-for-catalog-access) is the canonical adapter contract: it specifies identity, schema, path, refresh, size limits, stale data, and error handling. The implementation in `PlayniteCmdPal.Snapshot`, `PlayniteCmdPal.Playnite`, and `PlayniteCmdPal.PlaynitePlugin` follows that contract; [Catalog Bridge](CATALOG-BRIDGE.md) documents the cache policy, refresh triggers, packaging, and manual verification. Windows CI and CodeQL passed, and the maintainer reports successful Windows packaging and real-library checks covering event-driven refresh and missing-file recovery. Field-by-field metadata parity remains to be independently checked. Do not add named pipes or another IPC protocol without evidence that the snapshot design is insufficient.

Rejected for production:
- Direct database writes.
- UI automation.
- Direct database parsing as the supported integration contract.
- Arbitrary shell commands or executable paths derived from untrusted catalog data.

## Current logical structure

The generated CmdPal project remains intact. The Command Palette presentation layer consumes the catalog adapter; the bridge does not implement search/launch UI.
- `PlayniteCmdPal/`: generated CmdPal presentation/hosting template.
- `PlayniteCmdPal.Core/`: SDK-independent game model and deterministic search.
- `PlayniteCmdPal.Snapshot/`: shared v1 snapshot contract, allowlisted serializer, atomic publisher, and debounced export coordinator.
- `PlayniteCmdPal.Playnite/`: strict parser and read-only adapter/cache exposing `IReadOnlyPlayniteCatalog`.
- `PlayniteCmdPal.PlaynitePlugin/`: Playnite companion plugin projecting the database into the snapshot.
- `PlayniteCmdPal.Core.Tests/` and `PlayniteCmdPal.Playnite.Tests/`: synthetic unit/contract tests. Both run without Playnite, PowerToys, a personal library, or secrets.
- Manual local smoke tests (real-library verification and launch-by-ID) remain separate from automated CI. See [Testing](TESTING.md).

## Security and reliability

- Treat catalog data as input; validate stable IDs and expected schema before launch.
- Never accept arbitrary shell commands over IPC.
- No network listener, telemetry, cloud dependency, or private game-library data in Git.
- Never write to Playnite's database.
- Distinguish missing catalog, stale catalog, malformed catalog, no matches, launch requested, and launch failure.
- Keep disk/network/process operations off the CmdPal UI thread.

## Testing and CI

For PR [#10](https://github.com/werdscopez7347/PlayniteCmdPal/pull/10), the final Windows CI and CodeQL runs on the reviewed head commit succeeded before merge:
- [Windows CI run #67](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/41016300900) — succeeded.
- [CodeQL run #67](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/41016300844) — succeeded.

Earlier fork-origin runs reported `action_required`; those were superseded by the successful runs above. PR #10 was squash-merged into `dev` as commit [`7b268d7`](https://github.com/werdscopez7347/PlayniteCmdPal/commit/7b268d79964cb3b83e76826e71b339f076c747a6) on 2026-10-30.

- Core tests: ranking order, case normalization, duplicates, tie-breaks, empty query/catalog, word-boundary rules, query trimming, returned-collection immutability, and single-enumeration behavior.
- Adapter/contract tests: valid, missing, malformed, stale, empty, refresh/reconnect, oversized, duplicate/invalid records, cancellation, coalesced reads, allowlist enforcement, limit boundaries, atomic publication and failure cleanup, and coordinator lifecycle, all using synthetic data only.
- Coverage is mapped file by file in [Testing](TESTING.md#coverage-map); the suites deliberately contain no environment-gated or skipped tests.
- Manual integration: real Playnite library and launch-by-ID behavior.
- Normal CI must not require Playnite or personal library data.
- Windows CI builds the generated solution and runs both `PlayniteCmdPal.Core.Tests` and `PlayniteCmdPal.Playnite.Tests` through `scripts/ci.ps1`; see [developer setup](DEVELOPER-SETUP.md). The current successful Windows CI run validates these implementation changes.

## Developer setup

See [DEVELOPER-SETUP.md](DEVELOPER-SETUP.md). Key loop:
1. Record `dotnet --info` and actual Visual Studio version/components.
2. Run the shared checks with `.\scripts\ci.ps1`, which performs restore, a Release build, both test projects, and the .NET Framework snapshot smoke test using the same commands as CI.
3. In Visual Studio use **Build > Deploy** to register the generated MSIX package.
4. Reload Command Palette extensions and debug using the generated launch settings.
5. Post a bug report or PR validation note with the exact versions and outcomes.

## Open decisions

- Confirm an independently reviewed Windows build/test run of the catalog bridge, and finish validating companion-plugin lifecycle/refresh events against a real Playnite library.
- Tested minimum PowerToys/Visual Studio/.NET SDK versions.
- Optional alias/fuzzy matching beyond title matching.
- Packaging and gallery submission route.
- Repository license.

## WERD-80: Playnite-managed game icons

**Status (2026-11-11):** Implemented on the WERD-80 PR branch and verified by automated Windows CI/CodeQL plus one maintainer-reported real-library ICO case. Broader manual coverage (including uninstalled entries and high-DPI scaling) remains outstanding.

### Data flow

1. The companion plugin reads the saved Game.Icon reference from Playnite game metadata and resolves it with Playnite's database API. It does not inspect install directories or executables.
2. The producer validates the game-owned database reference and resolved path against the WERD-71 trust boundary. Raw paths, URLs, traversal, mismatched game directories, reparse points, unsupported extensions, and invalid file sizes are rejected.
3. Supported raster files are validated by signature and copied into the bridge-owned content-addressed cache. ICO is source-only: the producer validates the ICO directory and entry ranges, converts a bounded icon to PNG, validates the PNG output, and hashes/caches the converted bytes.
4. The v2 snapshot carries an optional iconArtworkId digest. It does not carry the original Playnite reference, source path, or resolved path.
5. The read-only catalog adapter maps the digest into the game model. The Command Palette artwork loader resolves only the icon digest; it does not read coverArtworkId as a fallback candidate.
6. The UI displays the neutral game symbol immediately, then asynchronously resolves and decodes the saved icon. Missing or failed artwork leaves the neutral symbol in place and does not affect search or launch.

### Consumer behavior

- Saved Playnite per-game icon is the sole artwork source for Search and Favorites results.
- Cover, background, promotional, storefront, and launcher/application artwork are not substitutes.
- Loading uses bounded concurrency and a bounded successful-result cache. Concurrent requests for the same icon share in-flight work; cancellation of one waiter must not cancel work still required by another waiter.
- Explicit catalog refresh invalidates prior artwork resolution so new content hashes can be loaded. Transient resolver/decoder failures are retryable rather than permanently cached as missing.
- Image work remains outside the synchronous search/ranking path.

### Resource and trust boundaries

The producer's documented source-file, ICO dimension, and active-generation cache limits remain authoritative in [Artwork Transport](ARTWORK-TRANSPORT.md). The consumer accepts only canonical lowercase SHA-256 identifiers and resolves files beneath the fixed cache root (or the matching producer rollback directory during publication). It validates local cache candidates before decoding. The adapter remains read-only; Playnite's database is never modified.

### Verification evidence and limits

On 2026-11-11, one installed Steam game with an ICO-backed saved Playnite icon was used for real-library verification. The plugin log showed the resolved icon path; the snapshot contained the converted PNG's digest; the corresponding PNG existed in the cache; and the maintainer confirmed the icon displayed in Command Palette after the updated extension/catalog was loaded. This demonstrates the end-to-end path for that one ICO-backed entry, not universal library coverage.

Automated tests cover icon-only selection, no-cover-fallback behavior, fallback on missing/invalid artwork, opaque identifier validation, async loading, bounded caching, cancellation/coalescing, refresh invalidation, producer validation, and diagnostic reporting. CI results are recorded in [Testing](TESTING.md). Real-library tests for uninstalled entries, icon replacement, additional formats, and normal/high-DPI layouts remain manual follow-ups.