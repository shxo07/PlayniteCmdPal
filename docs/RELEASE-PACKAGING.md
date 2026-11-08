# Release packaging and Command Palette Gallery readiness

**Status (2026-11-08): preparation only. No public release or Gallery submission is approved.**

This document is the release checklist for PlayniteCmdPal. It supersedes the generic examples in the publish-extension skill where those examples conflict with this repository's actual package, manifest, or licensing review.

## 1. Keep the two artifacts separate

This repository produces two different Windows artifacts:

| Artifact | Project / script | Purpose | Distribution status |
|---|---|---|---|
| Command Palette extension | `PlayniteCmdPal/PlayniteCmdPal.csproj`, `PlayniteCmdPal/Package.appxmanifest` | The MSIX-packaged out-of-process Command Palette extension | Candidate public product; not release-approved |
| Playnite companion plugin | `PlayniteCmdPal.PlaynitePlugin/`, `scripts/package-playnite-plugin.ps1` | Exports the Playnite catalog snapshot consumed by the CmdPal extension; produces a Playnite `.pext` | Local integration artifact; **not** the Command Palette installer and **not** a Gallery install source |

Do not upload `PlayniteCmdPalBridge.pext` to the Store or list it as the Command Palette extension's Gallery install source. The Gallery installs the CmdPal extension, not its companion Playnite bridge.

The CmdPal project is an MSIX/packaged extension. Its project targets `net10.0-windows10.0.26100.0`, declares `win-x64` and `win-arm64`, and has self-contained publish profiles for both architectures. The package manifest declares version `0.0.3.0`, Partner Center identity `werdscopez7347.PlayniteforCommandPalette`, and publisher `CN=B5EE8A57-B227-4726-A4E9-AF40E9D47E70`. The publisher display name is `werdscopez7347`. The COM registration is an out-of-process executable server (`com:ExeServer`); it is not an InprocServer32 DLL registration.

## 2. Distribution decision

Microsoft's current guidance says the Extension Gallery is a curated catalogue, not a hosting service. Each listing must point to an extension already published through one of the supported install sources: Microsoft Store or WinGet. Microsoft currently recommends the Store; WinGet is also supported when its manifest uses the `windows-commandpalette-extension` tag.

**Distribution decision: Microsoft Store via MSIX bundle is the primary channel.** GitHub Releases provide direct-download assets, and WinGet is an additional install route. The Command Palette Gallery will expose Microsoft Store and WinGet as its two install options after both sources are live. Do not create an Inno Setup EXE based on the old generic example: it changes the project to unpackaged mode and uses an InprocServer32 registration, which does not match this project's packaged out-of-process COM server.

Microsoft Store handles production signing for Store-distributed MSIX packages after certification. No publisher-managed production certificate is required for this path. GitHub-hosted packages and WinGet downloads do not inherit Store signing or Store installation reputation; treat their signing/trust requirements separately. No package should be published until the maintainer approves the completed release review.

## 3. Release version and package identity

- Keep the public version in Semantic Versioning form and map it to the four-part MSIX version. For example, SemVer `0.0.3` maps to MSIX `0.0.3.0`.
- The current manifest is `0.0.3.0`; do not increment it merely for this documentation change.
- The Partner Center identity supplied by the maintainer is applied in `PlayniteCmdPal/Package.appxmanifest`: Name `werdscopez7347.PlayniteforCommandPalette`, Publisher `CN=B5EE8A57-B227-4726-A4E9-AF40E9D47E70`, and PublisherDisplayName `werdscopez7347`.
- Keep the manifest version, evaluated package version, Store submission version, and release tag aligned.
- Microsoft Store performs production signing for Store-distributed packages. Follow [Microsoft Store package identity and MSIX signing](MSIX-SIGNING.md) for the identity record, Store-managed signing model, package validation, and submission evidence.

## 4. Assets and Gallery metadata

### Existing assets

The repository has an original brand asset pack at `PlayniteCmdPal-Brand-Assets/`. Its `icons/png/app-icon-256.png` is documented as a 256×256 PNG (5,393 bytes), which fits the Gallery guide's recommended 256×256 size and 100 KB maximum. Use this asset as the candidate Gallery icon after checking the actual file and previewing it at small sizes.

The MSIX-specific exports are in `PlayniteCmdPal-Brand-Assets/icons/png/msix/`. Their integration was completed by [branding PR #18](https://github.com/werdscopez7347/PlayniteCmdPal/pull/18), which has merged into `dev`. The merged `docs/BRANDING.md` and asset-package README define the asset mapping. Before producing a release candidate, verify the manifest-to-file paths and inspect the packaged visuals against that mapping.

### Gallery listing draft

A schema-shaped, deliberately unlisted template is in [`gallery/extension.json.template`](gallery/extension.json.template). It is not a publishable listing: its author/path and Microsoft Store product ID are placeholders, and `listed` is `false`. Replace every placeholder only after the publisher identity and a live install source are confirmed.

The current official Gallery contribution guide and schema require:

- A folder named `extensions/<author>/<extension-name>/`; both path segments use lowercase letters, digits, and hyphens.
- An `extension.json` with required `id`, `title`, `shortDescription`, `description`, `author`, `icon`, and at least one `installSources` entry.
- An ID of `<author>.<extension-name>` matching the folder path.
- A PNG or JPEG icon (SVG is not supported), no larger than 100 KB; 256×256 is recommended.
- Optional tags (up to five, max 30 characters each), categories (up to three from the official enum), and up to five screenshots (PNG/JPEG, max 1 MB each).
- A pull request to `microsoft/CmdPal-Extensions` targeting that repository's `main` branch. Its CI validates the schema, required fields, path/ID match, and icon presence/size. Gallery contributors may need to complete Microsoft's Contributor License Agreement (CLA).

The Gallery PR is a separate contribution to Microsoft's repository. It is not this repository's WERD-63 PR, which targets this project's `dev` branch.

### Do not create a live listing yet

The schema requires a real `installSources` entry. Do not insert a guessed WinGet package identifier or Store product ID just to make the JSON look complete. The draft remains unlisted until a real source exists and the release has passed its approval gates.

## 5. Build and package evidence

The checked-in publish profiles select `win-x64` and `win-arm64`, use self-contained publishing, and enable ReadyToRun. The project also has a post-publish target that copies the runtime pack's `THIRD-PARTY-NOTICES.TXT` into the publish output and fails if the source notice is absent.

The repository's documented automated baseline is:

```powershell
.\scripts\ci.ps1
```

That command restores, builds, runs the core and adapter test projects, and executes the .NET Framework snapshot smoke test. It is not an MSIX packaging test.

A release candidate still needs a Windows packaging run for both architectures. Record the exact Visual Studio/.NET SDK versions, commands, exit codes, package filenames, package versions, and SHA-256 hashes. For Store distribution, retain Partner Center submission and certification evidence; do not invent publisher-certificate thumbprint requirements. Inspect each package to confirm the extension manifest, assets, application binaries, project license, PowerToys template notice, and runtime notices are present. Do not commit generated packages, certificates, or private keys.

Do not copy a generic `dotnet publish` or Inno Setup command into release instructions until it has been run successfully against this exact project and the resulting MSIX has been inspected. The local Visual Studio **Build > Deploy** path in [Developer Setup](DEVELOPER-SETUP.md) is a development deployment, not proof of a distributable release package.

## 6. Licensing and release blockers

The current audit in [Licensing and release readiness](LICENSING-AND-RELEASE.md) identifies these unresolved gates:

1. **Windows SDK projection redistribution — WERD-76; unresolved release blocker.** The selected package is stable `Microsoft.Windows.SDK.NET.Ref 10.0.26100.87`; NuGet identifies it as a .NET 9-and-later targeting pack compatible with .NET 10 and links the standard Windows SDK licence (`https://aka.ms/WinSDKLicenseURL`). Microsoft's [Windows SDK REDIST.TXT](https://learn.microsoft.com/en-us/legal/windows-sdk/redist) has a dedicated package entry permitting listed package files to be redistributed unmodified as a NuGet package or as part of a WinRT-enabled program. The stable package archive was inspected: SHA-256 `2C1C57CAE573EBCA97F41CD257650161D5E610C009DDBD99CFC0774AF01F0663`, 104 entries, and only `lib/net9.0` framework assets. The REDIST entry enumerates `net8.0` and `net6.0` paths, not `net9.0`; whether its general package wording covers the stable package's `net9.0` assets remains unresolved. The downloaded SDK licence RTF was hashed (`DD07EB178E00C6BBA4148457FC00FF77CD4887EB521D504186FE59C9EC8BBE62`) and its conditions are recorded in `LICENSING-AND-RELEASE.md`. The audit also records Microsoft's maintained licence text, a Windows App SDK maintainer discussion, and conflicting anonymous Q&A guidance; none supplies written Microsoft confirmation of the `net9.0` scope. Exact stable-package x64/ARM64 publish and MSIX output inventories, package-signature verification, PE classification, and review of downstream terms and indemnification remain outstanding. Do not treat package availability, CI success, or the REDIST package entry alone as clearance. No public binary containing the affected files is approved.
2. **Stable Windows SDK targeting-pack selection:** `10.0.26100.87` has been selected and implemented on the WERD-77 branch. Its build/test validation is tracked there. This engineering decision does not establish redistribution permission and does not close blocker 1.
3. **Runtime notices — x64 and ARM64 MSIX payloads verified.** The filesystem-publish notice files for both RIDs match their restored runtime-pack notice files (SHA-256 `6D15E10A101C6BFFF2AB4429ED061BF76C456FC4B23AD6B03E0D0F8377148A21`). Both architecture MSIX packages were rebuilt and inspected; each contains `dotnet-runtime-THIRD-PARTY-NOTICES.txt`, the project `LICENSE`, `THIRD-PARTY-NOTICES.md`, and `Microsoft-PowerToys-LICENSE.txt`. The runtime-notice packaging defect is resolved for the inspected builds. Release notes must still reference the .NET Library License.
4. **Dependency inventory and publish-payload verification — Windows evidence recorded.** The corrected Windows PowerShell 5.1 inventory run reported 4 shipped-runtime, 4 shipped-build, 13 test-only, 0 developer-tool and 0 unclassified packages; its JSON SHA-256 is `A32B318636DC6AC98974524DBD014F5C915C80E967281FC50C5A5C86F51F174C`. Five review flags remain (four in shipped classifications); these flags are not a count of redistributed binaries. The inspected publish outputs did not contain the searched BuildTools, ReferenceAssemblies, xUnit or test-platform files. Both architecture MSIX archives contain the required notices and `Microsoft.CommandPalette.Extensions.winmd`. Disabling WinMD registration harvesting removed APPX1707 while retaining the WinMD payload. The `.pext` archive contains `LICENSE` and `THIRD-PARTY-NOTICES.md`; `System.Text.Json.dll` and `PlayniteSDK.dll` are absent. These results apply to the inspected builds and do not replace the still-required stable SDK projection output inventory for WERD-76.
5. **Store identity and signing:** Partner Center identity values have been supplied and applied to the manifest. Confirm them against the Partner Center product dashboard, then complete the Store packaging, validation, and certification workflow. No publisher-managed signing certificate is required for Store distribution. Direct GitHub/WinGet downloads need a separately evaluated trust model.
6. **Clean-machine acceptance:** replay install, first launch, update, uninstall, and rollback on a clean supported Windows environment. The Developer Setup page explicitly says the independent clean-machine walkthrough has not yet been completed.

A successful CI build does not clear these release blockers. Do not publish a binary or submit a Gallery listing while any required gate remains open.

## 7. Installation, update, uninstall, and rollback

### Development install (not public distribution)

For contributor testing, follow [Developer Setup](DEVELOPER-SETUP.md): enable Developer Mode, build and deploy the packaged project from Visual Studio, then use Command Palette's **Reload Command Palette extensions** command. This verifies a development deployment only; it is not the end-user installation flow.

### Public install

The public instructions depend on the selected live install source:

- **Microsoft Store:** install from the Store product page or the Gallery's Store action after the Store listing is live. Do not publish a Store URL or product ID before Partner Center provides it.
- **WinGet:** the selected additional Gallery install source. Document `winget install` only after the manifest and installer are accepted and install/upgrade/uninstall are validated. The manifest must include the official Command Palette discovery tag.

### Update

- **Store:** publish a higher package version through Partner Center; Store handles updates for installed users. Validate an upgrade from the previous released version before submission.
- **WinGet:** publish a new manifest version with verified installer URLs and hashes; validate `winget upgrade` on a clean test machine before listing it as supported.

### Uninstall

- **Store/MSIX:** use Windows **Settings → Apps → Installed apps → PlayniteCmdPal → Uninstall**. Verify the package is removed and that no stale extension entry remains after reloading Command Palette.
- **WinGet:** use `winget uninstall --id <verified-package-id> --exact` only if a WinGet package is eventually selected. Verify that the installer removes only the files and registration it owns.

### Rollback

Do not recommend manually downgrading an MSIX package. For a Store release, prepare a corrected higher-version package using the last known-good source revision and submit it as an update. For WinGet, publish a new higher package version containing the known-good build and validate the upgrade path. Keep the prior signed release and its release notes available for diagnosis; never overwrite a published asset in place.

## 8. Troubleshooting checklist

| Symptom | Checks |
|---|---|
| Extension does not appear after development deployment | Confirm Command Palette is enabled; redeploy the packaged project; run **Reload Command Palette extensions**; inspect Event Viewer and the extension process logs. |
| Package fails to install or register | Confirm package signature trust for the distribution source, exact package identity, architecture, and Store certification state where applicable. |
| Extension activation fails | Confirm the extension CLSID matches in the C# `[Guid]`, `com:Class Id`, and `CreateInstance ClassId`; confirm the manifest still declares the executable COM server and its activation arguments. |
| Gallery entry is visible but install fails | Confirm the listing's install-source type and identifier exactly match the live Store product or accepted WinGet package. The Gallery does not host the binary. |
| WinGet package is not discoverable in Command Palette | Confirm the published WinGet locale manifest includes `windows-commandpalette-extension`; verify the package is installable independently of the Gallery. |
| Upgrade leaves old behaviour or duplicate entries | Verify the package identity has not changed unintentionally, uninstall/reinstall on a clean VM, reload Command Palette, and capture package inventory before and after. |

## 9. Ownership and go/no-go checklist

The repository maintainer owns the release decision and must explicitly approve each external action. The engineering contributor prepares and validates artifacts but must not reserve identities, accept legal terms, sign with a private certificate, publish releases, or submit Gallery PRs on the maintainer's behalf.

Before the first public release, the maintainer must confirm:

- [ ] Windows SDK projection redistribution is cleared for the exact package/version and distribution model; Microsoft's REDIST scope for the stable `net9.0` package assets, full license conditions, distributor/end-user terms, required notices, and actual x64/ARM64 output inventories are recorded; the targeting-pack decision is recorded.
- [ ] The dependency inventory has been regenerated and all review items resolved.
- [ ] Runtime notices are present in both architecture outputs.
- [ ] Partner Center identity is confirmed against the product dashboard; x64 and ARM64 MSIX packages pass validation and Store certification. Direct GitHub/WinGet distribution has a separately documented trust model.
- [ ] x64 and ARM64 packages build and pass package inspection.
- [ ] A clean supported Windows machine passes install, first-run, update, uninstall, and rollback checks.
- [ ] Release notes, support/troubleshooting instructions, version, hashes, and notices are complete.
- [ ] The live Gallery metadata uses a real install source, validates against the current schema, and has a matching icon.
- [ ] The maintainer approves the public release and the separate Gallery contribution; any required Microsoft CLA is completed.

Until every required item is checked, the project remains **prepared for review, not ready to publish**.

## Official references

- [Microsoft: Publish Command Palette extensions](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/publish-extension)
- [Microsoft: Making your extension discoverable](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extension-gallery)
- [Gallery contribution guide](https://github.com/microsoft/CmdPal-Extensions/blob/main/docs/CONTRIBUTING.md)
- [Gallery extension schema](https://github.com/microsoft/CmdPal-Extensions/blob/main/.github/schemas/extension.schema.json)
- [Gallery sample extension](https://github.com/microsoft/CmdPal-Extensions/tree/main/extensions/microsoft/sample-extension)
