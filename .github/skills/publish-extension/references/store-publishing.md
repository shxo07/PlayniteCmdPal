# Microsoft Store distribution — PlayniteCmdPal

**Status: candidate route only; not approved for submission.**

The project currently ships the Command Palette extension as a packaged MSIX with x64 and ARM64 publish profiles. Microsoft's current Command Palette publishing documentation recommends the Microsoft Store and lists MSIX as the Store package format.

Do not follow a generic sample manifest or replace the current package identity with guessed Partner Center values. The current manifest uses package name `PlayniteCmdPal`, provisional publisher subject `CN=PlayniteCmdPal`, and version `0.0.3.0`. These values are not evidence of a reserved Store identity or a validated signing certificate.

## Before a Store submission

1. Resolve the Windows SDK projection redistribution and stable-targeting-pack blockers in [Licensing and release readiness](../../../../docs/LICENSING-AND-RELEASE.md).
2. Regenerate and review the dependency inventory. Verify the runtime third-party notices in both x64 and ARM64 self-contained publish outputs.
3. Complete the clean-machine installation, update, uninstall, and rollback validation in [Release packaging](../../../../docs/RELEASE-PACKAGING.md).
4. The maintainer reserves the app in Partner Center and supplies the exact package identity name, publisher, and publisher display name. Do not invent or reuse sample values.
5. Configure the project manifest and package settings with those exact values. The manifest publisher must match the signing certificate subject.
6. Build and inspect the x64 and ARM64 MSIX packages. Record tool versions, package versions, signatures, hashes, and included notices.
7. The maintainer reviews the Store listing, privacy/support details, certification notes, and legal terms before submission.

## Installation and updates

After an approved Store listing is live, users install from its Store product page or from the Gallery's Store action. Store handles updates for installed users. Users uninstall through **Windows Settings → Apps → Installed apps → PlayniteCmdPal → Uninstall**.

Do not publish a Store product URL or product ID until Partner Center has issued the real values. The Gallery metadata requires that real product ID in its `installSources` entry.

## Current blockers

- Microsoft Windows SDK preview projection redistribution permission is unresolved.
- A compatible stable Windows SDK targeting pack has not been selected and verified.
- Runtime notice copying has not yet been exercised in both architecture outputs.
- Final Store identity and matching signing certificate have not been validated.
- Independent clean-machine package acceptance remains uncompleted.

No package upload, certification submission, or public release is authorized by this guide.

## Official references

- [Microsoft: Publish Command Palette extensions](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/publish-extension)
- [Microsoft Store Partner Center](https://partner.microsoft.com/dashboard/home)
- [Project release checklist](../../../../docs/RELEASE-PACKAGING.md)
