# Architecture Decision Log

## ADR-001: Playnite remains the source of truth
**Status:** Accepted  
**Decision:** Use Playnite's unified library and configured launch behavior rather than integrating each storefront independently.  
**Reason:** Reduces duplicated integrations and keeps launch behavior aligned with the user's Playnite setup.

## ADR-002: C# and official CmdPal template
**Status:** Accepted, pending scaffold validation  
**Decision:** Build the extension using the official C# template and Microsoft.CommandPalette.Extensions.Toolkit.  
**Reason:** Native supported development path; target framework and package versions must come from the current generated template.

## ADR-003: No direct database writes or UI automation
**Status:** Accepted  
**Decision:** Never write to Playnite data. Do not use UI automation as the production integration.  
**Reason:** Data integrity and reliability.

## ADR-004: Playnite transport not yet decided
**Status:** Open  
**Options:** documented external API/URI/CLI; companion Playnite plugin with local IPC; temporary read-only DB prototype only for feasibility exploration.  
**Decision gate:** prove catalog retrieval and launch by stable game ID with documented support.  
**Owner:** project maintainer.  
**Tracking:** Linear issue WERD-56 — https://linear.app/werdscopez7347/issue/WERD-56/spike-verify-supported-playnite-catalog-and-launch-integration

## ADR-005: Clock/timer extension is separate
**Status:** Accepted  
**Decision:** Clock and timer functionality is out of scope for this repository and MVP. Treat it as a separate project.