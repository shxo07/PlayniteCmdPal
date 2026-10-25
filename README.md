# PlayniteCmdPal

Search and launch games from a Playnite library through Microsoft PowerToys Command Palette.

> Status: research / technical spike. Not yet a working extension.

## Goal
Provide a keyboard-first route to a user's unified Playnite library: open Command Palette, type a game name, press Enter, and let Playnite handle launch configuration.

## MVP
- Search Playnite game titles with deterministic ranking.
- Show useful metadata to disambiguate games.
- Launch through a verified Playnite-supported mechanism.
- Handle missing Playnite/integration and launch errors clearly.
- Automated core tests and reproducible Windows developer setup.

## Architecture
C# Command Palette extension using the official Microsoft template/toolkit. Playnite access stays behind an adapter. The transport decision is blocked on a proof of concept: prefer a documented external mechanism; if unavailable, evaluate a companion Playnite plugin plus local IPC. Do not make direct database reads or UI automation the production contract.

## Status
The current work is tracked in [Linear](https://linear.app/werdscopez7347/project/playnite-for-command-palette-9103e0746de7). The first priority is to verify the Playnite catalog/launch integration and scaffold the current CmdPal template.

## Docs
- [PRD](docs/PRD.md)
- [Technical design](docs/TECHNICAL-DESIGN.md)
- [Research](docs/RESEARCH.md)
- [Decisions](docs/DECISIONS.md)
- [Developer setup](docs/DEVELOPER-SETUP.md)

## Future ideas
A separate clock/timer Command Palette extension is intentionally out of scope for this repository.

## License
Not selected yet. Do not assume this repository is licensed for reuse until a license file is added.