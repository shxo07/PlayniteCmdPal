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
