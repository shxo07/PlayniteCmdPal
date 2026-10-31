---
name: publish-extension
description: >-
  Prepare and review release packaging and Command Palette Gallery materials
  for PlayniteCmdPal. Use when asked about release readiness, MSIX packaging,
  Microsoft Store, WinGet, or Gallery submission. Do not publish without
  explicit maintainer approval and cleared licensing gates.
---

# PlayniteCmdPal release preparation

Start with [the project-specific release and Gallery guide](../../../docs/RELEASE-PACKAGING.md). It is the source of truth for the current artifact layout, selected distribution direction, release blockers, install/update/uninstall guidance, and approval checklist.

## Project-specific constraints

- The Command Palette extension is the MSIX-packaged out-of-process COM server in `PlayniteCmdPal/`.
- The companion Playnite `.pext` produced by `scripts/package-playnite-plugin.ps1` is a separate catalog bridge. It is not the CmdPal installer and must not be submitted as the Gallery install source.
- The current investigation is Store-first because Microsoft recommends Store distribution for Command Palette extensions. This is not approval to publish: Windows SDK projection redistribution, stable SDK targeting, runtime notices, package identity/signing, and clean-machine validation remain gates.
- Do not use the old generic Inno Setup recipe to create an unpackaged EXE. Its COM registration example does not match this project's packaged out-of-process `com:ExeServer`.
- The Gallery is a catalogue, not a binary host. A listing needs a live Microsoft Store or WinGet install source before submission.
- The Gallery contribution is a separate PR to `microsoft/CmdPal-Extensions` targeting that repository's `main`. This project's code PRs target this repository's `dev`.

## Safe workflow

1. Read `docs/LICENSING-AND-RELEASE.md` and `docs/RELEASE-PACKAGING.md`.
2. Verify the current official PowerToys publishing and Gallery contribution documentation; re-check the live Gallery schema before finalizing metadata.
3. Use the existing MSIX project and architecture publish profiles. Do not change packaging mode or identity based on a generic example.
4. Run `.\scripts\ci.ps1` on Windows, then perform the separate x64/ARM64 package, signature, contents, and clean-machine checks listed in the release guide.
5. Keep the Gallery draft unlisted until a real install source exists. Replace its placeholders only after the maintainer confirms the publisher identity and source ID.
6. Report exact commands and observed results. Never describe an unrun package build, installation, upgrade, uninstall, or rollback test as passing.
7. Do not reserve Store identity, accept legal terms, sign with a private certificate, publish a release, or submit to the Gallery without explicit maintainer approval.

## References

- [Microsoft: Publish Command Palette extensions](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/publish-extension)
- [Microsoft: Making your extension discoverable](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extension-gallery)
- [Gallery contribution guide](https://github.com/microsoft/CmdPal-Extensions/blob/main/docs/CONTRIBUTING.md)
- [Gallery extension schema](https://github.com/microsoft/CmdPal-Extensions/blob/main/.github/schemas/extension.schema.json)
