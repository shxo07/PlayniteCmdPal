# PlayniteCmdPal

Search and launch games from a Playnite library through Microsoft PowerToys Command Palette.

> **Status:** The generated Command Palette template is committed. Core search and Playnite integration are under development. CI/security workflows are configured, but their first successful runs and the clean local build/deploy loop still need verification.

## Goal

Provide a keyboard-first route to a user's unified Playnite library: open Command Palette, type a game name, press Enter, and let Playnite handle launch configuration.

## MVP

- Search Playnite game titles with deterministic ranking.
- Show useful metadata to disambiguate games.
- Launch through a documented Playnite-supported mechanism using a stable game ID.
- Handle missing Playnite/integration, empty library, stale data, and launch errors clearly.
- Automated core/adapter tests and reproducible Windows developer setup.
- Windows CI and static security analysis.

## Architecture

- C# extension based on the current official Microsoft CmdPal template in `testExtension/`.
- Playnite remains the source of truth for library data and launch configuration.
- Keep the core game model and ranking logic independent from the CmdPal and Playnite SDKs.
- Put catalog access behind a read-only adapter. If no documented external catalog API exists, evaluate a companion Playnite plugin that exports a normalized JSON snapshot.
- Do not use direct Playnite database writes or UI automation. Do not add IPC unless evidence shows it is needed.

## Current baseline

- Solution: `testExtension.sln`
- Generated project: `testExtension/testExtension.csproj`
- Central package versions: `Directory.Packages.props`
- Repo-specific CmdPal instructions: `.github/copilot-instructions.md` and `.github/instructions/cmdpal-extension.instructions.md`
- GitHub Actions: Windows build workflow and CodeQL workflow; Dependabot is configured for NuGet and Actions.
- The first workflow runs have not yet been verified. Do not describe the repository as a working extension until the build, local deployment, and Playnite integration acceptance criteria pass.
- License not selected yet. Do not assume the repository is licensed for reuse until a license file is added.

## Development and project tracking

- [Developer setup](docs/DEVELOPER-SETUP.md)
- [Contributing](.github/CONTRIBUTING.md)
- [Security policy](SECURITY.md)
- [Code of Conduct](CODE_OF_CONDUCT.md)

## Project docs

- [PRD](docs/PRD.md)
- [Technical design](docs/TECHNICAL-DESIGN.md)
- [Research](docs/RESEARCH.md)
- [Decisions](docs/DECISIONS.md)

## References

- [Create a Command Palette extension](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension)
- [Command Palette extensibility overview](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview)
- [Playnite command-line arguments and URI commands](https://api.playnite.link/docs/manual/advanced/cmdlineArguments.html)

A clock/timer Command Palette extension is a separate future project and intentionally out of scope here.
