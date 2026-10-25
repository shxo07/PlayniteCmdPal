# Technical Design

Status: Proposed; integration transport is a decision gate. Last reviewed: 2026-10-25.

## Stack
- C#.
- Target framework and .NET SDK: take from the current official Command Palette generated template and verify against the installed stable PowerToys release; do not guess versions.
- Microsoft.CommandPalette.Extensions and Microsoft.CommandPalette.Extensions.Toolkit.
- Visual Studio with workloads/components required by the generated template.
- Unit tests: use existing template/repository convention or select xUnit after scaffolding.
- GitHub Actions Windows runner for build/test.
- Package for WinGet or Microsoft Store when stable; then submit to CmdPal gallery.

References:
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extension-development
- https://github.com/microsoft/PowerToys/tree/main/src/modules/cmdpal

## Layers
1. CmdPal presentation: provider, result list, actions, unavailable state.
2. Application core: search normalization/ranking, model mapping, cache policy.
3. Playnite adapter contracts: catalog and launcher interfaces.
4. Transport: choose after spike; documented external protocol or companion plugin bridge.

The core model should contain stable ID, display name, optional source, optional install state, and optional icon reference. Do not leak CmdPal SDK or Playnite SDK types into the core search layer.

## Integration decision gate
Prove whether a supported external contract can enumerate the library and launch by stable game ID while preserving Playnite's configured behavior. Check CLI/URI docs, lifecycle, Playnite closed/running, portable/custom paths, elevation and profiles.

Preferred order:
1. Documented external API/protocol supporting catalog and launch.
2. Companion plugin using Playnite SDK; local named pipe with user/session ACLs.
3. Read-only database parsing only for temporary experiments, never production.
4. UI automation rejected.

Playnite SDK documents IPlayniteAPI.Database and StartGame(Guid gameId). Those APIs exist in Playnite's plugin context; this does not prove external-process access: https://api.playnite.link/docs/api/Playnite.SDK.IPlayniteAPI.html

## Proposed structure
- src/PlayniteCmdPal.Extension — CmdPal provider, results, actions.
- src/PlayniteCmdPal.Core — game model, search/ranking, interfaces.
- src/PlayniteCmdPal.PlayniteBridge — only if the spike requires a companion plugin.
- tests/PlayniteCmdPal.Core.Tests
- tests/PlayniteCmdPal.Integration.Tests
- docs/PRD.md, TECHNICAL-DESIGN.md, RESEARCH.md, DECISIONS.md, DEVELOPER-SETUP.md
- .github/workflows/ci.yml, .editorconfig, .gitignore, README.md

Do not create the bridge project until the spike proves it is needed.

## Security and reliability
- Never accept arbitrary shell commands over IPC.
- Validate requested game IDs against the catalog before launch.
- If using named pipes, restrict access to current user/session and cap message sizes.
- No network listener, telemetry, or cloud dependency.
- Never write to Playnite data.
- Distinguish request sent, accepted, failed, and unknown launch outcomes.

## Testing
Unit tests for exact/prefix/word-prefix/substring ranking, tie-breaks, duplicates and empty query. Contract tests for catalog serialization and launch messages if IPC is used. Integration tests for Playnite absent/stopped, empty library, unavailable game, and launch failures. Benchmark a synthetic 10k-game catalog. CI must not require Playnite for core tests.

## Open decisions
Integration transport and bridge lifecycle; minimum supported versions; alias/fuzzy matching; packaging and WinGet manifest ownership; license. Do not reuse third-party branding assets without permission.