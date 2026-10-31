# Agent and AI-Assisted Development Guidance

This file is the repository-wide starting point for coding agents and AI-assisted contributors. Follow it together with the human contribution guide and the more specific CmdPal instructions.

## Before changing code

1. Read `README.md`, `docs/PRD.md`, `docs/TECHNICAL-DESIGN.md`, `docs/DECISIONS.md`, and the relevant sections of `docs/RESEARCH.md`.
2. Read `.github/copilot-instructions.md` and `.github/instructions/cmdpal-extension.instructions.md` for CmdPal-specific code.
3. Read relevant files under `.github/skills/` when working on the corresponding workflow.
4. Check Linear (and relevant GitHub issues and pull requests) for issue scope, acceptance criteria, dependencies, and existing decisions.

## Non-negotiable project constraints

- Playnite is the source of truth for library identity, metadata, and configured launch behavior.
- Use the checked-in generated Microsoft CmdPal template. Do not regenerate it as a substitute for understanding it.
- Follow the current official CmdPal SDK patterns in the generated project and official docs.
- Keep core game/search logic independent of CmdPal and Playnite SDK types.
- Never write to Playnite's database or use UI automation as a production integration.
- Do not assume a general external Playnite catalog API exists merely because plugin APIs expose the database.
- Do not add named-pipe IPC or another transport without a recorded decision justifying it.
- Do not commit real game-library exports, private game metadata, credentials, local machine paths, or build output.
- The clock/timer idea is out of scope for this repository.

## Engineering workflow

- Make focused, issue-linked changes; avoid unrelated refactors.
- Add or update tests for behavior changes.
- Run the documented restore/build and relevant tests when possible; report commands and failures accurately.
- Never claim a build, test, or manual smoke test passed unless it actually ran.
- Update `docs/DECISIONS.md` for architectural decisions and `docs/RESEARCH.md` when evidence changes.
- Keep README status honest until clean build, local deployment, and Playnite integration have been verified.
