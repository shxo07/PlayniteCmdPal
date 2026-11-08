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

`Microsoft.Windows.SDK.NET.Ref` 10.0.26100.87 is the selected Windows SDK targeting-pack package for the `net10.0-windows10.0.26100.0` extension. NuGet identifies version 10.0.26100.87 as a `net9.0` package compatible with .NET 10 and links its license information to Microsoft's standard Windows SDK license URL, `https://aka.ms/WinSDKLicenseURL`. It is not MIT-licensed. The package-linked SDK licence RTF has been downloaded and hashed (SHA-256 `DD07EB178E00C6BBA4148457FC00FF77CD4887EB521D504186FE59C9EC8BBE62`). Its extracted text identifies the Microsoft Windows Software Development Kit licence, EULA ID `WIN10SDK.RTM.AUG_2018_en-US`. The exact RTF has not yet been compared byte-for-byte with Microsoft's maintained text copy, and the NuGet package signature has not been verified. Do not treat the URL or this notice as a substitute for review of the applicable terms.

Microsoft's official [Windows SDK REDIST.TXT](https://learn.microsoft.com/en-us/legal/windows-sdk/redist) (updated 2024-10-21) has a dedicated `Microsoft.Windows.SDK.NET.Ref` entry. It permits the listed files to be distributed unmodified either as a NuGet package or as part of a program to enable WinRT API calls. The enumerated paths include `Microsoft.Windows.SDK.NET.dll`, `Microsoft.Windows.UI.Xaml.dll` and `WinRT.Runtime.dll` under `lib/net8.0`, plus `Microsoft.Windows.SDK.NET.dll` and `WinRT.Runtime.dll` under `lib/net6.0`, the WinRT source generator, package metadata and WinMD files.

**Unresolved scope:** the stable NuGet package targets `net9.0`, but the REDIST entry's listed framework-specific paths name `net8.0` and `net6.0`; it does not expressly enumerate the stable package's `net9.0` asset group. Obtain written Microsoft confirmation that the REDIST permission covers version `10.0.26100.87` and the exact files in that package before declaring the package cleared.

Additional evidence reviewed 2026-11-08:
- Microsoft's [win32metadata SDK licence text](https://github.com/microsoft/win32metadata/blob/main/licenses/sdk_license.txt) records conditional third-party redistribution rights for REDIST-listed object code, including significant-primary-functionality, unmodified-file, downstream-terms, copyright-notice and Microsoft indemnity requirements. It is a Microsoft-maintained text copy, not yet verified byte-for-byte against the exact NuGet-linked RTF.
- Microsoft's [Windows App SDK discussion #4368](https://github.com/microsoft/WindowsAppSDK/discussions/4368) directs developers to the Windows SDK NuGet licence for `Microsoft.Windows.SDK.NET.dll`; the maintainer disclaims legal expertise.
- A conflicting [Microsoft Q&A answer](https://learn.microsoft.com/en-us/answers/questions/1696668/can-i-redistribute-microsoft-windows-sdk-net-dll) says the DLL may not be redistributed, then advises consulting the package author or legal adviser. This anonymous support answer is not a licence amendment, but the conflict must be resolved.
- NuGet lists newer `10.0.28000.87`, associated with the Windows SDK 28000 line for Windows 11 26H1. This project targets the 26100/24H2 generation; the newer package is not an automatic replacement and has not been adopted or validated.
- The stable `10.0.26100.87` `.nupkg` was downloaded and inspected. SHA-256: `2C1C57CAE573EBCA97F41CD257650161D5E610C009DDBD99CFC0774AF01F0663`. The ZIP archive contains 104 entries and passes integrity validation; its only framework-specific library group is `lib/net9.0` (five library files), and it contains 91 WinMD files. The package inventory CSV matches all 104 archive entries. The package signature has not yet been verified.

The Windows SDK REDIST permission is conditional. The applicable terms require (among other things) unmodified distributable files, significant primary functionality, a valid copyright notice, protective terms binding distributors and external end users, and the specified indemnification. The project's selected distribution channels are Microsoft Store, GitHub Releases and WinGet. The maintainer must review how the distributor and end-user requirements apply to each channel and obtain qualified legal review where necessary.

The package's contents are not the same thing as the files shipped in the application. The stable `.nupkg` file list and SHA-256 are now recorded above, but that package inventory does not establish which files are copied into the application. Separately inspect fresh `win-x64` and `win-arm64` publish/MSIX outputs built against `10.0.26100.87`, and record every SDK-derived file, assembly version, source package, architecture and SHA-256. Verify the package signature and complete PE-level classification. Until the `net9.0` REDIST scope, downstream licence conditions and actual stable-package output inventory are resolved, public binary redistribution remains blocked. See `docs/LICENSING-AND-RELEASE.md`.
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
