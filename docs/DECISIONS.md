# Architecture Decision Log

## ADR-001: Playnite remains the source of truth
**Status:** Accepted  
Use Playnite's unified library and configured launch behavior rather than integrating each storefront independently.

## ADR-002: C# and official CmdPal template
**Status:** Accepted, pending scaffold validation  
Use the official C# template and Microsoft.CommandPalette.Extensions.Toolkit. Target framework and package versions must come from the current generated template.

## ADR-003: No direct database writes or UI automation
**Status:** Accepted  
Never write to Playnite data. Do not use UI automation as the production integration.

## ADR-004: Use Playnite's documented launch interface
**Status:** Proposed, requires smoke test  
Official docs list --start <gameId> and playnite://playnite/start/<gameId>: https://api.playnite.link/docs/manual/advanced/cmdlineArguments.html. Verify current stable behavior, then use URI or CLI for launch rather than a custom launch protocol.

## ADR-005: Catalog transport remains open
**Status:** Open  
First check for a documented external catalog API. If none exists, prototype a companion Playnite plugin exposing read-only normalized game records over local IPC. Direct database parsing is prototype-only; UI automation is rejected.  
Tracking: Linear issue WERD-56 — https://linear.app/werdscopez7347/issue/WERD-56/spike-verify-supported-playnite-catalog-and-launch-integration

## ADR-006: Clock/timer extension is separate
**Status:** Accepted  
Clock and timer functionality is out of scope for this repository and MVP.