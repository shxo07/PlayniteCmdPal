# Search filters

The **Filters** row opens a keyboard-first `ListPage`. Type to narrow the option rows and navigate with the keyboard. Each change applies immediately while the picker remains open. Press Escape to return to results without changing selections. The picker stays focused on choices and clear actions; the Filters entry subtitle gives a compact description of the available dimensions. Source and platform choices can be selected independently.

A fresh filter state selects **Installed**. Installed and Not installed can be selected independently: selecting neither or both removes the installation-status restriction. Clear status, Clear sources, Clear platforms, and Clear all remove only their named restrictions and never re-enable Installed. Source/platform selections persist across picker reopenings and catalog refreshes while the metadata value remains available. A temporary unavailable catalog does not clear selections; a successful refresh removes selections whose metadata is no longer present.

The checked and unchecked appearances are glyphs on list items, as explicitly authorized for WERD-72 to override the issue's earlier preference for native CmdPal checkbox controls. These are **not native checkbox controls** and do not claim checkbox-specific accessibility semantics.

## Verification checklist

Automated core tests cover fresh defaults, clearing, multi-select OR/AND filtering, both/neither installation statuses, missing metadata, empty queries, no matches, stale metadata, and deterministic ranking after filtering. The host-specific interaction still needs Windows Command Palette verification:

- Type a partial source/platform name; verify options narrow without leaving the picker.
- Use only keyboard navigation; verify repeated toggles keep the picker open, preserve focus/search text, and change the check glyph immediately.
- Verify clear actions update all visible glyphs and the Filters active-count indicator; verify Escape returns to results without undoing state.
- Refresh the catalog while Filters is open; verify current valid choices remain, stale metadata is removed, and the search text/focus remain usable.
- Confirm the glyphs render in the pinned Command Palette SDK/host version.

Windows UI/keyboard verification and a build against the pinned Windows SDK could not be run in the current non-Windows environment (the .NET SDK is not installed here). They remain required before merge.
