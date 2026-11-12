# PlayniteCmdPal

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="PlayniteCmdPal-Brand-Assets/svg/wordmark-horizontal-dark.svg">
  <img alt="PlayniteCmdPal" src="PlayniteCmdPal-Brand-Assets/svg/wordmark-horizontal.svg">
</picture>

[![Windows CI](https://github.com/werdscopez7347/PlayniteCmdPal/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/werdscopez7347/PlayniteCmdPal/actions/workflows/ci.yml)
[![CodeQL](https://github.com/werdscopez7347/PlayniteCmdPal/actions/workflows/codeql.yml/badge.svg?branch=main)](https://github.com/werdscopez7347/PlayniteCmdPal/actions/workflows/codeql.yml)

Search and launch games from a Playnite library through Microsoft PowerToys Command Palette.

> **Status:** The generated Command Palette template has been validated, and the independent core game model and deterministic search implementation are complete. Windows CI and CodeQL run on pull requests and `main`, with verified successful runs. The catalog bridge (snapshot transport, adapter, and companion Playnite plugin) is implemented and has maintainer-reported Windows build/packaging and real-library verification; see [Catalog Bridge](docs/CATALOG-BRIDGE.md) for the exact evidence and remaining gaps. CmdPal search and launch are implemented and merged through WERD-61 / [PR #15](https://github.com/werdscopez7347/PlayniteCmdPal/pull/15); independent clean Windows 11 setup and integration validation remains outstanding ([WERD-69](https://linear.app/werdscopez7347/issue/WERD-69/run-clean-windows-11-setup-and-integration-validation)). Release preparation is documented in [Release packaging](docs/RELEASE-PACKAGING.md); the Gallery metadata is an unlisted draft and licensing/signing/package-validation gates remain open. There are no releases yet.

## Goal

Provide a keyboard-first route to a user's unified Playnite library: open Command Palette, type a game name, press Enter, and let Playnite handle launch configuration.

## Direct game results at the root (WERD-86)

Eligible games are registered as individual **top-level commands** through `TopLevelCommands()`, alongside Search games, Favorites, and Recently played games. Type a game title at Command Palette's home/root and select its **Open with Playnite** row. The row uses the saved Playnite game icon, or the neutral game symbol while unavailable/loading.

**This is not a fallback. No “Include in the Global result” setting is required.** The earlier fallback prototype has been replaced. CmdPal searches and ranks the registered games normally; the extension does not control first place or placement relative to other providers. As ordinary root commands, games may also appear in the host's unfiltered/home command catalog.

Catalog changes refresh automatically. To force a reread, select **Search games → More commands (`Ctrl+K`) → Refresh game commands**. Launch always revalidates the captured stable Playnite ID. Same-title games remain separate, explicitly selectable rows with source/platform/install metadata and distinct Playnite IDs. Unhealthy/stale snapshots do not authorize direct root commands; **Search games** remains the full browsing/recovery route. The maintainer has confirmed root results and supplied a screenshot with icons, but also reported two identical Playnite rows. The initial catalog is now prepared before exposing the provider to avoid a startup-registration race; this duplicate-row fix and real-game launch/lifecycle checks still need desktop verification. See [the supported API, safety rules, and host checks](docs/CMDPAL-GLOBAL-SEARCH.md).

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
- Windows CI and CodeQL have successful runs. All three test projects (`PlayniteCmdPal.Core.Tests`, `PlayniteCmdPal.Playnite.Tests`, and `PlayniteCmdPal.Command.Tests`) run in CI through `scripts/ci.ps1` alongside the .NET Framework snapshot smoke test; see [Testing](docs/TESTING.md) for the coverage map. Do not describe the repository as a working end-to-end extension until local deployment and Playnite integration acceptance criteria pass.
- **License:** MIT for PlayniteCmdPal-authored material; see [LICENSE](LICENSE). Third-party components and assets retain their own license and attribution requirements; distribution artifacts carry [third-party notices](third-party/THIRD-PARTY-NOTICES.md). See [licensing and release readiness](docs/LICENSING-AND-RELEASE.md) for the dependency audit and its open review items.

## Development and project tracking

- [Developer setup](docs/DEVELOPER-SETUP.md): clean Windows setup, Visual Studio/`.NET 10` prerequisites, deploy/reload/debug loop, and troubleshooting.
- [Testing](docs/TESTING.md): what the automated suites cover, which checks run in CI versus requiring a local Playnite/PowerToys install, and how to run one project or one test.
- [Release packaging](docs/RELEASE-PACKAGING.md): project-specific MSIX distribution decision, Gallery metadata draft, installation/update/uninstall guidance, and release blockers.
- [Release notes template](docs/RELEASE-NOTES-TEMPLATE.md): checklist-backed template for a future public release.
- Run the same Windows restore/build/test checks as CI with `.\scripts\ci.ps1` from PowerShell on Windows with the .NET 10 SDK; the script is the single source of truth for the automated steps.
- [Contributing](.github/CONTRIBUTING.md)
- [Security policy](SECURITY.md)
- [Code of Conduct](CODE_OF_CONDUCT.md)

## Project docs

- [Branding and asset integration](docs/BRANDING.md)
- [Command Palette extension metadata](docs/CMDPAL-EXTENSION-METADATA.md)
- [PRD](docs/PRD.md)
- [Technical design](docs/TECHNICAL-DESIGN.md)
- [Catalog Bridge](docs/CATALOG-BRIDGE.md)
- [Testing](docs/TESTING.md)
- [Research](docs/RESEARCH.md)
- [Decisions](docs/DECISIONS.md)
- [Release packaging](docs/RELEASE-PACKAGING.md)

## References

- [Create a Command Palette extension](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension)
- [Command Palette extensibility overview](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview)
- [Playnite command-line arguments and URI commands](https://api.playnite.link/docs/manual/advanced/cmdlineArguments.html)

A clock/timer Command Palette extension is a separate future project and intentionally out of scope here.
