# Licensing and release readiness

PlayniteCmdPal-authored material is licensed under the MIT License; see the root [LICENSE](../LICENSE). This grant applies to project-authored material, not automatically to third-party code, template files, trademarks, artwork, SDKs or dependencies. Preserve all applicable third-party notices and comply with each component's terms. This inventory is not a legal opinion and does not certify compliance.


## Stable Windows SDK payload audit

Audit date: 2026-11-08. Builds were performed in isolated Git worktrees based on current `dev` commit `3a0957135f9448dff1aaa538b418dfdd3c6dcb14`. No source files or the user's existing checkout were changed. The current `dev` source still pins the preview package; each audit build passed `WindowsSdkPackageVersion` explicitly on the command line.

### Stable package 10.0.26100.87 — exact shipped files

Package SHA-256: `2C1C57CAE573EBCA97F41CD257650161D5E610C009DDBD99CFC0774AF01F0663`. The package has 104 archive entries, five `lib/net9.0` files, 91 WinMD files, one analyzer and two data files. Its NuGet signature verification succeeded on Windows with `dotnet nuget verify --all` (exit code 0).

The current Release profile has `PublishTrimmed=true` and `PublishReadyToRun=true`. With those settings, the two SDK-derived DLLs shipped in the MSIX are transformed and do not match their package-source hashes:

| File | Package source | x64 MSIX | ARM64 MSIX |
| --- | --- | --- | --- |
| `Microsoft.Windows.SDK.NET.dll` | 27,249,976 bytes; file version 10.0.26100.86; SHA-256 `C31A32FEBB8798D7D8A0BCBF2F2373D34ACEF3B3EB9AAC5BDDE2F94698098C12` | 425,984 bytes; PE AMD64; SHA-256 `59DB24E312D83F2785C716C48A139EAE445A81F3680BFA6111E62DC6656696D7` | 487,424 bytes; PE ARM64; SHA-256 `9F1EAD9C6754207DD223C7A980FFD54A51444F46530F7C667275E8747094D3D9` |
| `WinRT.Runtime.dll` | 598,840 bytes; file version 2.3.1.54291; SHA-256 `42AC514D4AECDD3137B5F6513270DE8082AF81D9F205A62B7379F0F3F229319C` | 958,464 bytes; PE AMD64; SHA-256 `BFEA2D2026CF89F7A4787D7C24118B601CC9379961EF939454D4C95D1338F48C` | 1,081,344 bytes; PE ARM64; SHA-256 `42DF98B1183DDB06227F5D8D50002BE36B9A642519B4BE48EF01474F41ADD08D` |

Normal-profile MSIX hashes: x64 `F0A7475C77C0B387EE29F271F13C06545F23E77E3F8B88B06A2CBE60E660DCC6` (13,346,656 bytes); ARM64 `892767719868F2B36606C9FE652D3CF8E1704710276B3DBCA63EE91FA62B86DD` (12,884,377 bytes).

The package's `Microsoft.Windows.UI.Xaml.dll`, XML documentation, SDK WinMD files, analyzer and data files are not present in these MSIX payloads. The `Microsoft.CommandPalette.Extensions.winmd` payload is from the separate Command Palette SDK dependency, not this Windows SDK targeting pack. The two DLLs in the table are the only files from this targeting-pack package found in the MSIX payload.

### Unmodified .87 build variant

A second x64/ARM64 MSIX build used `PublishTrimmed=false` and `PublishReadyToRun=false`. Both SDK DLLs were byte-for-byte identical to their .87 package-source files in both architectures (the same source hashes and sizes shown above; PE machine I386/AnyCPU). This demonstrates a build configuration that preserves the package files unmodified, but it does not resolve the REDIST page's omission of `lib/net9.0`.

Unmodified-variant MSIX hashes: x64 `71FBB4D029DF194BFFFF188010BB418E45229F66C3040E755A35A9A62E457C68` (43,935,374 bytes); ARM64 `909762EB3D012771DAEEEC60AC8FEA33C2C45BB4B7C26602BBDB177057255F28` (42,548,190 bytes).

### Candidate stable package 10.0.26100.84

A separate candidate was tested without changing the application target framework. NuGet identifies `10.0.26100.84` as a stable package with `lib/net8.0` assets and computed compatibility with .NET 10. The application remains `net10.0-windows10.0.26100.0`; restore, x64/ARM64 MSIX builds, and the test suite succeeded against this package.

Package SHA-256: `2EFF36ACCB9D76466D36E27E7199D375C662818D2C1376E65125D48BA4BD1C96`. It has five `lib/net8.0` files, 91 WinMD files, one analyzer and two data files. Its NuGet signature verification succeeded on Windows with `dotnet nuget verify --all` (exit code 0).

With trimming and ReadyToRun disabled, the MSIX files contain the exact unmodified package-source DLLs in both architectures:

| File | Package source and x64/ARM64 MSIX payload |
| --- | --- |
| `Microsoft.Windows.SDK.NET.dll` | 27,196,448 bytes; file version 10.0.26100.82; SHA-256 `CFF6F50D9E25D5C8148EE47C0860B1E13D5826448C52C6FE677577D051C5A48F`; exact hash match in x64 and ARM64 |
| `WinRT.Runtime.dll` | 528,944 bytes; file version 2.2.0.48161; SHA-256 `BCF3A14E8712A90837FC5C0D8C8A24696AF2BD7F74E9767597EC81EDEDFB23DB`; exact hash match in x64 and ARM64 |

Candidate .84 MSIX hashes: x64 `66DBDD4B3BE147331E29809FE94D553625611B3C5B4C3F84B6F1B61DFA4A99B8` (43,857,942 bytes); ARM64 `DC53C85E3FE166703D3C445796C6098BBD03DE0683DB50CBB3F050005D97883C` (42,470,759 bytes). Both packages contain the project `LICENSE`, `THIRD-PARTY-NOTICES.md`, `dotnet-runtime-THIRD-PARTY-NOTICES.txt`, and `Microsoft-PowerToys-LICENSE.txt`.

This .84 candidate aligns with the REDIST entry's listed `lib/net8.0` path and preserves the two SDK DLLs unmodified. It has not been selected or pinned in `dev`; the WERD-77 branch currently selects .87. The candidate MSIX files were unsigned and were not installed or runtime-tested. A full solution test run had one environment-only failure because `pwsh.exe` (PowerShell 7) is absent; rerunning with that single PowerShell-7-dependent test excluded passed 341 tests (107 Core, 3 Command, 231 Playnite). This is build/test evidence, not an install/activation test.

### SDK licence text comparison

The downloaded licence RTF SHA-256 remains `DD07EB178E00C6BBA4148457FC00FF77CD4887EB521D504186FE59C9EC8BBE62`. Its extracted text and Microsoft's maintained plain-text copy at [microsoft/win32metadata](https://github.com/microsoft/win32metadata/blob/main/licenses/sdk_license.txt) both identify EULA ID `WIN10SDK.RTM.AUG_2018_en-US`. A normalized token comparison produced 4,307 tokens on each side with an exact token-sequence match after normalizing whitespace, bullet glyphs and hyperlink/URL markup. No substantive text difference was detected. Byte-for-byte equality is not applicable between RTF and plain text.

### Distribution-channel terms review

The Windows SDK licence's §2.a conditions include significant-primary-functionality, unmodified-file, downstream distributor/end-user terms at least as protective as the SDK agreement, copyright notices, and Microsoft indemnification/defence obligations. The following public terms were reviewed as context, not as a legal opinion:

- **Microsoft Store:** the App Developer Agreement v8.10 (effective 2025-10-14) makes the publisher responsible for third-party licence compliance and ensuring Microsoft has necessary rights (§3(c)); identifies the publisher as the distributor (§4(a)); permits a publisher EULA or applies the Store SALT by default (§4(i)); and requires the publisher to defend and indemnify Covered Parties, including Microsoft, for specified third-party claims (§10(c)). The SALT includes Microsoft among Covered Parties for warranty/liability provisions. This is a potentially workable channel, but counsel must determine whether the exact SALT/custom EULA and indemnity satisfy the Windows SDK licence.
- **GitHub Releases:** a release asset download does not provide a native enforced end-user EULA acceptance step. The project's MIT licence and third-party notices alone do not demonstrate that external users agree to the Windows SDK terms. A separate enforceable agreement/installation flow or a different distribution approach is needed unless counsel confirms the existing flow is sufficient.
- **WinGet:** the community manifest schema supports `Agreements` that users must accept before download and subsequent install/upgrade. The agreement body is restricted to verified developers; other submitters may provide a label and URL. `--accept-package-agreements` can accept the displayed agreements. This may provide a consent step, but developer verification status and legal sufficiency must be confirmed.

No independent legal opinion has been obtained. No written Microsoft confirmation has been obtained. Do not mark WERD-76 complete or approve public binary distribution until the package choice, any required publish-profile changes, and channel-specific terms/indemnification are reviewed.

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

  The initial generator run was executed on the supported Windows build machine with Windows PowerShell 5.1 after a successful Release restore on 2026-11-01. Its baseline output was `artifacts/dependency-inventory.json` (git-ignored), SHA-256: `C90B5B2E1E286D1967BEF3740208D2571CD7565E0E296EAF5B524B60EA2C48E2`; it classified 7 packages as shipped-runtime, 4 as shipped-build, 13 as test-only, and reported 13 review flags. That baseline incorrectly treated three project references as NuGet packages and treated NuGet's `requireLicenseAcceptance` metadata as an automatic redistribution review. The generator has now been corrected to record project references separately and to report the acceptance flag without automatically treating it as a redistribution restriction. Re-run it on Windows before relying on updated counts or a new hash. Package-level evidence collected on 2026-11-01: CsWinRT 2.2.0 declares MIT in its package license file and has `requireLicenseAcceptance=false`; WinRTServer 2.1.1 includes its MIT license (copyright 2022 Shmueli Englard) and declares `requireLicenseAcceptance=true`; the MSIX BuildTools package contains Microsoft Windows SDK license terms; the ReferenceAssemblies and xunit.abstractions packages expose license URLs rather than local license files. A corrected Windows PowerShell 5.1 inventory run classified 4 packages as shipped-runtime, 4 as shipped-build, 13 as test-only, 0 as developer-tool and 0 as unclassified; it reported 5 review flags, 4 in the shipped classifications. The generated inventory JSON SHA-256 for this run is `A32B318636DC6AC98974524DBD014F5C915C80E967281FC50C5A5C86F51F174C`. The review flag count is an audit queue, not a count of binaries redistributed: the actual publish scan found no BuildTools, ReferenceAssemblies or xUnit/test-platform files in either RID output. It found `Shmuelie.WinRTServer.dll` and the three project-owned assemblies in both RIDs. A subsequent explicit scan found `WinRT.Runtime.dll` and `Microsoft.Windows.SDK.NET.dll` in both RIDs. The resolved `PlayniteCmdPal/obj/project.assets.json` confirms `WinRT.Runtime.dll` is a runtime and compile asset of `Microsoft.Windows.CsWinRT` 2.2.0 (`lib/net8.0/WinRT.Runtime.dll`); the package's own `Microsoft.Windows.CsWinRT.dll` is not the redistributed runtime assembly. The Windows SDK projection package has since been migrated to stable `Microsoft.Windows.SDK.NET.Ref` 10.0.26100.87 under WERD-77. The stable package archive hash and package inventory are recorded under open item 1. Stable-package x64/ARM64 publish and MSIX payloads have now been inventoried on the current `dev` base with explicit package-version overrides. The .87 Release profile transforms the SDK-derived DLLs through trimming and ReadyToRun; byte-identical .87 and .84 variants were also built with those transforms disabled. See **Stable Windows SDK payload audit** below. Legal scope and downstream terms remain unresolved. Do not infer clearance from package metadata alone. Transitive dependencies reached only through version ranges (for example `System.Reflection.Metadata >= 1.6.0`, `Newtonsoft.Json >= 13.0.1`) resolve to whatever NuGet selects at restore time; those resolved versions must come from the regenerated inventory.
- Publish profiles, project properties and `scripts/package-playnite-plugin.ps1` were read to determine what each artifact actually contains. Trimming, single-file settings and `ExcludeAssets` change the shipped set, so the checklist below also requires inspecting an actual publish/MSIX layout.

### Redistributed runtime dependencies — Command Palette extension

| Package | Version | Declared license (source) | Role in the artifact | Assessment |
| --- | --- | --- | --- | --- |
| Microsoft.CommandPalette.Extensions | 0.9.260303001 | MIT (`licenseExpression`; © Microsoft Corporation) | SDK/toolkit assemblies and the native `Microsoft.CommandPalette.Extensions.dll` used at runtime | Compatible. Ship the MIT notice. |
| Microsoft.Windows.CsWinRT | 2.2.0 | MIT (`LICENSE`; © Microsoft Corporation); `requireLicenseAcceptance=false` | C#/WinRT projection/build support. `Microsoft.Windows.CsWinRT.dll` was not found in either inspected publish output; `WinRT.Runtime.dll` is present and the resolved assets identify it as a runtime/compile asset of this package (`lib/net8.0/WinRT.Runtime.dll`) | MIT terms identified. Keep the MIT notice where required for distributed material covered by this package. |
| Shmuelie.WinRTServer | 2.1.1 | MIT (`licenseExpression`; © 2022 Shmueli Englard); no dependencies | Out-of-process WinRT/COM server used to host the extension; its DLL is present in both inspected publish directories | MIT terms identified. The NuGet acceptance flag is not, by itself, a redistribution prohibition. Include the MIT notice. |
| Microsoft.Windows.SDK.NET.Ref | 10.0.26100.87 | Microsoft Windows SDK license (`licenseUrl` → `https://aka.ms/WinSDKLicenseURL`); not an OSI license | .NET 9 targeting-pack package used by the `net10.0-windows10.0.26100.0` application. The inspected stable `.nupkg` contains 104 entries, including `lib/net9.0` assets and 91 WinMD files. The x64/ARM64 shipped assets are now inventoried; the normal .87 Release output transforms the two SDK-derived DLLs, and the byte-identical .84 candidate is documented below | **Not yet cleared.** Microsoft's REDIST page names this package, but its enumerated framework assets are `net8.0` and `net6.0`; it does not expressly enumerate the stable package's `net9.0` asset group. Obtain authoritative scope confirmation and inspect actual stable per-RID outputs — see open item 1. |
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
| Windows SDK projections | Microsoft's official [Windows SDK REDIST.TXT](https://learn.microsoft.com/en-us/legal/windows-sdk/redist) has a dedicated `Microsoft.Windows.SDK.NET.Ref` entry and permits the listed package files to be redistributed unmodified as a NuGet package or as part of a program enabling WinRT API calls. The inspected stable package is `10.0.26100.87` and contains `net9.0` assets. The REDIST entry's visible file list names `net8.0` and `net6.0` groups, not `net9.0`. Applicability to the stable package's exact contents therefore remains unconfirmed. Required license terms, distributor/end-user flow and per-RID shipped files are also open (open item 1). |
| Source-availability offers / reciprocal licenses | None identified. No dependency uses GPL, LGPL, AGPL, SSPL, EUPL, MPL or similar terms; the only non-OSI component is the Microsoft Windows SDK license |
| Trademark and artwork permissions | `third-party/THIRD-PARTY-NOTICES.md` records that no trademark rights are granted and that `PlayniteCmdPal/Assets/` holds original artwork only |

### Open items requiring maintainer or qualified legal review

1. **Windows SDK projection redistribution — WERD-76; unresolved release blocker.** The project has selected `Microsoft.Windows.SDK.NET.Ref 10.0.26100.87` through the WERD-77 change. NuGet identifies this as the Windows SDK targeting pack for .NET 9 and later, with compatibility for .NET 10 ([exact package/version](https://www.nuget.org/packages/Microsoft.Windows.SDK.NET.Ref/10.0.26100.87)). Its licence URL is the standard Microsoft Windows SDK licence, `https://aka.ms/WinSDKLicenseURL`, rather than the preview-specific licence URL.

   Microsoft's official [Windows SDK REDIST list](https://learn.microsoft.com/en-us/legal/windows-sdk/redist) contains a dedicated `Microsoft.Windows.SDK.NET.Ref` entry. It states that files included in the package may be distributed unmodified as a NuGet package or as part of a program enabling WinRT API calls. Its enumerated paths include:
   - `./analyzers/dotnet/cs/WinRT.SourceGenerator.dll`
   - `./data/frameworkList.xml`
   - `./data/RuntimeList.xml`
   - `./lib/net8.0/Microsoft.Windows.SDK.NET.dll` and its XML documentation
   - `./lib/net8.0/Microsoft.Windows.UI.Xaml.dll` and its XML documentation
   - `./lib/net8.0/WinRT.Runtime.dll`
   - `./lib/net6.0/Microsoft.Windows.SDK.NET.dll` and its XML documentation
   - `./lib/net6.0/WinRT.Runtime.dll`
   - `./winmd/*.winmd`

   **Stable-package evidence:** The downloaded `Microsoft.Windows.SDK.NET.Ref 10.0.26100.87` package was independently inspected. Its SHA-256 is `2C1C57CAE573EBCA97F41CD257650161D5E610C009DDBD99CFC0774AF01F0663`. Its archive contains 104 entries and passes ZIP integrity validation. Its only framework-specific library directory is `lib/net9.0/`; the `net8.0` and `net6.0` directories enumerated by the REDIST entry are absent. The package contains five `net9.0` library files, the WinRT source generator, framework/runtime lists, and 91 WinMD files. The package inventory CSV matches all 104 archive entries.

   **Scope gap:** Microsoft's REDIST entry does not explicitly enumerate `lib/net9.0` paths. Its general wording about files included in the package supports a broader interpretation, but the enumerated file list does not expressly confirm that the stable package's `net9.0` assets are covered. Do not infer clearance from the package's public availability, NuGet compatibility, or the existence of the package-level REDIST entry alone.

   The downloaded SDK licence RTF was also independently hashed. SHA-256: `DD07EB178E00C6BBA4148457FC00FF77CD4887EB521D504186FE59C9EC8BBE62`. Its text identifies the Microsoft Windows Software Development Kit licence, EULA ID `WIN10SDK.RTM.AUG_2018_en-US`. The licence's §2.a covers distributable code listed in REDIST.TXT and imposes conditions including significant primary functionality, downstream distributor/end-user terms at least as protective as the SDK agreement, a valid copyright notice, and indemnification, defence and hold-harmless obligations for Microsoft. §2.a.iii also imposes distribution restrictions. These conditions require review against the project's actual licence and distribution model; engineering review is not legal approval.

   Microsoft's [win32metadata licence text](https://github.com/microsoft/win32metadata/blob/main/licenses/sdk_license.txt) provides a Microsoft-maintained copy of the SDK licence. Microsoft's [Windows App SDK discussion #4368](https://github.com/microsoft/WindowsAppSDK/discussions/4368) identifies `Microsoft.Windows.SDK.NET.dll` as a Windows Platform SDK projection and points to the Windows SDK NuGet licence, but the maintainer disclaims legal expertise. A [Microsoft Q&A response](https://learn.microsoft.com/en-us/answers/questions/1696668/can-i-redistribute-microsoft-windows-sdk-net-dll) claims the DLL may not be redistributed, but cites no licence clause or REDIST entry and is community guidance, not a licence amendment. These sources do not resolve the `net9.0` scope question.

   **Project-output evidence remains incomplete for the stable package.** Earlier Windows validation against the preview package `10.0.26100.68-preview` found `Microsoft.Windows.SDK.NET.dll` in both x64 and ARM64 self-contained publish outputs. That is historical preview-package evidence, not proof of the stable package's exact output contents. The inspected stable `.nupkg` does not establish which files are copied into the current stable-package publish outputs or final distribution artifacts. Record the resolved package assets and inspect fresh x64 and ARM64 publish directories and MSIX archives built against `10.0.26100.87`. Record each SDK-derived output file, source package, assembly version, architecture and SHA-256. Also inspect the actual GitHub Release and WinGet payloads.

   **Status: INDETERMINATE; keep WERD-76 open.** Required closure evidence includes written Microsoft confirmation of `net9.0` asset coverage, stable-package output inventories, PE-level classification of the relevant DLLs, and review of the downstream-terms and indemnification conditions. The stable NuGet package signature was verified successfully on Windows on 2026-11-08; signer details and the verification command are recorded in `third-party/THIRD-PARTY-NOTICES.md`. Do not describe this item as legally cleared or approve public binaries containing affected files until the evidence and required conditions have been addressed.

2. **Stable Windows SDK targeting pack — version selected; validation tracked separately by WERD-77.** The selected version is `Microsoft.Windows.SDK.NET.Ref 10.0.26100.87`, replacing `10.0.26100.68-preview`. WERD-77 tracks the targeting-pack migration and its Windows restore/build/test, Visual Studio deployment, x64/ARM64 publish and package validation. The stable package selection does not itself establish redistribution permission. Keep WERD-76 as the independent licensing and redistribution gate. Do not change the selected version based solely on the newer `10.0.28000.87` package: that package belongs to the Windows SDK 28000 generation, while this project targets `net10.0-windows10.0.26100.0`. Any change requires a separate compatibility decision and Windows validation.

3. **.NET runtime notices — x64 and ARM64 MSIX payloads verified.** The filesystem-publish notice files for both RIDs match their respective restored runtime-pack notice files (SHA-256 `6D15E10A101C6BFFF2AB4429ED061BF76C456FC4B23AD6B03E0D0F8377148A21`). The maintainer rebuilt both architecture MSIX packages after adding the exact runtime-pack notice directly to `AppxPackagePayload`. Direct archive inspection confirmed `dotnet-runtime-THIRD-PARTY-NOTICES.txt` is present in both packages, alongside the project `LICENSE`, `THIRD-PARTY-NOTICES.md` and `Microsoft-PowerToys-LICENSE.txt`. The runtime-notice packaging defect is resolved for the inspected builds. Release notes must still reference the .NET Library License.

4. **Dependency inventory and publish-payload verification — Windows evidence recorded.** The corrected Windows PowerShell 5.1 inventory run completed with 4 shipped-runtime, 4 shipped-build, 13 test-only, 0 developer-tool and 0 unclassified packages. Its git-ignored JSON SHA-256 is `A32B318636DC6AC98974524DBD014F5C915C80E967281FC50C5A5C86F51F174C`. Five review flags remain, four within shipped classifications; these flags are not a count of redistributed binaries. The inspected publish outputs do not contain the searched BuildTools, ReferenceAssemblies, xUnit or test-platform files. Both architecture MSIX archives contain the required notices and `Microsoft.CommandPalette.Extensions.winmd`. Disabling WinMD registration harvesting removed APPX1707 on both builds while retaining the WinMD payload. The `.pext` archive was also inspected and contains `LICENSE` and `THIRD-PARTY-NOTICES.md`; `System.Text.Json.dll` and `PlayniteSDK.dll` are absent as expected. These results are evidence for the inspected builds and do not substitute for the stable Windows SDK redistribution inventory required by WERD-76.

5. **Package signing, installation and activation — historical x64 local test; production signing tracked by WERD-79.** The current project and manifest publisher is `CN=B5EE8A57-B227-4726-A4E9-AF40E9D47E70`. The maintainer previously created a local test-signing certificate with subject `CN=PlayniteCmdPal`, imported it into the local machine's Trusted People store, signed an x64 MSIX, verified the signature, installed the package and confirmed that the extension appeared and worked in Command Palette. That historical test used a different publisher identity and does not establish that a package built with the current publisher identity can be signed and installed. WERD-79 tracks the production signing path. ARM64 installation and activation have not been tested on ARM64 hardware.

**Release-gate summary:** WERD-76 remains an unresolved redistribution blocker because stable-package `net9.0` coverage, the complete downstream obligations, and actual stable-package output contents have not been established. WERD-77 tracks the separate SDK migration and validation. The .NET runtime-notice packaging defect is resolved for the inspected x64 and ARM64 MSIX builds. Dependency inventory evidence is recorded, with review flags remaining. Production signing and publisher-identity validation remain tracked separately by WERD-79. No build success, package selection, or engineering validation constitutes legal clearance.

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
