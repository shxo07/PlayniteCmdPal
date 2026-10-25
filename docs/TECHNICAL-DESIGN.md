# Technical Design

Status: Proposed; catalog transport remains a decision gate. Last reviewed: 2026-10-25.

## Stack
- C#.
- Target framework and .NET SDK: use the current official Command Palette generated template and verify against installed stable PowerToys; do not guess versions.
- Microsoft.CommandPalette.Extensions and Microsoft.CommandPalette.Extensions.Toolkit.
- Visual Studio with workloads/components required by the generated template.
- Unit tests: use the template/repository convention or choose xUnit after scaffolding.
- GitHub Actions Windows runner for build/test.
- Later package via WinGet or Microsoft Store and submit to CmdPal gallery.

References:
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extension-development
- https://github.com/microsoft/PowerToys/tree/main/src/modules/cmdpal

## Layers
1. CmdPal presentation: provider, result list, actions, unavailable state.
2. Application core: search normalization/ranking, model mapping, cache policy.
3. Playnite catalog adapter contract.
4. Transport: documented external catalog API if available; otherwise companion Playnite plugin + local IPC.

Core model: stable ID, display name, optional source, optional install state, optional icon reference. Do not leak CmdPal SDK or Playnite SDK types into the core search layer.

## Launch integration
Official Playnite docs list --start <gameId> and playnite://playnite/start/<gameId>, with showgame and search URI commands. See https://api.playnite.link/docs/manual/advanced/cmdlineArguments.html. Verify these against the current stable version, including an already-running Playnite instance and portable installs. Use a URI or CLI launch request after validation; do not implement custom launch IPC unless testing proves it necessary.

## Catalog integration decision gate
Prove whether a supported external contract enumerates the current library. If none exists, prototype a companion Playnite plugin using SDK database access to expose read-only normalized records over a local named pipe with user/session ACLs. Verify plugin lifecycle, refresh/reconnect, Playnite closed/running, portable/custom paths, multiple profiles, and failure behavior.

Playnite's SDK IPlayniteAPI.Database and StartGame(Guid) are plugin-context APIs; they do not by themselves prove external-process access: https://api.playnite.link/docs/api/Playnite.SDK.IPlayniteAPI.html

Preferred order:
1. Documented external catalog API if found.
2. Companion plugin + local IPC for catalog; documented URI/CLI for launch.
3. Read-only database parsing only for temporary experiments, never production.
4. UI automation rejected.

## Proposed structure
- src/PlayniteCmdPal.Extension — CmdPal provider, results, actions.
- src/PlayniteCmdPal.Core — game model, search/ranking, interfaces.
- src/PlayniteCmdPal.PlayniteBridge — only if spike requires a companion plugin.
- tests/PlayniteCmdPal.Core.Tests
- tests/PlayniteCmdPal.Integration.Tests
- docs/PRD.md, TECHNICAL-DESIGN.md, RESEARCH.md, DECISIONS.md, DEVELOPER-SETUP.md
- .github/workflows/ci.yml, .editorconfig, .gitignore, README.md

Do not create the bridge project until the spike proves it is needed.

## Security and reliability
- Never accept arbitrary shell commands over IPC.
- Validate game IDs against the catalog before launch.
- If using named pipes, restrict access to current user/session and cap message sizes.
- No network listener, telemetry, or cloud dependency.
- Never write to Playnite data.
- Distinguish request sent, accepted, failed, and unknown launch outcomes.

## Testing
Unit tests for exact/prefix/word-prefix/substring ranking, tie-breaks, duplicates and empty query. Contract tests for catalog serialization if IPC is used. Integration tests for Playnite absent/stopped, empty library, unavailable game, URI/CLI launch behavior and errors. Benchmark a synthetic 10k-game catalog. CI must not require Playnite for core tests.

## Developer setup
1. Windows 11; current stable PowerToys with CmdPal enabled.
2. Run CmdPal's built-in Create extension command.
3. Install Visual Studio workloads/components required by the generated template.
4. Build and run the untouched generated extension.
5. Record actual target framework, SDK versions, packaging and debugging steps.
6. Add core search tests.
7. Install Playnite and create a test library with multiple sources and similarly named games.
8. Run the catalog/launch spike before committing to a bridge.

## Open decisions
Catalog transport and bridge lifecycle; minimum supported versions; alias/fuzzy matching; packaging and WinGet manifest ownership; license. Do not reuse third-party branding assets without permission.