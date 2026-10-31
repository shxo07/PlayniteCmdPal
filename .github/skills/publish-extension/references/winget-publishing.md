# WinGet distribution — not selected for the current project

**Status: not approved; do not use the former generic Inno Setup recipe.**

Microsoft documents WinGet as a supported Command Palette distribution source. A WinGet package is discoverable in Command Palette when its locale manifest includes the `windows-commandpalette-extension` tag. The Gallery also accepts a WinGet package identifier as an install source.

That does not mean the current generic EXE/installer example is valid for PlayniteCmdPal. The project is a packaged MSIX extension whose manifest registers an out-of-process executable COM server (`com:ExeServer`). The former Inno Setup sample changed the project to unpackaged mode and registered an `InprocServer32` DLL. That registration model does not match the project's activation model and must not be used.

## Reconsider WinGet only if

1. A specific installer format supported by WinGet is selected for this exact packaged extension.
2. The installer preserves the correct packaged COM activation, package identity, signing/trust, architecture support, and clean uninstall behaviour.
3. x64 and ARM64 installation, upgrade, uninstall, and rollback have been tested on clean supported Windows machines.
4. The installer and all redistributed components pass the licensing review in [Licensing and release readiness](../../../../docs/LICENSING-AND-RELEASE.md).
5. The maintainer approves WinGet as the distribution route.

Do not add an invented EXE, package identifier, download URL, runtime dependency, or Store product ID to a manifest. Do not switch `WindowsPackageType`, remove the MSIX publish profile, or add registry entries solely to satisfy an old sample.

## Requirements after a route is validated

- Publish the installer through an approved, stable release URL.
- Generate and validate the WinGet manifest using current WinGet tooling and the current manifest schema.
- Include `windows-commandpalette-extension` in the locale manifest's `Tags`.
- Declare only dependencies that the actual installer requires; do not copy a sample Windows App Runtime dependency without checking the project's resolved runtime requirements.
- Verify package identifier, version, architecture, installer type, URL, and SHA-256 hashes.
- Validate `winget install`, `winget upgrade`, and `winget uninstall` on a clean test machine before linking it from the Gallery.

## Official references

- [Microsoft: Publish Command Palette extensions](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/publish-extension)
- [WinGet documentation](https://learn.microsoft.com/en-us/windows/package-manager/winget/)
- [Project release checklist](../../../../docs/RELEASE-PACKAGING.md)
