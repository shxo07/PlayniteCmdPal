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

Audit date: 2026-11-03. This section records an evidence-based inventory of every direct and transitive NuGet dependency in the solution, what each one contributes to a release artifact, and the resulting obligations. Items that could not be settled from package metadata are listed as open review items; nothing here is legal advice or certification.

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

  The generator was added by this audit. It has not yet been executed on the supported Windows build machine; run it after a clean restore and review the output, including resolved transitive versions, before relying on this section for a release. File-based nuspec licenses are always marked for manual review; the generator does not treat the presence of a license-file path as an approval. Transitive dependencies reached only through version ranges (for example `System.Reflection.Metadata >= 1.6.0`, `Newtonsoft.Json >= 13.0.1`) resolve to whatever NuGet selects at restore time; those resolved versions must come from the generator.
- Publish profiles, project properties and `scripts/package-playnite-plugin.ps1` were read to determine what each artifact actually contains. Trimming, single-file settings and `ExcludeAssets` change the shipped set, so the checklist below also requires inspecting an actual publish/MSIX layout.

### Redistributed runtime dependencies — Command Palette extension

| Package | Version | Declared license (source) | Role in the artifact | Assessment |
| --- | --- | --- | --- | --- |
| Microsoft.CommandPalette.Extensions | 0.9.260303001 | MIT (`licenseExpression`; © Microsoft Corporation) | SDK/toolkit assemblies and the native `Microsoft.CommandPalette.Extensions.dll` used at runtime | Compatible. Ship the MIT notice. |
| Microsoft.Windows.CsWinRT | 2.2.0 | MIT (`license` file; © Microsoft Corporation) | Projection/build support; `WinRT.Runtime.dll` and the Windows SDK projections it depends on may be copied into the output | Compatible. Ship the MIT notice for any included assembly. |
| Shmuelie.WinRTServer | 2.1.1 | MIT (`licenseExpression`; © 2025 Shmueli Englard). No dependencies. | Out-of-process WinRT/COM server used to host the extension | Compatible. Ship the MIT notice. |
| Microsoft.Windows.SDK.NET.Ref | 10.0.26100.87 | Microsoft Windows SDK license (`licenseUrl` → `https://aka.ms/WinSDKLicenseURL`; Microsoft-owned, not an OSI license) | .NET 9 targeting-pack package used by the `net10.0-windows10.0.26100.0` application; exact package files and copied publish assets still require inspection | **Not yet cleared.** Microsoft's REDIST page names this package but its enumerated framework assets are `net8.0` and `net6.0`; it does not expressly enumerate the stable package's `net9.0` asset group. Obtain authoritative scope confirmation and inspect the actual `.nupkg` and per-RID outputs — see open item 1. |
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
| Microsoft.NETFramework.ReferenceAssemblies (and `.net462` and other TFM variants) | 1.0.3 | MIT | `PrivateAssets=all`; build-only reference assemblies so `net462` builds do not depend on an installed .NET Framework developer pack. |
| Microsoft.NET.Test.Sdk, Microsoft.TestPlatform.TestHost, Microsoft.TestPlatform.ObjectModel, Microsoft.CodeCoverage | 17.13.0 | MIT | Test host and coverage tooling. |
| xunit, xunit.core, xunit.assert, xunit.extensibility.core, xunit.extensibility.execution | 2.9.3 | Apache-2.0 | Test framework. |
| xunit.analyzers | 1.18.0 | Apache-2.0 | Transitive of `xunit`; Roslyn analyzer only, with no runtime asset. |
| xunit.abstractions | 2.0.3 | Apache-2.0 | Transitive of `xunit.extensibility.*`. |
| xunit.runner.visualstudio | 3.0.1 | Apache-2.0 | Runner; `PrivateAssets=all`, not published. |
| Newtonsoft.Json | `>= 13.0.1` (resolved at restore) | MIT | Transitive of the test host on `netcoreapp3.1`+; test-only. |
| System.Reflection.Metadata | `>= 1.6.0` (resolved at restore) | MIT | Transitive of `Microsoft.TestPlatform.ObjectModel`; test-only. |
| NETStandard.Library | 1.6.1 | MIT | Appears only in legacy `netstandard1.1`/`net1.0` dependency groups; not selected for `net10.0` tests, and redistributed with the .NET runtime anyway. |

No Apache-2.0 assembly is redistributed, so no Apache-2.0 notice is required in the artifacts. If a test project is ever published, add the Apache-2.0 license text and any upstream `NOTICE`.

### Declared central versions with no package reference

Six previously declared but unreferenced central package versions were removed from `Directory.Packages.props`; the remaining central entries correspond to package references in the project files.

### Notices, attribution and redistribution obligations

The obligations identified above are handled as follows; publish-time output still requires Windows verification.

| Obligation | Where satisfied |
| --- | --- |
| MIT notice and copyright for the Command Palette SDK, C#/WinRT, `Shmuelie.WinRTServer` and PowerToys template material | `third-party/THIRD-PARTY-NOTICES.md` (MIT text plus one copyright line per component) and `third-party/Microsoft-PowerToys-LICENSE.txt`, both copied into the extension build/publish output and `.pext` publish output by the project files |
| Project MIT notice | Root `LICENSE`; add the license text to the MSIX payload or release download |
| .NET runtime third-party notices | `CopyDotNetRuntimeThirdPartyNotices` copies the exact restored runtime pack's `THIRD-PARTY-NOTICES.TXT` to `dotnet-runtime-THIRD-PARTY-NOTICES.txt` in self-contained publish output; verify both RIDs on Windows |
| Windows SDK projections | Microsoft's official [Windows SDK REDIST.TXT](https://learn.microsoft.com/en-us/legal/windows-sdk/redist) has a dedicated `Microsoft.Windows.SDK.NET.Ref` entry and permits the listed package files to be redistributed unmodified as a NuGet package or as part of a program enabling WinRT API calls. The current stable package is `10.0.26100.87` and targets `net9.0`; the REDIST entry's visible file list names `net8.0` and `net6.0` groups, not `net9.0`. Applicability to the stable package's exact contents therefore remains unconfirmed. Required license terms, distributor/end-user flow and per-RID shipped files are also open (open item 1). |
| Source-availability offers / reciprocal licenses | None identified. No dependency uses GPL, LGPL, AGPL, SSPL, EUPL, MPL or similar terms; the only non-OSI component is the Microsoft Windows SDK preview license |
| Trademark and artwork permissions | `third-party/THIRD-PARTY-NOTICES.md` records that no trademark rights are granted and that `PlayniteCmdPal/Assets/` holds original artwork only |

### Open items requiring maintainer or qualified legal review

1. **Windows SDK projection redistribution — release blocker; stable package selected, legal scope not yet established.** The project has selected `Microsoft.Windows.SDK.NET.Ref 10.0.26100.87` through the WERD-77 change. NuGet identifies this as the Windows SDK targeting pack for .NET 9 and later, and reports compatibility with .NET 10 ([exact package/version](https://www.nuget.org/packages/Microsoft.Windows.SDK.NET.Ref/10.0.26100.87)). Its license link is the standard Microsoft Windows SDK license URL, `https://aka.ms/WinSDKLicenseURL`; this is no longer the preview-specific `WinSDKPreviewLicenseURL`. The license RTF could not be retrieved in this audit environment, so this document does not claim to have reviewed the complete version-specific agreement.

   Microsoft's official [Windows SDK REDIST.TXT](https://learn.microsoft.com/en-us/legal/windows-sdk/redist) (updated 2024-10-21) contains a dedicated **Microsoft.Windows.SDK.NET.Ref** entry. It states that the files in that NuGet package may be distributed unmodified as a NuGet package or as part of a program enabling WinRT API calls. Its enumerated package paths include:
   - `./lib/net8.0/Microsoft.Windows.SDK.NET.dll`
   - `./lib/net8.0/Microsoft.Windows.UI.Xaml.dll`
   - `./lib/net8.0/WinRT.Runtime.dll`
   - `./lib/net6.0/Microsoft.Windows.SDK.NET.dll`
   - `./lib/net6.0/WinRT.Runtime.dll`
   - `./analyzers/dotnet/cs/WinRT.SourceGenerator.dll`, package metadata and `./winmd/*.winmd`

   **Scope gap:** NuGet describes version `10.0.26100.87` as a `net9.0` package, while the REDIST entry's enumerated framework-specific paths name `net8.0` and `net6.0`. The broad REDIST wording is relevant, but the published file list does not expressly enumerate the stable package's `net9.0` asset group. Do not infer that this is cleared. Obtain written confirmation from Microsoft that the REDIST permission covers version `10.0.26100.87` and all of its actual `net9.0` package files when redistributed in a self-contained Command Palette extension.

   **Additional Microsoft evidence reviewed (2026-11-03):**
   - Microsoft's own [win32metadata repository](https://github.com/microsoft/win32metadata/blob/main/licenses/sdk_license.txt) contains a text copy of the Windows 10 SDK licence. Its distributable-code section permits distribution of object code listed by REDIST.TXT and permits third-party distributors to distribute that code as part of the program. The conditions include significant primary functionality, no modification of distributable files, downstream distributor/end-user terms at least as protective as the SDK agreement, a valid copyright notice, and indemnification/defence/hold-harmless obligations for Microsoft. This is a Microsoft-maintained source copy, but it has **not** been compared byte-for-byte with the RTF linked from the exact NuGet package.
   - The current [REDIST.TXT package entry](https://learn.microsoft.com/en-us/legal/windows-sdk/redist) says files included in `Microsoft.Windows.SDK.NET.Ref` may be distributed unmodified either as a NuGet package or as part of a program enabling WinRT API calls. It explicitly enumerates `net8.0` and `net6.0` paths, not `net9.0` paths. This broad grant is meaningful evidence in favour of redistribution, but does not remove the need to verify whether the newer package's actual files are within the listed scope.
   - Microsoft's [Windows App SDK discussion #4368](https://github.com/microsoft/WindowsAppSDK/discussions/4368) says `Microsoft.Windows.SDK.NET.dll` is a Windows Platform SDK projection, not a Windows App SDK component, and points to the Windows SDK NuGet licence. The maintainer expressly says they are not a legal expert.
   - A [Microsoft Q&A response dated 2024-06-17](https://learn.microsoft.com/en-us/answers/questions/1696668/can-i-redistribute-microsoft-windows-sdk-net-dll) states that `Microsoft.Windows.SDK.NET.dll` may not be redistributed; a later response in the same thread recommends consulting the NuGet author or legal adviser. This is conflicting, anonymous support guidance, not a licence amendment or authoritative legal determination. It must be disclosed rather than ignored.
   - NuGet now also lists `Microsoft.Windows.SDK.NET.Ref 10.0.28000.87` (published 2026-10-03). Microsoft's SDK downloads page identifies the 28000 SDK line with Windows 11 26H1. Windows 11 26H1 is a distinct core/release for selected new devices and is not an in-place upgrade path from 24H2/25H2. The project targets `net10.0-windows10.0.26100.0` (Windows 11 24H2 generation), so the newer 28000 package is **not automatically a replacement** for the selected 26100 targeting pack. Do not change WERD-77's pin without a separate compatibility decision and Windows validation.
   - The web audit could read the Microsoft-maintained text licence copy and REDIST page, but could not retrieve the NuGet-linked `sdk_license.rtf` (served as `application/octet-stream`) or download the actual `.nupkg` in this environment. Therefore the exact package file inventory, RTF equality, and SHA-256 remain unverified.

   **Current interpretation:** Microsoft's published REDIST entry is affirmative conditional permission for the listed files and the stated WinRT-app use case. The unresolved issue is whether every file in the selected stable `net9.0` package is covered by that entry and whether this project's distribution model can satisfy all downstream terms and indemnity conditions. Do not convert this evidence into an unqualified legal-clearance claim.

   The standard Windows SDK license's REDIST conditions must also be checked against the complete stable license text and this distribution model. The conditions described by Microsoft's SDK terms include unmodified distributable files, significant primary functionality, a valid copyright notice, protective terms binding distributors and external end users, and the specified indemnification. The project must review how the distributor/end-user requirements apply to Microsoft Store, GitHub Releases and WinGet, including the separate signing/trust model for non-Store downloads.

   The actual stable `.nupkg` file list and exact files copied into the `win-x64` and `win-arm64` publish/MSIX outputs have not been supplied in this PR. Record package file paths and SHA-256, then record each SDK-derived output file, assembly version, source package, and architecture. Do not mark this item cleared until Microsoft confirms the `net9.0` scope, the complete license conditions are reviewed, the distribution terms are addressed, and the output inventory is attached. Engineering review is not legal approval.
2. **Stable Windows SDK targeting pack — version selected; validation tracked separately by WERD-77.** The selected version is `10.0.26100.87`, replacing `10.0.26100.68-preview`. Do not treat that engineering migration as redistribution clearance. The stable package's exact REDIST scope remains open under item 1.
3. **.NET runtime notices — implemented; `win-x64` reported verified, `win-arm64` unverified.** `CopyDotNetRuntimeThirdPartyNotices` runs after self-contained publish and copies `THIRD-PARTY-NOTICES.TXT` from the restored `Microsoft.NETCore.App.Runtime.<RID>` package (`$(NuGetPackageRoot)`), not from `RuntimeFrameworkVersion` and not from the Windows Desktop or Windows SDK packs. It fails the publish when zero or more than one candidate notice file is found. Maintainer-reported evidence (2026-10-31, not reproduced in this repository's automation): a local Release `win-x64` publish succeeded and `dotnet-runtime-THIRD-PARTY-NOTICES.txt` was present in the publish directory. The `win-arm64` publish and the notice content for both RIDs still need verification; release notes must also reference the .NET Library License.
4. **Inventory regeneration.** Run `scripts/licensing/New-DependencyInventory.ps1` on the supported Windows build machine, confirm the resolved transitive versions and runtime/trimmed asset classification, and record the generated JSON path or hash in the release notes for the first public release.
5. **Package signing and identity.** Unrelated to licensing but still release-blocking: `Package.appxmanifest` uses the provisional publisher `CN=PlayniteCmdPal` with package version `0.0.3.0`. The publisher must match the signing certificate subject, and the version must be synchronized with the release version.

Item 1 remains a release blocker: the stable package is selected and Microsoft's REDIST entry is directly relevant, but the exact `net9.0` package-asset scope, complete stable license conditions, distributor and end-user terms for Microsoft Store/GitHub Releases/WinGet, and actual per-RID output inventory are not yet established. The targeting-pack version decision is recorded separately under WERD-77; it does not close this legal gate. Item 3 has an implementation but requires Windows publish verification; item 4 is implemented; item 5 remains pending. The package identity/signing and release-channel gates remain separate. No engineering change in this PR constitutes legal confirmation.

### Audit limitations

- The audit reads declared package metadata and project/build configuration. Windows CI restore/build/test passes, including the .NET Framework snapshot smoke test, but CI does not run a publish, MSIX packaging or `.pext` packaging step, and the inventory generator has not been executed here. A maintainer-reported local Release `win-x64` publish succeeded and produced `dotnet-runtime-THIRD-PARTY-NOTICES.txt`; that is the only publish evidence recorded so far and it was not reproduced by automation. The exact published file list, the MSIX payload contents, the `win-arm64` output, the trimmed asset set, and the resolved versions of range-only transitive dependencies therefore still need confirmation from a release build before this document is treated as release evidence.
- Package metadata can change between versions; every dependency upgrade requires re-running the generator and revisiting this section.
- This document is not legal advice. Escalate open items to the maintainer and, where warranted, to qualified counsel before the first public binary release.

## Assets and artwork

- The seven checked-in `PlayniteCmdPal/Assets/` PNGs are original PlayniteCmdPal artwork (a custom gamepad/command-prompt mark and wordmark) generated for this project. They do not include Playnite or Microsoft logos, nor PowerToys extension template artwork. Do not add third-party logos, trademarks or artwork without permission, and review any future asset individually.
- Third-party icons or media fetched at run time are not redistributed by this repository; if that changes, extend `third-party/THIRD-PARTY-NOTICES.md` and this section.

## Package identity and signing

`PlayniteCmdPal/Package.appxmanifest` uses the PlayniteCmdPal package identity, display name and provisional Publisher value `CN=PlayniteCmdPal`. The Publisher value must exactly match the subject of the certificate used to sign the MSIX. A matching signing certificate has not been generated or validated; the maintainer has confirmed `CN=PlayniteCmdPal` as the selected publisher subject. A matching signing certificate still must be generated and validated before local package signing or distribution; public distribution must use a publisher identity the distributor controls. The package version remains `0.0.3.0` and must be synchronized with the selected release version.

## Before the first public release

1. Resolve the open review items above, including the Windows SDK projection redistributability question and the runtime notices mechanism. Obtain permission where needed; do not ship unverified third-party artwork or code.
2. Regenerate the dependency inventory from a clean restore (`scripts/licensing/New-DependencyInventory.ps1`) and compare it with this document; update the tables for any added, removed or upgraded package.
3. Apply the versioning, release-channel, release-notes and changelog policy above.
4. Set and validate package identity, signing/publisher information, display names and consistent version metadata. Confirm artifact contents and required third-party notices for each distribution channel, including `THIRD-PARTY-NOTICES.md`, `Microsoft-PowerToys-LICENSE.txt`, the project `LICENSE`, and the .NET runtime notices.
5. Complete the Windows build, deployment, integration and security checks tracked by the project; do not describe the template as a verified release.
