# Contributing

Contributions to PlayniteCmdPal are accepted under the repository's MIT License. By submitting a contribution, you agree that it may be distributed under those MIT terms. You must have the right to submit the contribution and must identify and preserve any third-party code, assets, notices or restrictions included with it. The project license does not relicense third-party material.

## Before starting

- Read [README](../README.md), [PRD](../docs/PRD.md), [technical design](../docs/TECHNICAL-DESIGN.md), [research](../docs/RESEARCH.md), and [decisions](../docs/DECISIONS.md).
- Check open GitHub issues and pull requests for scope, dependencies, and acceptance criteria.
- For CmdPal provider/page/action work, read `.github/copilot-instructions.md`, `.github/instructions/cmdpal-extension.instructions.md`, and the relevant guidance under `.github/skills/`.
- Avoid duplicating work already tracked in GitHub issues and pull requests.

## Workflow

1. Work from an issue with clear acceptance criteria.
2. Create a focused branch named after the GitHub issue number or a short description.
3. Keep each pull request focused on one coherent change.
4. Add tests for behavior changes and update documentation when decisions or developer workflows change.
5. Run restore/build and relevant tests locally.
6. Open a pull request using the repository template and link the related GitHub issue, if one exists.
7. Wait for Windows CI and security checks; address failures before merging.

## Engineering rules

- Playnite remains the source of truth for library data and launch behavior.
- Use the checked-in generated CmdPal template and current official Microsoft documentation.
- Keep core search logic independent of CmdPal and Playnite SDK types.
- Do not write to Playnite's database or use UI automation.
- Never commit credentials, real library exports, private game metadata, machine-specific paths, or build output.
- Record architecture decisions in `docs/DECISIONS.md` and new evidence in `docs/RESEARCH.md`.
- Do not include clock/timer features; they belong to a separate project.

## Local build

Use the SDK and commands documented in [Developer Setup](../docs/DEVELOPER-SETUP.md). The checked-in solution is `PlayniteCmdPal.sln`. If the generated template fails to build, report the exact command, SDK version, and sanitized error output rather than silently changing package versions.

## Security

Do not report exploitable vulnerabilities in public issues. Follow [SECURITY.md](../SECURITY.md).
