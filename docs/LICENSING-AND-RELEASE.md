# Licensing and release readiness

PlayniteCmdPal-authored material is licensed under the MIT License; see the root [LICENSE](../LICENSE). This grant applies to project-authored material, not automatically to third-party code, template files, trademarks, artwork, SDKs or dependencies. Preserve all applicable third-party notices and comply with each component's terms. This inventory is not a legal opinion and does not certify compliance.


## Selected Windows SDK projection package

**WERD-76 decision (2026-11-08):** use stable `Microsoft.Windows.SDK.NET.Ref 10.0.26100.84` and preserve its two redistributed projection DLLs byte-for-byte. The application remains targeted at `net10.0-windows10.0.26100.0`; NuGet resolves the package's `lib/net8.0` assets as compatible with .NET 10. The .84 asset group matches the framework path explicitly enumerated by Microsoft's [Windows SDK REDIST.TXT](https://learn.microsoft.com/en-us/legal/windows-sdk/redist). The .87 package was not selected because its `lib/net9.0` asset group is not expressly enumerated in that list.

Package SHA-256: `2EFF36ACCB9D76466D36E27E7199D375C662818D2C1376E65125D48BA4BD1C96`. The archive contains five `lib/net8.0` files, 91 WinMD files, one analyzer and two data files. Its NuGet signature verification succeeded on Windows with `dotnet nuget verify --all` (exit code 0).

### Exact SDK projection files in the MSIX

Release publishing disables both trimming and ReadyToRun. The following package-source files are present unchanged in both x64 and ARM64 MSIX payloads:

| File | Package source / x64 / ARM64 |
| --- | --- |
| `Microsoft.Windows.SDK.NET.dll` | 27,196,448 bytes; file version 10.0.26100.82; SHA-256 `CFF6F50D9E25D5C8148EE47C0860B1E13D5826448C52C6FE677577D051C5A48F` |
| `WinRT.Runtime.dll` | 528,944 bytes; file version 2.2.0.48161; SHA-256 `BCF3A14E8712A90837FC5C0D8C8A24696AF2BD7F74E9767597EC81EDEDFB23DB` |

The hashes above match the package-source files exactly in both architectures. Candidate .84 MSIX hashes: x64 `66DBDD4B3BE147331E29809FE94D553625611B3C5B4C3F84B6F1B61DFA4A99B8` (43,857,942 bytes); ARM64 `DC53C85E3FE166703D3C445796C6098BBD03DE0683DB50CBB3F050005D97883C` (42,470,759 bytes). Both packages contain the project `LICENSE`, `THIRD-PARTY-NOTICES.md`, `dotnet-runtime-THIRD-PARTY-NOTICES.txt`, and `Microsoft-PowerToys-LICENSE.txt`.

The package's other SDK files (including the XAML projection, WinMD files, analyzer and data files) are not included in the MSIX. The Command Palette SDK WinMD is from its separate dependency.

### Build and test evidence

The .84 candidate restored and built for x64 and ARM64 in isolated worktrees. The filtered solution test run passed 341 tests (107 Core, 3 Command, 231 Playnite); one PowerShell 7-dependent test was excluded because `pwsh.exe` was unavailable on the connected Windows machine. The unfiltered run's only failure was that environment dependency. The candidate MSIX packages were unsigned and have not been installed or runtime-tested. CI for the committed WERD-76 branch is the remaining implementation validation.

### SDK licence and redistribution conditions

The downloaded SDK licence RTF SHA-256 is `DD07EB178E00C6BBA4148457FC00FF77CD4887EB521D504186FE59C9EC8BBE62`. Its extracted text and Microsoft's maintained plain-text copy at [microsoft/win32metadata](https://github.com/microsoft/win32metadata/blob/main/licenses/sdk_license.txt) both identify EULA ID `WIN10SDK.RTM.AUG_2018_en-US`. A normalized token comparison produced 4,307 tokens on each side with an exact sequence match after normalizing whitespace, bullets and hyperlink markup.

The REDIST entry explicitly lists the relevant `lib/net8.0` projection files and permits listed files to be distributed unmodified as a NuGet package or as part of a WinRT-enabled program. This implementation uses those listed assets and preserves the two SDK DLLs unmodified. **Individual written Microsoft confirmation is not an engineering prerequisite for this selected package path.** This is an engineering interpretation of the published REDIST list, not a legal opinion or a claim that every distribution-channel obligation has been satisfied.

The Windows SDK licence §2.a also includes conditions concerning significant primary functionality, unmodified files, downstream distributor/end-user terms at least as protective as the SDK agreement, copyright notices, and Microsoft indemnification/defence obligations. The following public terms were reviewed as context: [Windows SDK licence](https://learn.microsoft.com/en-us/legal/windows-sdk/license), [Windows SDK REDIST.TXT](https://learn.microsoft.com/en-us/legal/windows-sdk/redist), [Microsoft Store App Developer Agreement v8.10](https://cdn-dynmedia-1.microsoft.com/is/content/microsoftcorp/microsoft/store/documents/legal/ada/fy26/MS.Store.ADAv8.10.EN.US.pdf), [WinGet manifest schema 1.28.0](https://github.com/microsoft/winget-pkgs/blob/master/doc/manifest/schema/1.28.0/singleton.md), and [GitHub Terms of Service](https://docs.github.com/en/site-policy/github-terms/github-terms-of-service).

- **Microsoft Store:** the publisher remains responsible for third-party licence compliance; the Store agreement's EULA/SALT and indemnity provisions still need to be assessed as part of release readiness.
- **GitHub Releases:** release downloads do not natively enforce a separate SDK end-user agreement. Do not assume the project MIT licence or notices alone satisfy downstream SDK terms.
- **WinGet:** manifest agreements can request acceptance, subject to the community repository's agreement-body restrictions and the publisher's verification status.

No independent legal opinion has been obtained. This engineering decision removes the unlisted `net9.0` asset-path ambiguity and the transformed-file issue from the selected package configuration; it does not certify legal compliance for Store, GitHub Releases or WinGet. Keep those channel-specific obligations in the public-release checklist. No public binary is approved by this change alone.

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

Audit date: 2026-11-08. This section records an evidence-based inventory of every direct and transitive NuGet dependency in the solution, what each one contributes to a release artifact, and the resulting obligations. Items that could not be settled from package metadata are listed as open review items; nothing here is legal advice or certification.

### What a release consists of

| Artifact | Project | Target framework | Publishing | Contents |
| --- | --- | --- | --- | --- |
| Command Palette extension (MSIX) | `PlayniteCmdPal/PlayniteCmdPal.csproj` | `net10.0-windows10.0.26100.0` | `SelfContained=true`, `PublishTrimmed=false`, `PublishReadyToRun=false` for both RIDs, per `Properties/PublishProfiles/win-x64.pubxml` and `win-arm64.pubxml` | Application assemblies, Command Palette SDK, C#/WinRT projection support, `Shmuelie.WinRTServer`, Windows SDK projections, .NET runtime, original artwork |
| Playnite catalog bridge plugin (`.pext`) | `PlayniteCmdPal.PlaynitePlugin/PlayniteCmdPal.PlaynitePlugin.csproj` | `net462`, `AnyCPU` | `dotnet publish` then `.zip` → `.pext` (`scripts/package-playnite-plugin.ps1`) | Plugin module, `PlayniteCmdPal.Snapshot.dll`, `extension.yaml`, notices. The Playnite SDK is compile-only (`ExcludeAssets="runtime"`) and is supplied by Playnite |

`PlayniteCmdPal.Core`, `PlayniteCmdPal.Snapshot` and `PlayniteCmdPal.Playnite` are project-authored (MIT) libraries that are compiled into those artifacts. Test projects and `tools/CatalogVerifier` are development-only and are not distributed.

### Method and evidence

- Package identity, pinned versions and dependency edges: `Directory.Packages.props`, project `PackageReference`/`ExcludeAssets`/`PrivateAssets` items, and NuGet package metadata (`.nuspec` license element, `licenseUrl`, `requireLicenseAcceptance`, dependency groups) read through the NuGet catalog and package pages on 2026-10-31. Licenses were taken from the packages' declared metadata, not from repository names or package titles.
- Redistribution terms for the .NET runtime: [dotnet/core license-information](https://github.com/dotnet/core/blob/main/license-information.md), which states that runtime packs and self-contained deployments are product distributions licensed on Windows under the [.NET Library License](https://dotnet.microsoft.com/dotnet_library_license.htm), and that their bundled components are listed in the runtime pack's `THIRD-PARTY-NOTICES.TXT`.
- Reproducible inventory generator: `scripts/licensing/New-DependencyInventory.ps1`. It restores the solution, reads `obj/project.assets.json` for every target framework and RID of the projects above, enriches each resolved package with its `.nuspec` license metadata, classifies it as `shipped-runtime`, `shipped-build`, `test-only` or `developer-tool`, and writes `artifacts/dependency-inventory.json` (git-ignored). It flags packages whose license is missing, ambiguous, URL-only, not on the assessed allow-list, or that require license acceptance, and exits with code 3 under `-FailOnReview`.

  ```powershell
  pwsh -File scripts/licensing/New-DependencyInventory.ps1 -Configuration Release
  ```

  The initial generator run was executed on the supported Windows build machine with Windows PowerShell 5.1 after a successful Release restore on 2026-11-01. Its baseline output was `artifacts/dependency-inventory.json` (git-ignored), SHA-256: `C90B5B2E1E286D1967BEF3740208D2571CD7565E0E296EAF5B524B60EA2C48E2`; it classified 7 packages as shipped-runtime, 4 as shipped-build, 13 as test-only, and reported 13 review flags. That baseline incorrectly treated three project references as NuGet packages and treated NuGet's `requireLicenseAcceptance` metadata as an automatic redistribution review. The generator has now been corrected to record project references separately and to report the acceptance flag without automatically treating it as a redistribution restriction. Re-run it on Windows before relying on updated counts or a new hash. Package-level evidence collected on 2026-11-01: CsWinRT 2.2.0 declares MIT in its package license file and has `requireLicenseAcceptance=false`; WinRTServer 2.1.1 includes its MIT license (copyright 2022 Shmueli Englard) and declares `requireLicenseAcceptance=true`; the MSIX BuildTools package contains Microsoft Windows SDK license terms; the ReferenceAssemblies and xunit.abstractions packages expose license URLs rather than local license files. A corrected Windows PowerShell 5.1 inventory run classified 4 packages as shipped-runtime, 4 as shipped-build, 13 as test-only, 0 as developer-tool and 0 as unclassified; it reported 5 review flags, 4 in the shipped classifications. The generated inventory JSON SHA-256 for this run is `A32B318636DC6AC98974524DBD014F5C915C80E967281FC50C5A5C86F51F174C`. The review flag count is an audit queue, not a count of binaries redistributed: the actual publish scan found no BuildTools, ReferenceAssemblies or xUnit/test-platform files in either RID output. It found `Shmuelie.WinRTServer.dll` and the three project-owned assemblies in both RIDs. A subsequent explicit scan found `WinRT.Runtime.dll` and `Microsoft.Windows.SDK.NET.dll` in both RIDs. The resolved `PlayniteCmdPal/obj/project.assets.json` confirms `WinRT.Runtime.dll` is a runtime and compile asset of `Microsoft.Windows.CsWinRT` 2.2.0 (`lib/net8.0/WinRT.Runtime.dll`); the package's own `Microsoft.Windows.CsWinRT.dll` is not the redistributed runtime assembly. The Windows SDK projection package selected on this WERD-76 branch is stable `Microsoft.Windows.SDK.NET.Ref` 10.0.26100.84. Its `lib/net8.0` assets are listed by Microsoft's REDIST entry, and the two SDK-derived DLLs are preserved byte-for-byte in x64 and ARM64 MSIX builds with trimming and ReadyToRun disabled. Exact package, DLL and MSIX hashes are recorded under **Selected Windows SDK projection package** above. This is an engineering implementation decision, not a legal opinion; channel-specific downstream terms remain part of release readiness. Transitive dependencies reached only through version ranges (for example `System.Reflection.Metadata >= 1.6.0`, `Newtonsoft.Json >= 13.0.1`) resolve to whatever NuGet selects at restore time; those resolved versions must come from the regenerated inventory.
- Publish profiles, project properties and `scripts/package-playnite-plugin.ps1` were read to determine what each artifact actually contains. Trimming, single-file settings and `ExcludeAssets` change the shipped set, so the checklist below also requires inspecting an actual publish/MSIX layout.

### Redistributed runtime dependencies — Command Palette extension

| Package | Version | Declared license (source) | Role in the artifact | Assessment |
| --- | --- | --- | --- | --- |
| Microsoft.CommandPalette.Extensions | 0.9.260303001 | MIT (`licenseExpression`; © Microsoft Corporation) | SDK/toolkit assemblies and the native `Microsoft.CommandPalette.Extensions.dll` used at runtime | Compatible. Ship the MIT notice. |
| Microsoft.Windows.CsWinRT | 2.2.0 | MIT (`LICENSE`; © Microsoft Corporation); `requireLicenseAcceptance=false` | C#/WinRT projection/build support. `Microsoft.Windows.CsWinRT.dll` was not found in either inspected publish output; `WinRT.Runtime.dll` is present and the resolved assets identify it as a runtime/compile asset of this package (`lib/net8.0/WinRT.Runtime.dll`) | MIT terms identified. Keep the MIT notice where required for distributed material covered by this package. |
| Shmuelie.WinRTServer | 2.1.1 | MIT (`licenseExpression`; © 2022 Shmueli Englard); no dependencies | Out-of-process WinRT/COM server used to host the extension; its DLL is present in both inspected publish directories | MIT terms identified. The NuGet acceptance flag is not, by itself, a redistribution prohibition. Include the MIT notice. |
| Microsoft.Windows.SDK.NET.Ref | 10.0.26100.84 | Microsoft Windows SDK license (`licenseUrl` → `https://aka.ms/WinSDKLicenseURL`); not an OSI license | .NET 8 asset group used by the `net10.0-windows10.0.26100.0` application. The inspected stable `.nupkg` contains 104 entries, including `lib/net8.0` assets and 91 WinMD files. The two redistributed SDK DLLs are byte-identical to package source in x64 and ARM64 MSIX payloads | **Selected for WERD-76.** The relevant `lib/net8.0` paths are enumerated by Microsoft's REDIST list; trimming and ReadyToRun are disabled to preserve the files. Channel-specific downstream terms remain a public-release review item. |
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
| Windows SDK projections | Microsoft's official [Windows SDK REDIST.TXT](https://learn.microsoft.com/en-us/legal/windows-sdk/redist) has a dedicated `Microsoft.Windows.SDK.NET.Ref` entry and permits the listed package files to be redistributed unmodified as a NuGet package or as part of a program enabling WinRT API calls. The selected stable package is `10.0.26100.84` and contains the explicitly listed `net8.0` asset group. The two SDK-derived DLLs present in both x64 and ARM64 MSIX payloads match the package-source hashes exactly; release trimming and ReadyToRun are disabled. See the WERD-76 package audit above. Channel-specific downstream terms and indemnification remain public-release review items. |
| Source-availability offers / reciprocal licenses | None identified. No dependency uses GPL, LGPL, AGPL, SSPL, EUPL, MPL or similar terms; the only non-OSI component is the Microsoft Windows SDK license |
| Trademark and artwork permissions | `third-party/THIRD-PARTY-NOTICES.md` records that no trademark rights are granted and that `PlayniteCmdPal/Assets/` holds original artwork only |

### Open items requiring maintainer or qualified legal review

1. **Windows SDK projection package — WERD-76 implementation.** This branch pins stable `Microsoft.Windows.SDK.NET.Ref 10.0.26100.84`, keeps the application on `.NET 10`, and sets `PublishTrimmed=false` and `PublishReadyToRun=false` for x64 and ARM64. The package uses `lib/net8.0`, a framework path explicitly enumerated in Microsoft's [Windows SDK REDIST list](https://learn.microsoft.com/en-us/legal/windows-sdk/redist). The two SDK-derived DLLs in the tested MSIX payloads match their package-source SHA-256 values exactly. See **Selected Windows SDK projection package** above for package, DLL and MSIX hashes, signature verification, build and test evidence. Individual written Microsoft confirmation is not required for this selected engineering path. This does not claim a legal opinion or waive the Windows SDK licence's downstream-terms, copyright and indemnification conditions; review those conditions for each public distribution channel before release.

2. **Branch validation and runtime acceptance.** The isolated .84 x64/ARM64 build and 341-test run are recorded above. CI must pass on this WERD-76 commit. The unsigned candidate MSIX packages have not been installed or runtime-tested; that remains part of Windows release acceptance.

3. **.NET runtime notices — x64 and ARM64 MSIX payloads verified.** The filesystem-publish notice files for both RIDs match their respective restored runtime-pack notice files (SHA-256 `6D15E10A101C6BFFF2AB4429ED061BF76C456FC4B23AD6B03E0D0F8377148A21`). The maintainer rebuilt both architecture MSIX packages after adding the exact runtime-pack notice directly to `AppxPackagePayload`. Direct archive inspection confirmed `dotnet-runtime-THIRD-PARTY-NOTICES.txt` is present in both packages, alongside the project `LICENSE`, `THIRD-PARTY-NOTICES.md` and `Microsoft-PowerToys-LICENSE.txt`. The runtime-notice packaging defect is resolved for the inspected builds. Release notes must still reference the .NET Library License.

4. **Dependency inventory and publish-payload verification — Windows evidence recorded.** The corrected Windows PowerShell 5.1 inventory run completed with 4 shipped-runtime, 4 shipped-build, 13 test-only, 0 developer-tool and 0 unclassified packages. Its git-ignored JSON SHA-256 is `A32B318636DC6AC98974524DBD014F5C915C80E967281FC50C5A5C86F51F174C`. Five review flags remain, four within shipped classifications; these flags are not a count of redistributed binaries. The inspected publish outputs do not contain the searched BuildTools, ReferenceAssemblies, xUnit or test-platform files. Both architecture MSIX archives contain the required notices and `Microsoft.CommandPalette.Extensions.winmd`. Disabling WinMD registration harvesting removed APPX1707 on both builds while retaining the WinMD payload. The `.pext` archive was also inspected and contains `LICENSE` and `THIRD-PARTY-NOTICES.md`; `System.Text.Json.dll` and `PlayniteSDK.dll` are absent as expected. These results are evidence for the inspected builds and do not substitute for the stable Windows SDK redistribution inventory required by WERD-76.

5. **Package signing, installation and activation — historical x64 local test; production signing tracked by WERD-79.** The current project and manifest publisher is `CN=B5EE8A57-B227-4726-A4E9-AF40E9D47E70`. The maintainer previously created a local test-signing certificate with subject `CN=PlayniteCmdPal`, imported it into the local machine's Trusted People store, signed an x64 MSIX, verified the signature, installed the package and confirmed that the extension appeared and worked in Command Palette. That historical test used a different publisher identity and does not establish that a package built with the current publisher identity can be signed and installed. WERD-79 tracks the production signing path. ARM64 installation and activation have not been tested on ARM64 hardware.

**Release-gate summary:** this WERD-76 branch now selects the stable .84 package whose relevant `net8.0` assets are enumerated by REDIST and preserves the two shipped projection DLLs unmodified. Individual Microsoft confirmation is not an engineering prerequisite for this path. CI remains to be verified on the committed branch. This is not a legal opinion: downstream terms, copyright and indemnification conditions still require channel-specific review before any public binary release. The .NET runtime-notice packaging defect is resolved for the inspected x64 and ARM64 MSIX builds. Dependency inventory evidence is recorded, with review flags remaining. Production signing and publisher-identity validation remain tracked separately by WERD-79.

The Windows SDK package choice and byte-preserving publish configuration are implemented on this WERD-76 branch only. No WERD-77 branch, PR or source file was changed. These changes do not authorize public binary distribution.
### Audit limitations

- The inventory JSON is git-ignored and machine-specific; its recorded hash is evidence for the maintainer's 2026-11-05 Windows run, not a portable artifact. The maintainer-reported x64/ARM64 publish and MSIX archive inspections, x64 signing, installation and Command Palette activation are local Windows evidence, not Windows CI artifacts. ARM64 installation/activation remains unverified. CI restore/build/test does not run publish, MSIX packaging or `.pext` packaging. The .84 publish/MSIX hashes above are from isolated Windows builds and must be rechecked against committed branch outputs.
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
