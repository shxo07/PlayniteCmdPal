# Product Requirements Document

Status: Draft v0.1 — overall product scope remains subject to ongoing validation. WERD-80 artwork requirements reviewed: 2026-11-11; WERD-86 fallback-results requirements updated: 2026-11-12.

## Summary
A PowerToys Command Palette extension that searches a user's Playnite library and launches a selected game without switching to the Playnite window.

## Problem
Users with games across storefronts, local games, and emulators rely on Playnite to consolidate their library. Starting a game still requires navigating a separate UI or searching for an executable that may not preserve Playnite's configured launch behavior.

## Principles
- Playnite remains the source of truth for library and launch configuration.
- Keyboard-first: open palette, type, Enter.
- Fast and offline-capable after local data is available.
- No store-specific integration unless Playnite cannot represent a required capability.
- Fail safely with actionable errors.
- No cloud account requirement.

## Target users
Playnite users who prefer keyboard workflows, especially users with libraries across multiple storefronts.

## MVP user stories
1. Search by game title and see matching Playnite library entries.
2. Select a result and launch it through a verified Playnite-supported mechanism.
3. Distinguish similarly named games using available source/platform and installation metadata.
4. Receive a clear error if Playnite is missing, the bridge is unavailable, or launch fails.
5. Run core search tests without a Playnite installation.

## MVP scope
Included: read-only metadata, case-insensitive title search, deterministic ranking, launch by stable ID, local cache with defined refresh behavior, diagnostic logs without personal-data collection, setup/troubleshooting docs, automated tests.

Excluded: storefront-specific integrations, install/uninstall, library editing, remote control, network server, cloud sync/accounts/analytics, clock/timer features, dashboards, recommendations, achievements, and playtime analytics.

## UX requirements
- Result title is the game's display name.
- Subtitle includes only useful disambiguation metadata.
- Enter launches the selected game.
- Avoid duplicate results; one canonical entry per Playnite game ID.
- Empty, unavailable, and error states explain recovery.
- Keyboard-only use must be supported.

## Functional requirements
- Detect whether the Playnite integration is available.
- Retrieve library metadata without mutation.
- Normalize to a project-owned model with ID, name, optional source, install state, and icon reference.
- Update results according to current CmdPal lifecycle.
- Dispatch launch by stable game ID, never display name.
- Distinguish accepted, failed, and unknown launch outcomes.
- Define cache refresh after the integration spike.
- Document supported standard, portable, and custom install scenarios.

## Non-functional requirements
- No writes to Playnite data.
- No admin privileges for normal use.
- No listening network port.
- Any IPC is local-only, access-controlled, and validates message size and shape.
- Deterministic, unit-testable search; no blocking UI thread.
- Document tested Windows, PowerToys, and Playnite versions.
- No telemetry by default.

## Success criteria
A developer can set up from a clean environment; a real Playnite library returns correct results and Enter launches the intended game through Playnite; search/adapter logic has automated tests; missing integration and launch errors are handled; packaging and gallery submission are documented.

## Risks
- Unsupported external Playnite API: prove feasibility first; use companion plugin only if needed.
- CmdPal API changes: official template/toolkit, pinned versions, isolate CmdPal types.
- Large libraries: cache and benchmark.
- Duplicate titles: show metadata and use stable IDs.
- Install friction: aim for one package; otherwise document component health.

## Release plan
Alpha: local search and launch. Beta: packaging, custom paths, failure states, feedback. v1: stable release, WinGet package, gallery listing. Later features only when requested and supported.

## Acceptance tests
Exact title ranks first; prefix/substring results are deterministic; duplicate titles are distinguishable; launch uses stable ID; missing integration/empty library/stale cache/errors are handled; tests pass in CI; no Playnite data writes.
## Game artwork in Command Palette results (WERD-80)

**Decision:** Search results display the saved per-game icon managed by Playnite. If that icon is absent, unreadable, unsupported, or invalid, the result displays the existing neutral game symbol. A cover image, background image, promotional image, storefront image, or launcher/application icon is never substituted.

**Source of truth and management**
- Playnite remains the source of truth. The extension reads the saved per-game icon reference from Playnite library metadata; it does not inspect the game's installation directory or executable to discover an icon.
- Users manage missing or incorrect icons in Playnite. After Playnite saves an icon and the companion plugin refreshes the catalog, the extension can pick up the new content.
- Installed state does not determine whether an icon is eligible. Installed and uninstalled library entries follow the same saved-icon policy.
- No extension-side online lookup, third-party artwork provider, API key, account, hosted proxy, or network request is part of this scope.

**Presentation and responsiveness**
- The search result is available immediately with a neutral fallback; image resolution and decoding happen asynchronously.
- Artwork loading must not block typing, ranking, keyboard navigation, or launch.
- Loading is bounded and cancellable. Shared in-flight work must remain available while another active result still needs it; successful decoded results may be cached within a bounded policy.
- Catalog refresh must invalidate stale artwork so changed Playnite icon bytes do not leave an old image displayed.

**Transport and format policy**
- Reuse WERD-71's opaque SHA-256 artwork transport. The snapshot exposes an optional digest, never a Playnite reference, source path, absolute path, or URL.
- Playnite-managed PNG, JPEG, GIF, and WebP sources are accepted by the transport based on signature. ICO is accepted as a source only, structurally validated, decoded within the producer's 256×256 icon bound, and converted to PNG before caching.
- Missing, malformed, unsupported, inaccessible, oversized, or undecodable artwork is an optional-field failure, not a catalog or launch failure.
- The cache and source-file limits documented in [Artwork Transport](ARTWORK-TRANSPORT.md) remain in force.

**Verification status (2026-11-11):** The maintainer confirmed one installed Steam library entry with a saved ICO-backed Playnite icon now displays its icon in Command Palette. Diagnostics showed the Playnite path resolved, ICO conversion succeeded, the converted PNG was published to the content-addressed cache, and the matching icon digest appeared in the v2 snapshot. This verifies one real-library ICO case; it does not establish all formats, uninstalled-game behavior, or high-DPI behavior. See [Testing](TESTING.md) for the manual verification log and remaining checks.

## Installed-game fallback result (WERD-86)

- Expose exactly one **Playnite** top-level command. Its single page provides in-page Search games, Recently played, and Favorites views; do not register the three views as separate root commands or register every Playnite game as a root command.
- Share installed-status, source, and platform filter selections across all three views; apply them consistently and persist them locally so they survive closing/reopening the page and restarting Command Palette.
- Expose a single query-reactive `FallbackCommandItem` through `FallbackCommands()`. It can show one direct-launch row when the query is an exact, case-insensitive match (after trimming surrounding query whitespace) to exactly one installed game title. Partial, unknown, and ambiguous installed-title matches produce no row.
- Exclude uninstalled and installation-unknown games from this global direct-launch fallback. The unified library page retains its existing browsing/filter behavior.
- Show the matched saved title, the subtitle/action **Open with Playnite**, and the saved Playnite game icon; use the neutral symbol while loading or when saved artwork is missing/invalid. Do not append PC, platform, source, installation-state, or other metadata labels to the fallback row.
- Fallback results occupy the host-managed **Fallbacks** section by default, separate from regular Results. An Apps-provider row may still rank above it. Do not claim or attempt control of global rank, adjacency, or another provider's results; do not enable a host option that merges fallbacks into regular Results by default.
- `playnite` should not display every game: the root provider contains navigation only, and the fallback returns no game unless an installed game title exactly matches the query. Preserve stable canonical Playnite IDs; revalidate the same installed ID and exact displayed title before dispatch. Never infer a game from a partial query or retarget another same-title ID.
- Reject unhealthy, stale, partial, invalid, or conflicting catalogs for fallback launch. Keep saved-icon resolution asynchronous, bounded, icon-only, cancellation/version guarded, and nonessential to launch.
- Synthetic tests must cover installed/uninstalled filtering, exact/no/ambiguous matching, stable-ID launch revalidation, asynchronous saved icons, fallback/provider registration, and race/disposal safety. Desktop fallback rendering/coexistence/icon scaling and actual launches remain separate manual checks.
