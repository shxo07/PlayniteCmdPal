# Product Requirements Document

Status: Draft v0.1 — subject to technical spike. Last reviewed: 2026-10-25.

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