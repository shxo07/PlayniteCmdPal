# Third-party notices

This file lists the third-party components that are redistributed with PlayniteCmdPal
distribution artifacts, together with their notices. It is provided to satisfy
attribution and notice-retention obligations; it is not a legal opinion and does not
modify any third-party license.

PlayniteCmdPal-authored material is licensed under the MIT License; see the root
[`LICENSE`](../LICENSE). That grant does not cover any component listed below.

## Where these notices are distributed

| Artifact | Contents | Notices location |
| --- | --- | --- |
| Command Palette extension (MSIX, `win-x64` / `win-arm64`, self-contained) | PlayniteCmdPal application, Microsoft Command Palette SDK, C#/WinRT projection support, Shmuelie.WinRTServer, Microsoft Windows SDK projections, .NET runtime | `THIRD-PARTY-NOTICES.md` and `Microsoft-PowerToys-LICENSE.txt` in the package root |
| Playnite catalog bridge plugin (`.pext`, `net462`) | PlayniteCmdPal plugin and snapshot assemblies; Playnite SDK is provided by Playnite and is not redistributed | `THIRD-PARTY-NOTICES.md` in the package root |

`PlayniteCmdPal/PlayniteCmdPal.csproj` copies this file and
`Microsoft-PowerToys-LICENSE.txt` to the extension's build and filesystem-publish output.
Inspection of the x64 and ARM64 MSIX archives confirmed that both files, the root
`LICENSE`, and `dotnet-runtime-THIRD-PARTY-NOTICES.txt` are present. The runtime notice
is sourced from each RID's restored runtime pack and added directly to the MSIX payload.
The maintainer also signed, installed and activated the x64 test package successfully;
ARM64 installation/activation remains unverified. `PlayniteCmdPal.PlaynitePlugin/PlayniteCmdPal.PlaynitePlugin.csproj`
copies this file to the plugin's build and publish output, and
`scripts/package-playnite-plugin.ps1` fails if it is missing. The audit behind this list
is recorded in `docs/LICENSING-AND-RELEASE.md`.

## MIT-licensed components

The following components are redistributed under the MIT License. Each entry's
copyright line is reproduced with the license text below it.

| Component | Version | Copyright line | Source |
| --- | --- | --- | --- |
| Microsoft.CommandPalette.Extensions (Command Palette Extension SDK) | 0.9.260303001 | Copyright (c) Microsoft Corporation. | <https://www.nuget.org/packages/Microsoft.CommandPalette.Extensions/0.9.260303001/license> |
| Microsoft.Windows.CsWinRT | 2.2.0 | Copyright (c) Microsoft Corporation. | <https://www.nuget.org/packages/Microsoft.Windows.CsWinRT/2.2.0/license> |
| Shmuelie.WinRTServer | 2.1.1 | Copyright (c) 2022 Shmueli Englard | <https://www.nuget.org/packages/Shmuelie.WinRTServer/2.1.1/license> |
| Microsoft PowerToys template material (generated project files) | n/a | Copyright (c) Microsoft Corporation. All rights reserved. | [`Microsoft-PowerToys-LICENSE.txt`](Microsoft-PowerToys-LICENSE.txt) |

```text
MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

The four Microsoft-generated files in `PlayniteCmdPal/` retain their own MIT file
headers and the PowerToys copyright notice. Do not remove those headers.

## Microsoft Windows SDK projections

`Microsoft.Windows.SDK.NET.Ref` 10.0.26100.84 is the selected Windows SDK targeting-pack package for the `net10.0-windows10.0.26100.0` extension. NuGet identifies it as a stable package with `lib/net8.0` assets compatible with the application's .NET 10 target. Package SHA-256: `2EFF36ACCB9D76466D36E27E7199D375C662818D2C1376E65125D48BA4BD1C96`. The archive contains five `lib/net8.0` files, 91 WinMD files, one analyzer and two data files. NuGet signature verification succeeded on Windows with `dotnet nuget verify --all` (exit code 0). The package is licensed under the Microsoft Windows SDK licence, not MIT; NuGet links to `https://aka.ms/WinSDKLicenseURL`.

The two SDK-derived files present in both x64 and ARM64 MSIX payloads are byte-identical to their package-source files:

| File | Version | Size | SHA-256 (package source and both MSIX architectures) |
| --- | --- | ---: | --- |
| `Microsoft.Windows.SDK.NET.dll` | 10.0.26100.82 | 27,196,448 bytes | `CFF6F50D9E25D5C8148EE47C0860B1E13D5826448C52C6FE677577D051C5A48F` |
| `WinRT.Runtime.dll` | 2.2.0.48161 | 528,944 bytes | `BCF3A14E8712A90837FC5C0D8C8A24696AF2BD7F74E9767597EC81EDEDFB23DB` |

The project sets `PublishTrimmed=false` and `PublishReadyToRun=false` for both x64 and ARM64 release profiles. This preserves the SDK projection DLLs unmodified, as required by the REDIST permission. The package's other Windows SDK assets (including XAML projection, WinMD, analyzer and data files) are not present in the MSIX. The Command Palette SDK WinMD is supplied by a separate package.

Microsoft's official [Windows SDK REDIST.TXT](https://learn.microsoft.com/en-us/legal/windows-sdk/redist) has a dedicated `Microsoft.Windows.SDK.NET.Ref` entry. It lists the relevant `lib/net8.0` files, including `Microsoft.Windows.SDK.NET.dll` and `WinRT.Runtime.dll`, for unmodified redistribution as a NuGet package or as part of a WinRT-enabled program. The selected package path and the exact files shipped by this project align with those listed assets. Individual written Microsoft confirmation is not treated as an engineering prerequisite for this path. This is not a legal opinion or a guarantee that every distribution channel meets all separate conditions.

The downloaded SDK licence RTF SHA-256 is `DD07EB178E00C6BBA4148457FC00FF77CD4887EB521D504186FE59C9EC8BBE62`. Its extracted text and Microsoft's maintained plain-text copy at [microsoft/win32metadata](https://github.com/microsoft/win32metadata/blob/main/licenses/sdk_license.txt) both identify EULA ID `WIN10SDK.RTM.AUG_2018_en-US`. A normalized token comparison produced 4,307 tokens on each side with an exact token-sequence match after normalizing whitespace, bullets and hyperlink markup.

The Windows SDK licence §2.a includes conditions concerning significant primary functionality, unmodified files, downstream distributor/end-user terms at least as protective as the SDK agreement, copyright notices, and Microsoft indemnification/defence obligations. Those conditions remain relevant to the project's distribution model and must be considered before public release. The following channel terms were reviewed as context, not as a legal opinion: [Windows SDK licence](https://learn.microsoft.com/en-us/legal/windows-sdk/license), [Windows SDK REDIST.TXT](https://learn.microsoft.com/en-us/legal/windows-sdk/redist), [Microsoft Store App Developer Agreement v8.10](https://cdn-dynmedia-1.microsoft.com/is/content/microsoftcorp/microsoft/store/documents/legal/ada/fy26/MS.Store.ADAv8.10.EN.US.pdf), [WinGet manifest schema 1.28.0](https://github.com/microsoft/winget-pkgs/blob/master/doc/manifest/schema/1.28.0/singleton.md), and [GitHub Terms of Service](https://docs.github.com/en/site-policy/github-terms/github-terms-of-service).

- **Microsoft Store:** the publisher remains responsible for third-party licence compliance; the Store agreement's EULA/SALT and indemnity provisions need to be assessed as part of release readiness.
- **GitHub Releases:** release downloads do not natively enforce a separate SDK end-user agreement. Do not assume the project MIT licence or notices alone satisfy downstream SDK terms.
- **WinGet:** manifest agreements can request acceptance, subject to the community repository's agreement-body restrictions and the publisher's verification status.

The project retains the Windows SDK licence reference and this notice file in the MSIX. No independent legal opinion has been obtained. No public binary release is approved by this package-selection change alone.

## .NET runtime and library components

The Command Palette extension is published with `SelfContained=true`
(`PlayniteCmdPal/Properties/PublishProfiles/win-*.pubxml`), so the package contains the
.NET runtime. Per the .NET project's license information
(<https://github.com/dotnet/core/blob/main/license-information.md>), product
distributions such as runtime packs are licensed on Windows under the
[.NET Library License](https://dotnet.microsoft.com/dotnet_library_license.htm), and the
runtime embeds components under other licenses that are listed in the runtime pack's
`THIRD-PARTY-NOTICES.TXT`.

Obligations for each release:

- The `CopyDotNetRuntimeThirdPartyNotices` MSBuild target copies the runtime's
  `THIRD-PARTY-NOTICES.TXT` to `dotnet-runtime-THIRD-PARTY-NOTICES.txt` after a
  self-contained filesystem publish. It resolves the file from the restored
  `Microsoft.NETCore.App.Runtime.<RID>` package under `$(NuGetPackageRoot)`, so it does
  not depend on `RuntimeFrameworkVersion` (which can be empty at publish time) and does
  not pick up the Windows Desktop, ASP.NET Core or Windows SDK packs. The target fails
  the publish when no notice file or more than one notice file is found. The project now
  also adds that exact runtime-pack file directly to `AppxPackagePayload` immediately
  before `_GenerateAppxPackageFile`. The maintainer inspected the rebuilt x64 and ARM64
  MSIX archives and confirmed the runtime notice is present in both. Re-verify this
  requirement for every release build and RID.
- Reproduce the .NET Library License reference and this notice in the release notes, or
  ship the license text alongside the package.
- Re-check the runtime version and notices for every release; a repository copy would go
  stale at the first servicing update.

The Windows Desktop (WPF) assemblies used by the `net462` catalog bridge are part of
.NET Framework, which Playnite's host provides; this project does not redistribute them.

## Test and build-time components (not distributed)

These components are restored for building and testing only. They are not part of either
published artifact:

| Component | Version | License |
| --- | --- | --- |
| Microsoft.NET.Test.Sdk | 17.13.0 | MIT |
| Microsoft.TestPlatform.TestHost | 17.13.0 | MIT |
| Microsoft.TestPlatform.ObjectModel | 17.13.0 | MIT |
| Microsoft.CodeCoverage | 17.13.0 | MIT |
| xunit, xunit.core, xunit.assert, xunit.extensibility.core, xunit.extensibility.execution | 2.9.3 | Apache-2.0 |
| xunit.analyzers | 1.18.0 | Apache-2.0 |
| xunit.abstractions | 2.0.3 | Apache-2.0 |
| xunit.runner.visualstudio | 3.0.1 | Apache-2.0 |
| Newtonsoft.Json (transitive, test host) | >= 13.0.1 | MIT |
| System.Reflection.Metadata (transitive, test platform) | >= 1.6.0 | MIT |
| NETStandard.Library (transitive, legacy `netstandard1.x` groups only) | 1.6.1 | MIT |
| Microsoft.NETFramework.ReferenceAssemblies (including `.net462`) | 1.0.3 | MIT |
| Microsoft.Windows.SDK.BuildTools.MSIX | 1.7.20250829.1 | Microsoft SDK license terms (`sdk_license.txt`) |

The Apache-2.0 components do not require notices in a binary that does not contain
them; no Apache-2.0 code is redistributed here. If a test project is ever published as
an artifact, add the Apache-2.0 license text and any upstream `NOTICE` content.

`Directory.Packages.props` also declares central versions for
`Microsoft.CodeAnalysis.NetAnalyzers`, `Microsoft.Web.WebView2`, `Microsoft.Windows.CsWin32`,
`Microsoft.Windows.SDK.BuildTools`, `Microsoft.WindowsAppSDK` and `StyleCop.Analyzers`.
No project references them, so they are not restored and not distributed. See the audit
open items for the recommendation to remove or justify them.

## Trademarks and artwork

Playnite, Microsoft, PowerToys, Command Palette, Windows and .NET are trademarks of
their respective owners. This project's MIT license and these notices do not grant
permission to use those trademarks, logos or artwork. The PNG assets under
`PlayniteCmdPal/Assets/` are original PlayniteCmdPal artwork; do not add Playnite or
Microsoft branding without permission.

## Reporting an omission

If a distributed artifact contains a component that is missing from this file, treat it
as a release-blocking defect: open an issue describing the artifact, the component and
the evidence, and do not publish until the notice is added.
