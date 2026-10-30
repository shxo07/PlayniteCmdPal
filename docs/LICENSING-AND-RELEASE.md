# Licensing and release readiness

PlayniteCmdPal-authored material is licensed under the MIT License; see the root [LICENSE](../LICENSE). This grant applies to project-authored material, not automatically to third-party code, template files, trademarks, artwork, SDKs or dependencies. Preserve all applicable third-party notices and comply with each component's terms. This inventory is not a legal opinion.

## Selected project license

The maintainer selected **MIT** for PlayniteCmdPal-authored material. The license permits use, modification and redistribution, including proprietary use, provided the copyright and license notices are retained. It does not include an express patent grant. Contributions submitted to this repository are intended to be offered under the same MIT terms; contributors must ensure they have the right to submit their contributions and must preserve third-party notices and restrictions.

The project license does not relicense Microsoft PowerToys template material or any other third-party component. A future companion Playnite plugin and its dependencies must be reviewed for their own licensing and distribution requirements.

## Initial release policy

- **Versioning:** use Semantic Versioning for public releases. While pre-1.0, use 0.y.z: increment z for backward-compatible fixes and small improvements, and increment y for incompatible changes or meaningful new functionality that changes the public contract. Treat 1.0.0 as the first stable public contract. Keep the MSIX four-part version synchronized with the release version using a documented mapping (for example, 1.2.3 to 1.2.3.0).
- **Release channel:** publish source and packaged binaries through GitHub Releases, with a corresponding version tag. Do not publish a release until the release checklist below is satisfied.
- **Release notes:** use the GitHub Release description as the authoritative changelog for each release. Include version/date, user-visible changes, fixes, known issues, supported PowerToys/Windows/Playnite versions, installation or upgrade instructions, and breaking changes.
- **Standalone changelog:** defer a checked-in CHANGELOG.md while releases are infrequent; revisit if release history becomes difficult to scan or consumers need an offline changelog.
- **Pre-releases:** label preview builds as GitHub pre-releases and use SemVer prerelease identifiers (for example, 0.3.0-preview.1). Do not present previews as stable releases.

## Inventory and unresolved checks

- Four generated C# files in PlayniteCmdPal/ carry Microsoft copyright and MIT notices. Their original root-license references pointed to a nonexistent file; the Microsoft PowerToys MIT text is retained separately at [third-party/Microsoft-PowerToys-LICENSE.txt](../third-party/Microsoft-PowerToys-LICENSE.txt). Keep the file headers and include the notice when distributing covered material. The project MIT license does not replace this notice.
- The seven checked-in PlayniteCmdPal/Assets/ PNGs were replaced with original PlayniteCmdPal artwork (a custom gamepad/command-prompt mark and wordmark) generated for this project. The previous files matched PowerToys extension template assets; the replacement removes that template artwork from the package. Do not add Playnite or Microsoft logos/trademarks without permission, and review any future third-party assets individually.
- Directory.Packages.props lists centrally managed versions, not necessarily the resolved dependency graph. The extension directly references Microsoft.CommandPalette.Extensions, Microsoft.Windows.CsWinRT, Shmuelie.WinRTServer and Microsoft.Windows.SDK.BuildTools.MSIX; test projects reference Microsoft.NET.Test.Sdk, xunit and xunit.runner.visualstudio. The Windows SDK and transitive/runtime dependencies may add more. Package license texts, redistribution terms, notices and binary inclusion have not been verified. Before distributing, restore on the supported Windows setup, inventory exact direct and transitive versions and published contents, inspect each package's license/terms (including non-SPDX and SDK terms), and bundle required notices. Test/build-only dependencies still need review for their actual use but are not automatically shipped.
- [Playnite](https://github.com/JosefNemec/Playnite/blob/master/LICENSE.md) declares MIT for its repository; this does not establish permission to use Playnite trademarks or artwork, or settle the terms of a future plugin and all its dependencies. No companion plugin is currently checked in. Review the exact SDK, plugin, assets and distribution once selected.
- PlayniteCmdPal/Package.appxmanifest now uses the PlayniteCmdPal package identity, display name and provisional Publisher value CN=PlayniteCmdPal. The Publisher value must exactly match the subject of the certificate used to sign the MSIX. A matching signing certificate has not been generated or validated; the maintainer has confirmed CN=PlayniteCmdPal as the selected publisher subject. A matching signing certificate still must be generated and validated before local package signing or distribution; public distribution must use a publisher identity the distributor controls. The package version remains 0.0.1.0 and must be synchronized with the selected release version.

## Before the first public release

1. Complete the dependency, asset, attribution and contribution-rights review above. Obtain permission where needed; do not ship unverified third-party artwork.
2. Apply the versioning, release-channel, release-notes and changelog policy above.
3. Set and validate package identity, signing/publisher information, display names and consistent version metadata. Confirm artifact contents and required third-party notices for each distribution channel.
4. Complete the Windows build, deployment, integration and security checks tracked by the project; do not describe the template as a verified release.
