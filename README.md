# PlayniteCmdPal

[![Windows CI](https://github.com/werdscopez7347/PlayniteCmdPal/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/werdscopez7347/PlayniteCmdPal/actions/workflows/ci.yml)
[![CodeQL](https://github.com/werdscopez7347/PlayniteCmdPal/actions/workflows/codeql.yml/badge.svg?branch=main)](https://github.com/werdscopez7347/PlayniteCmdPal/actions/workflows/codeql.yml)

Search and launch games from a Playnite library through Microsoft PowerToys Command Palette.

> **Status:** The generated Command Palette template has been validated, and the independent core game model and deterministic search implementation are complete. Windows CI and CodeQL run on pull requests and `main`, with verified successful runs. The catalog bridge (snapshot transport, adapter, and companion Playnite plugin) is implemented and has maintainer-reported Windows build/packaging and real-library verification; see [Catalog Bridge](docs/CATALOG-BRIDGE.md) for the exact evidence and remaining gaps. CmdPal search/launch UI and release packaging remain in progress. There are no releases yet.

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

- C# extension based on the current official Microsoft CmdPal template in `PlayniteCmdPal/`.
- Playnite remains the source of truth for library data and launch configuration.
- Keep the core game model and ranking logic independent from the CmdPal and Playnite SDKs.
- Catalog transport is decided in [ADR-005](docs/DECISIONS.md#adr-005-companion-plugin-json-snapshot-for-catalog-access): a companion Playnite plugin exports a normalized JSON snapshot that the read-only CmdPal adapter consumes.
- Do not use direct Playnite database writes or UI automation. Do not add IPC unless evidence shows it is needed.

## Current baseline

- Solution: `PlayniteCmdPal.sln`
- Generated project: `PlayniteCmdPal/PlayniteCmdPal.csproj`
- Catalog bridge: `PlayniteCmdPal.Snapshot`, `PlayniteCmdPal.Playnite`, and `PlayniteCmdPal.PlaynitePlugin`; see [Catalog Bridge](docs/CATALOG-BRIDGE.md)
- Central package versions: `Directory.Packages.props`
- Repo-specific CmdPal instructions: `.github/copilot-instructions.md` and `.github/instructions/cmdpal-extension.instructions.md`
- GitHub Actions: Windows build/test workflow and CodeQL workflow; Dependabot is configured for NuGet and Actions.
- Windows CI and CodeQL have successful runs. The core model/search tests run in CI; do not describe the repository as a working end-to-end extension until local deployment and Playnite integration acceptance criteria pass.
- **License:** MIT for PlayniteCmdPal-authored material; see [LICENSE](LICENSE). Third-party components and assets retain their own license and attribution requirements. See [licensing and release readiness](docs/LICENSING-AND-RELEASE.md).

## Development and project tracking

- [Developer setup](docs/DEVELOPER-SETUP.md)
- Run the same Windows restore/build/test checks as CI with `.\scripts\ci.ps1` from PowerShell on Windows with the .NET 10 SDK; see Developer Setup for details and verification status.
- [Contributing](.github/CONTRIBUTING.md)
- [Security policy](SECURITY.md)
- [Code of Conduct](CODE_OF_CONDUCT.md)

## Project docs

- [PRD](docs/PRD.md)
- [Technical design](docs/TECHNICAL-DESIGN.md)
- [Catalog Bridge](docs/CATALOG-BRIDGE.md)
- [Research](docs/RESEARCH.md)
- [Decisions](docs/DECISIONS.md)

## References

- [Create a Command Palette extension](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension)
- [Command Palette extensibility overview](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview)
- [Playnite command-line arguments and URI commands](https://api.playnite.link/docs/manual/advanced/cmdlineArguments.html)

A clock/timer Command Palette extension is a separate future project and intentionally out of scope here.
