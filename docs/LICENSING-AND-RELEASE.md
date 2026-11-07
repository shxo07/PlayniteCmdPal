# Licensing and release readiness

PlayniteCmdPal-authored material is licensed under the MIT License; see the root [LICENSE](../LICENSE). This grant applies to project-authored material, not automatically to third-party code, template files, trademarks, artwork, SDKs or dependencies. Preserve all applicable third-party notices and comply with each component's terms. This inventory is not a legal opinion and does not certify compliance.

## Selected project license

The maintainer selected **MIT** for PlayniteCmdPal-authored material. The license permits use, modification and redistribution, including proprietary use, provided the copyright and license notices are retained. It does not include an express patent grant. Contributions submitted to this repository are intended to be offered under the same MIT terms; contributors must ensure they have the right to submit their contributions and must preserve third-party notices and restrictions.

The project license does not relicense Microsoft PowerToys template material or any other third-party component. A future companion Playnite plugin and its dependencies must be reviewed for their own licensing and distribution requirements.

## Initial release policy

- **Versioning:** use Semantic Versioning for public releases. While pre-1.0, use 0.y.z: increment z for backward-compatible fixes and small improvements, and increment y for incompatible changes or meaningful new functionality that changes the public contract. Treat 1.0.0 as the first stable public contract. Keep the MSIX four-part version synchronized with the release version using a documented mapping (for example, 1.2.3 to 1.2.3.0).
- **Release channel:** publish source and packaged binaries through GitHub Releases, with a corresponding version tag. Do not publish a release until the release checklist below is satisfied.
- **Release notes:** use the GitHub Release description as the authoritative changelog for each release. Include version/date, user-visible changes, fixes, known issues, supported PowerToys/Windows/Playnite versions, installation or upgrade instructions, breaking changes, and the third-party notices statement described under [Notices and attribution](#notices-attribution-and-redistribution-obligations).
- **Standalone changelog:** defer a checked-in CHANGELOG.md while releases are infrequent; revisit if release history becomes difficult to scan or consumers need an offline changelog.
- **Pre-releases:** label preview builds as GitHub pre-releases and use SemVer prerelease identifiers (for example, 0.3.0-preview.1). Do not present previews as stable releases.

## Dependency and redistribution audit

Audit date: 2026-10-31. This section records an evidence-based inventory of every direct and transitive NuGet dependency in the solution, what each one contributes to a release artifact, and the resulting obligations. Items that could not be settled from package metadata are listed as open review items; nothing here is legal advice or certification.

### What a release consists of

| Artifact | Project | Target framework | Publishing | Contents |
| --- | --- | --- | --- | --- |
| Command Palette extension (MSIX) | `PlayniteCmdPal/PlayniteCmdPal.csproj` | `net10.0-windows10.0.26100.0` | `SelfContained=true`, `PublishReadyToRun=true`, trimming on for non-Debug configurations, per `Properties/PublishProfiles/win-x64.pubxml` and `win-arm64.pubxml` | Application assemblies, Command Palette SDK, C#/WinRT projection support, `Shmuelie.WinRTServer`, Windows SDK projections, .NET runtime, original artwork |
| Playnite catalog bridge plugin (`.pext`) | `PlayniteCmdPal.PlaynitePlugin/PlayniteCmdPal.PlaynitePlugin.csproj` | `net462`, `AnyCPU` | `dotnet publish` then `.zip` → `.pext` (`scripts/package-playnite-plugin.ps1`) | Plugin module, `PlayniteCmdPal.Snapshot.dll`, `extension.yaml`, notices. The Playnite SDK is compile-only (`ExcludeAssets="runtime"`) and is supplied by Playnite |

`PlayniteCmdPal.Core`, `PlayniteCmdPal.Snapshot` and `PlayniteCmdPal.Playnite` are project-authored (MIT) libraries that are compiled into those artifacts. Test projects and `tools/CatalogVerifier` are development-only and are not distributed.

### Method and evidence

- Package identity, pinned versions and dependency edges: `Directory.Packages.props`, project `PackageReference`/`ExcludeAssets`/`PrivateAssets` items, and NuGet package metadata (`.nuspec` license element, `licenseUrl`, `requireLicenseAcceptance`, dependency groups) read through the NuGet catalog and package pages on 2026-10-31. Licenses were taken from the packages' declared metadata, not from repository names or package titles.
- Redistribution terms for the .NET runtime: [dotnet/core license-information](https://github.com/dotnet/core/blob/main/license-information.md), which states that runtime packs and self-contained deployments are product distributions licensed on Windows under the [.NET Library License](https://dotnet.microsoft.com/dotnet_library_license.htm), and that their bundled components are listed in the runtime pack's `THIRD-PARTY-NOTICES.TXT`.
- Reproducible inventory generator: `scripts/licensing/New-DependencyInventory.ps1`. It restores the solution, reads `obj/project.assets.json` for every target framework and RID of the projects above, enriches each resolved package with its `.nuspec` license metadata, classifies it as `shipped-runtime`, `shipped-build`, `test-only` or `developer-tool`, and writes `artifacts/dependency-inventory.json` (git-ignored). It flags packages whose license is missing, ambiguous, URL-only, not on the assessed allow-list, or that require license acceptance, and exits with code 3 under `-FailOnReview`.

  ```powershell
  pwsh -File scripts/licensing/New-DependencyInventory.ps1 -Configuration Release
  ```

  The initial generator run was executed on the supported Windows build machine with Windows PowerShell 5.1 after a successful Release restore on 2026-11-01. Its baseline output was `artifacts/dependency-inventory.json` (git-ignored), SHA-256: `C90B5B2E1E286D1967BEF3740208D2571CD7565E0E296EAF5B524B60EA2C48E2`; it classified 7 packages as shipped-runtime, 4 as shipped-build, 13 as test-only, and reported 13 review flags. That baseline incorrectly treated three project references as NuGet packages and treated NuGet's `requireLicenseAcceptance` metadata as an automatic redistribution review. The generator has now been corrected to record project references separately and to report the acceptance flag without automatically treating it as a redistribution restriction. Re-run it on Windows before relying on updated counts or a new hash. Package-level evidence collected on 2026-11-01: CsWinRT 2.2.0 declares MIT in its package license file and has `requireLicenseAcceptance=false`; WinRTServer 2.1.1 includes its MIT license (copyright 2022 Shmueli Englard) and declares `requireLicenseAcceptance=true`; the MSIX BuildTools package contains Microsoft Windows SDK license terms; the ReferenceAssemblies and xunit.abstractions packages expose license URLs rather than local license files. A corrected Windows PowerShell 5.1 inventory run classified 4 packages as shipped-runtime, 4 as shipped-build, 13 as test-only, 0 as developer-tool and 0 as unclassified; it reported 5 review flags, 4 in the shipped classifications. The generated inventory JSON SHA-256 for this run is `A32B318636DC6AC98974524DBD014F5C915C80E967281FC50C5A5C86F51F174C`. The review flag count is an audit queue, not a count of binaries redistributed: the actual publish scan found no BuildTools, ReferenceAssemblies or xUnit/test-platform files in either RID output. It found `Shmuelie.WinRTServer.dll` and the three project-owned assemblies in both RIDs. A subsequent explicit scan found `WinRT.Runtime.dll` and `Microsoft.Windows.SDK.NET.dll` in both RIDs. The resolved `PlayniteCmdPal/obj/project.assets.json` confirms `WinRT.Runtime.dll` is a runtime and compile asset of `Microsoft.Windows.CsWinRT` 2.2.0 (`lib/net8.0/WinRT.Runtime.dll`); the package's own `Microsoft.Windows.CsWinRT.dll` is not the redistributed runtime assembly. The Windows SDK projection assembly `Microsoft.Windows.SDK.NET.dll` is separately associated with `Microsoft.Windows.SDK.NET.Ref` 10.0.26100.68-preview in the resolved assets. Their file versions and SHA-256 hashes are recorded under open item 1. Do not infer clearance from package metadata alone. Transitive dependencies reached only through version ranges (for example `System.Reflection.Metadata >= 1.6.0`, `Newtonsoft.Json >= 13.0.1`) resolve to whatever NuGet selects at restore time; those resolved versions must come from the regenerated inventory.
- Publish profiles, project properties and `scripts/package-playnite-plugin.ps1` were read to determine what each artifact actually contains. Trimming, single-file settings and `ExcludeAssets` change the shipped set, so the checklist below also requires inspecting an actual publish/MSIX layout.

### Redistributed runtime dependencies — Command Palette extension

| Package | Version | Declared license (source) | Role in the artifact | Assessment |
| --- | --- | --- | --- | --- |
| Microsoft.CommandPalette.Extensions | 0.9.260303001 | MIT (`licenseExpression`; © Microsoft Corporation) | SDK/toolkit assemblies and the native `Microsoft.CommandPalette.Extensions.dll` used at runtime | Compatible. Ship the MIT notice. |
| Microsoft.Windows.CsWinRT | 2.2.0 | MIT (`LICENSE`; © Microsoft Corporation); `requireLicenseAcceptance=false` | C#/WinRT projection/build support. `Microsoft.Windows.CsWinRT.dll` was not found in either publish output; `WinRT.Runtime.dll` is present and its exact assembly/package provenance must be confirmed against the resolved assets before attributing its license solely to this package | Package license identified as MIT. Keep the MIT notice where required for any distributed material covered by this package. |
| Shmuelie.WinRTServer | 2.1.1 | MIT (`licenseExpression`; © 2022 Shmueli Englard); `requireLicenseAcceptance=true`. No dependencies. | Out-of-process WinRT/COM server used to host the extension; its DLL is present in both inspected publish directories | MIT terms identified. The NuGet acceptance flag is not, by itself, a redistribution prohibition. The MIT notice is included. |
| Microsoft.Windows.SDK.NET.Ref | 10.0.26100.68-preview | Microsoft Windows SDK (preview) license (`licenseUrl` → `https://aka.ms/WinSDKPreviewLicenseURL`); `requireLicenseAcceptance=true`; not an OSI license | `Microsoft.Windows.SDK.NET.dll` is present in both self-contained publish outputs | **Release blocker** — redistribution permission for the exact preview package/version remains unconfirmed; see open items 1 and 2. |
| .NET runtime (`Microsoft.NETCore.App.Runtime.win-x64` / `win-arm64`, implicit) | resolved by the .NET 10 SDK at publish time | On Windows, the .NET Library License; embedded components listed in the runtime pack's `THIRD-PARTY-NOTICES.TXT` (@ .NET Foundation and Contributors) | Self-contained runtime | **Obligation** — ship the runtime's third-party notices and the license reference; see open item 3. |

Project-authored MIT assemblies (`PlayniteCmdPal.dll`/`.exe`, `PlayniteCmdPal.Core.dll`, `PlayniteCmdPal.Playnite.dll`, `PlayniteCmdPal.Snapshot.dll`) require the project `LICENSE` text. Four files in `PlayniteCmdPal/` are derived from the Microsoft PowerToys extension template and carry Microsoft MIT headers; the retained notice is `third-party/Microsoft-PowerToys-LICENSE.txt`.

### Redistributed components — Playnite bridge plugin (`.pext`)

| Component | Version | License (source) | Role | Assessment |
| --- | --- | --- | --- | --- |
| PlayniteCmdPal.PlaynitePlugin, PlayniteCmdPal.Snapshot | repository version | MIT (root `LICENSE`) | Plugin module and snapshot serializer | Ship the project license text. |
| PlayniteSDK | 6.18.0 | MIT (`license` URL; © Josef Nemec) | Compile-only (`ExcludeAssets="runtime"`); Playnite supplies the SDK at run time | Not redistributed. No notice obligation; recorded for completeness. |
| .NET Framework 4.6.2 | OS component | Microsoft .NET Framework terms | Host runtime (Playnite and Windows provide it) | Not redistributed by this project. |

No other third-party runtime assemblies are expected in the `.pext`; `scripts/package-playnite-plugin.ps1` already fails when an unexpected `System.Text.Json.dll` appears, and now also fails when `THIRD-PARTY-NOTICES.md` is missing.

### Build-time and test-only dependencies

None of these are present in either artifact; they are recorded so build and test tooling is not mistaken for redistributed code.

| Package | Version | License | Notes |
| --- | --- | --- | --- |
| Microsoft.Windows.SDK.BuildTools.MSIX | 1.7.20250829.1 | Microsoft SDK license terms (`sdk_license.txt`) | `PrivateAssets=all`; generates the MSIX. The MSIX tooling itself is not part of the package payload. |
| Microsoft.NETFramework.ReferenceAssemblies (and `.net462` and other TFM variants) | 1.0.3 | Nuspec supplies a Microsoft .NET repository license URL, not a local license expression/file | `PrivateAssets=all`; build-only reference assemblies; no matching files found in either inspected publish directory. |
| Microsoft.NET.Test.Sdk, Microsoft.TestPlatform.TestHost, Microsoft.TestPlatform.ObjectModel, Microsoft.CodeCoverage | 17.13.0 | MIT | Test host and coverage tooling. |
| xunit, xunit.core, xunit.assert, xunit.extensibility.core, xunit.extensibility.execution | 2.9.3 | Apache-2.0 | Test framework. |
| xunit.analyzers | 1.18.0 | Apache-2.0 | Transitive of `xunit`; Roslyn analyzer only, with no runtime asset. |
| xunit.abstractions | 2.0.3 | Nuspec supplies the xUnit license URL, not a local license expression/file | Transitive of `xunit.extensibility.*`; test-only and no matching file found in either inspected publish directory. |
| xunit.runner.visualstudio | 3.0.1 | Apache-2.0 | Runner; `PrivateAssets=all`, not published. |
| Newtonsoft.Json | `>= 13.0.1` (resolved at restore) | MIT | Transitive of the test host on `netcoreapp3.1`+; test-only. |
| System.Reflection.Metadata | `>= 1.6.0` (resolved at restore) | MIT | Transitive of `Microsoft.TestPlatform.ObjectModel`; test-only. |
| NETStandard.Library | 1.6.1 | MIT | Appears only in legacy `netstandard1.1`/`net1.0` dependency groups; not selected for `net10.0` tests, and redistributed with the .NET runtime anyway. |

No Apache-2.0 assembly is redistributed, so no Apache-2.0 notice is required in the artifacts. If a test project is ever published, add the Apache-2.0 license text and any upstream `NOTICE`.

### Declared central versions with no package reference

Six previously declared but unreferenced central package versions were removed from `Directory.Packages.props`; the remaining central entries correspond to package references in the project files.

### Notices, attribution and redistribution obligations

The obligations identified above are handled as follows. The Windows publish and MSIX outputs were inspected on 2026-11-05; see open items 3 and 4 for the recorded evidence.

| Obligation | Where satisfied |
| --- | --- |
| MIT notice and copyright for the Command Palette SDK, C#/WinRT, `Shmuelie.WinRTServer` and PowerToys template material | `third-party/THIRD-PARTY-NOTICES.md` (MIT text plus one copyright line per component) and `third-party/Microsoft-PowerToys-LICENSE.txt`, both copied into the extension build/publish output and `.pext` publish output by the project files |
| Project MIT notice | Root `LICENSE`; add the license text to the MSIX payload or release download |
| .NET runtime third-party notices | `CopyDotNetRuntimeThirdPartyNotices` copies the exact restored runtime pack's `THIRD-PARTY-NOTICES.TXT` to `dotnet-runtime-THIRD-PARTY-NOTICES.txt` in self-contained publish output; both RIDs and their MSIX archives were verified on Windows |
| Windows SDK projections | Not yet satisfied — pending redistributability confirmation (open items 1 and 2) |
| Source-availability offers / reciprocal licenses | None identified. No dependency uses GPL, LGPL, AGPL, SSPL, EUPL, MPL or similar terms; the only non-OSI component is the Microsoft Windows SDK preview license |
| Trademark and artwork permissions | `third-party/THIRD-PARTY-NOTICES.md` records that no trademark rights are granted and that `PlayniteCmdPal/Assets/` holds original artwork only |

### Open items requiring maintainer or qualified legal review

1. **Windows SDK projection redistribution — tracked separately in WERD-76; unresolved release blocker.** The Windows build inventory identifies `Microsoft.Windows.SDK.NET.Ref` 10.0.26100.68-preview as the source of `Microsoft.Windows.SDK.NET.dll`, which is present in both x64 and ARM64 self-contained publish outputs. The preview package points to the Microsoft Windows SDK preview license (`https://aka.ms/WinSDKPreviewLicenseURL`) and requires license acceptance. WERD-76 records an important conditional permission: Microsoft's official [Windows SDK REDIST.TXT](https://learn.microsoft.com/en-us/legal/windows-sdk/redist) has a dedicated `Microsoft.Windows.SDK.NET.Ref` entry permitting listed package files to be redistributed unmodified as a NuGet package or as part of a program enabling WinRT API calls. However, the explicit file list identifies `net8.0` and `net6.0` paths, not the `net9.0` asset path used by the package's newer target assets. The exact preview-license RTF linked by the package has not been retrieved and compared byte-for-byte with Microsoft's maintained SDK license text. The applicable SDK redistribution conditions also include unmodified files, significant primary functionality, downstream terms at least as protective, copyright notice, and distributor indemnity/defence/hold-harmless obligations. WERD-76 remains Todo while the preview-specific terms, `net9.0` scope, exact package contents and downstream obligations are unresolved. A successful build, local installation, or migration to a stable targeting pack does not itself resolve this separate clearance question. Do not approve public binaries containing the affected projection assembly until WERD-76 records a supported conclusion and the required conditions are met.
2. **Preview Windows SDK targeting pack — tracked separately in WERD-77.** The WERD-78 branch and current `dev` branch still set `<WindowsSdkPackageVersion>10.0.26100.68-preview</WindowsSdkPackageVersion>`. WERD-77 selected stable `Microsoft.Windows.SDK.NET.Ref 10.0.26100.87`; PR #24 adds that version centrally on its separate branch and remains open/draft. WERD-77 remains Todo pending clean Windows restore/build/test, Visual Studio deployment, x64/ARM64 publish/package validation, and evaluated restore-asset checks. The maintainer's x64/ARM64 build results in this issue were produced against the preview package. Re-run the applicable Windows validation after the stable-pack migration lands. Even if WERD-77 succeeds, WERD-76 remains the independent redistribution gate; the stable package's exact license and runtime projection assets still require review.
3. **.NET runtime notices — x64 and ARM64 MSIX payloads verified.** The filesystem-publish notice files for both RIDs match their respective restored runtime-pack notice files (SHA-256 `6D15E10A101C6BFFF2AB4429ED061BF76C456FC4B23AD6B03E0D0F8377148A21`). The maintainer rebuilt both architecture MSIX packages after adding the exact runtime-pack notice directly to `AppxPackagePayload`. Direct archive inspection confirmed `dotnet-runtime-THIRD-PARTY-NOTICES.txt` is present in both packages, alongside the project `LICENSE`, `THIRD-PARTY-NOTICES.md` and `Microsoft-PowerToys-LICENSE.txt`. The runtime-notice packaging defect is resolved for the inspected builds. Release notes must still reference the .NET Library License.
4. **Inventory and publish-payload verification — Windows evidence recorded.** The corrected Windows PowerShell 5.1 inventory run completed with 4 shipped-runtime, 4 shipped-build, 13 test-only, 0 developer-tool and 0 unclassified packages; 5 review flags remain (4 in shipped classifications). Its git-ignored JSON SHA-256 is `A32B318636DC6AC98974524DBD014F5C915C80E967281FC50C5A5C86F51F174C`. Review flags are not a count of redistributed binaries: the inspected publish outputs do not contain the searched BuildTools, ReferenceAssemblies, xUnit or test-platform files. Both architecture MSIX archives contain the required notices and `Microsoft.CommandPalette.Extensions.winmd`. Disabling WinMD registration harvesting removed APPX1707 on both builds while retaining the WinMD payload. The `.pext` archive was also inspected and contains `LICENSE` and `THIRD-PARTY-NOTICES.md`; `System.Text.Json.dll` and `PlayniteSDK.dll` are absent as expected.
5. **Package signing, installation and activation — historical x64 local test; current publisher identity not yet validated.** The maintainer previously created a local test-signing certificate with subject `CN=PlayniteCmdPal`, imported it into the local machine's Trusted People store, signed an x64 MSIX, verified the signature, installed the package and confirmed that the extension appeared and worked in Command Palette. The current project and manifest publisher is `CN=B5EE8A57-B227-4726-A4E9-AF40E9D47E70`; the earlier `CN=PlayniteCmdPal` test certificate does not establish that a package built with this current publisher identity can be signed and installed. This historical test is not production signing evidence. WERD-79 tracks the production signing path. ARM64 installation/activation has not been tested on ARM64 hardware.

These results complete the WERD-78 Windows inventory, runtime-notice, MSIX payload and x64 local activation evidence. They do not resolve WERD-76 redistribution clearance or WERD-77 stable targeting-pack migration, and they do not authorize public binary distribution.
### Audit limitations

- The inventory JSON is git-ignored and machine-specific; its recorded hash is evidence for the maintainer's 2026-11-05 Windows run, not a portable artifact. The maintainer-reported x64/ARM64 publish and MSIX archive inspections, x64 signing, installation and Command Palette activation are local Windows evidence, not Windows CI artifacts. ARM64 installation/activation remains unverified. CI restore/build/test does not run publish, MSIX packaging or `.pext` packaging. WERD-76 redistribution clearance and WERD-77 stable targeting-pack migration remain independent gates.
- Package metadata can change between versions; every dependency upgrade requires re-running the generator and revisiting this section.
- This document is not legal advice. Escalate open items to the maintainer and, where warranted, to qualified counsel before the first public binary release.

## Assets and artwork

- The seven checked-in `PlayniteCmdPal/Assets/` PNGs are original PlayniteCmdPal artwork (a custom gamepad/command-prompt mark and wordmark) generated for this project. They do not include Playnite or Microsoft logos, nor PowerToys extension template artwork. Do not add third-party logos, trademarks or artwork without permission, and review any future asset individually.
- Third-party icons or media fetched at run time are not redistributed by this repository; if that changes, extend `third-party/THIRD-PARTY-NOTICES.md` and this section.

## Package identity and signing

`PlayniteCmdPal/PlayniteCmdPal.csproj` and `PlayniteCmdPal/Package.appxmanifest` both configure the package Publisher as `CN=B5EE8A57-B227-4726-A4E9-AF40E9D47E70`, with package identity `werdscopez7347.PlayniteforCommandPalette`. The previously documented local test certificate used subject `CN=PlayniteCmdPal`; that test predates or does not validate the currently configured publisher identity. A signing and installation test using a certificate whose subject matches the configured Publisher remains necessary. WERD-79 tracks the production signing path. Public distribution must use a publisher identity the distributor controls. The package version remains `0.0.3.0` and must be synchronized with the selected release version.

## Before the first public release

1. Resolve the open review items above, including the Windows SDK projection redistributability question and the runtime notices mechanism. Obtain permission where needed; do not ship unverified third-party artwork or code.
2. Regenerate the dependency inventory from a clean restore (`scripts/licensing/New-DependencyInventory.ps1`) and compare it with this document; update the tables for any added, removed or upgraded package.
3. Apply the versioning, release-channel, release-notes and changelog policy above.
4. Set and validate package identity, signing/publisher information, display names and consistent version metadata. Confirm artifact contents and required third-party notices for each distribution channel, including `THIRD-PARTY-NOTICES.md`, `Microsoft-PowerToys-LICENSE.txt`, the project `LICENSE`, and the .NET runtime notices.
5. Complete the Windows build, deployment, integration and security checks tracked by the project; do not describe the template as a verified release.
