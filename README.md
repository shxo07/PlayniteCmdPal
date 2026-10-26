# PlayniteCmdPal

Search and launch games from a Playnite library through Microsoft PowerToys Command Palette.

> Status: generated Command Palette template is committed. Core search, Playnite catalog integration, and launch behavior are not implemented yet; the clean build/local deployment still need verification.

## Goal

Provide a keyboard-first route to a user's unified Playnite library: open Command Palette, type a game name, press Enter, and let Playnite handle launch configuration.

## MVP

- Search Playnite game titles with deterministic ranking.
- Show useful metadata to disambiguate games.
- Launch through a documented Playnite-supported mechanism using a stable game ID.
- Handle missing Playnite/integration, empty library, stale data, and launch errors clearly.
- Automated core/adapter tests and reproducible Windows developer setup.
- Windows CI that builds the generated solution.

## Architecture

- C# extension based on the current official Microsoft CmdPal template in `testExtension/`.
- Playnite remains the source of truth for library data and launch configuration.
- Keep the core game model and ranking logic independent from the CmdPal and Playnite SDKs.
- Put catalog access behind a read-only adapter. WERD-56 chooses the transport; if no documented external catalog API exists, evaluate a companion Playnite plugin that exports a normalized JSON snapshot.
- Do not use direct Playnite database writes or UI automation. Do not add IPC unless evidence shows it is needed.

## Current baseline

- Solution: `testExtension.sln`
- Generated project: `testExtension/testExtension.csproj`
- Central package versions: `Directory.Packages.props`
- Repo-specific CmdPal instructions: `.github/copilot-instructions.md` and `.github/instructions/cmdpal-extension.instructions.md`
- Build/deploy and integration are not yet verified; do not describe this repository as a working extension until WERD-57 and WERD-56 acceptance criteria are met.
- License not selected yet. Do not assume the repository is licensed for reuse until a license file is added.

## Project tracking

Work, dependencies, milestones, acceptance criteria, and authoritative links are tracked in [Linear](https://linear.app/werdscopez7347/project/playnite-for-command-palette-9103e0746de7). The [Implementation Roadmap & Engineering Workflow](https://linear.app/werdscopez7347/document/implementation-roadmap-and-engineering-workflow-aa55b062593b) is the project execution overview.

## Docs

- [PRD](docs/PRD.md)
- [Technical design](docs/TECHNICAL-DESIGN.md)
- [Research](docs/RESEARCH.md)
- [Decisions](docs/DECISIONS.md)
- [Developer setup](docs/DEVELOPER-SETUP.md)

## References

- [Create a Command Palette extension](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension)
- [Command Palette extensibility overview](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview)
- [Playnite command-line arguments and URI commands](https://api.playnite.link/docs/manual/advanced/cmdlineArguments.html)

A clock/timer Command Palette extension is a separate future project and intentionally out of scope here.
