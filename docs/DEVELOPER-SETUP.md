# Developer Setup

This guide takes a clean Windows machine to a running build, an automated test run, a deployed Command Palette extension, and an optional real Playnite library check.

**Status (2026-10-31):** Windows CI and CodeQL pass restore, Release build, both test projects (66 core and 183 adapter cases), and the .NET Framework snapshot smoke test on GitHub-hosted runners; see the evidence table below for the exact runs. The maintainer has additionally reported a successful Windows walkthrough: .NET SDK 10.0.403 on Windows 10 build 19044, x64 and ARM64 builds with and without `-p:DefaultLanguage=en-US`, Visual Studio package deployment, and a Command Palette placeholder-page load. The sections below separate **checked-in facts** from **verified on a real machine** so a reader can tell what has actually been exercised. The maintainer develops and tests on **Windows 10 build 19041**, and a full replay of this page on a freshly imaged machine by someone other than the maintainer has **not** been done — that acceptance step is deferred, not passed (see [Operating-system requirements and what has actually been tested](#operating-system-requirements-and-what-has-actually-been-tested)). This page is not evidence that it was done. Do not regenerate the CmdPal template.

Related pages: [Testing](TESTING.md) (what runs where, and how to run it), [Catalog Bridge](CATALOG-BRIDGE.md) (snapshot transport and real-library checklist), [Troubleshooting](#troubleshooting).

## 1. Hardware, OS, and account prerequisites

| Requirement | Why | Source |
|---|---|---|
| Windows with build 19041 or later | Matches `TargetPlatformMinVersion`/`SupportedOSPlatformVersion` in `PlayniteCmdPal/PlayniteCmdPal.csproj`, and PowerToys' own minimum, which is also Windows 10 build 19041. Windows 10 build 19041 is the environment this project is developed and tested on. | Checked in; see [operating-system requirements](#operating-system-requirements-and-what-has-actually-been-tested) |
| x64 or ARM64 Windows | The project declares `win-x64;win-arm64` runtime identifiers and `x64;ARM64` MSBuild platforms. There is no 32-bit target. | Checked in |
| A non-administrator or administrator account that can enable Developer Mode | Local MSIX deployment requires Developer Mode or a trusted signing certificate. | [Microsoft: Developer Mode](https://learn.microsoft.com/en-us/windows/apps/get-started/enable-your-device-for-development) |
| PowerToys installed, with Command Palette **enabled** | The extension is loaded by Command Palette, not by Windows. | [Command Palette overview](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/overview) |
| Playnite (only for real-library checks) | Playnite supplies the SDK assembly at runtime and owns the library. | [Playnite docs](https://api.playnite.link/docs/manual/) |

Microsoft's Command Palette documentation lists "Windows 11 with PowerToys installed" as its prerequisites, while PowerToys itself supports Windows 10 build 19041 and later. Command Palette shipped in PowerToys 0.90; treat that as the floor and update PowerToys if an extension fails to appear.

### Operating-system requirements and what has actually been tested

Six different claims get conflated in reviews. Keep them apart when reporting a result.

| Claim | Status |
|---|---|
| Windows 10 build 19041 or later is the **maintainer's development and test environment** | Current practice. The project is developed and tested on Windows 10 build 19041; see the contributor environment table below. |
| Microsoft's Command Palette documentation **states Windows 11** as a prerequisite | An upstream documentation requirement, recorded as such. It is not evidence that this project fails on Windows 10, and this project has no evidence that it does. |
| This project **officially supports** Windows 10 for Command Palette extensions | Not claimed. Microsoft documents Windows 11; this repository does not overrule that. |
| This project **cannot** run on Windows 10 | Not claimed either. There is no recorded technical incompatibility, and development continues on Windows 10. |
| Windows 11 compatibility of this project has been **independently validated** | Not validated independently. The maintainer's environment is Windows 10, so Windows 11 behaviour is unverified beyond what CI shows. |
| This page has been **replayed on a clean machine** by an independent contributor | **Deferred.** Not done, not passed. See the note below. |

> **Deferred validation: clean-machine setup walkthrough.** Following this page from a freshly imaged Windows machine, by someone other than the maintainer, has not been performed. The reason is environmental rather than technical: the maintainer's development and test machine is the Windows 10 build 19041 environment described here, and no separate clean Windows machine was available for the acceptance test. This step is **deferred**, not completed, and nothing in this repository should be read as a record of a passing clean-machine run. What it would cover: Developer Mode, Visual Studio workloads, .NET 10 SDK, `scripts/ci.ps1`, deploy/reload, and the placeholder-page runtime check, all on a machine that has never built this project. Until then, treat every step as "documented and partly exercised by the maintainer", not "independently verified".

## 2. Enable Developer Mode

1. Open **Settings → System → For developers** (Windows 11) or **Settings → Update & Security → For developers** (Windows 10).
2. Turn on **Developer Mode** and accept the prompt.

Developer Mode is what lets Visual Studio register the signed MSIX package locally. Without it, **Build > Deploy** fails with a deployment/developer-license error rather than a useful compile error.

## 3. Install the Visual Studio workload

The solution targets .NET 10 with Windows App SDK and MSIX packaging. The current official guidance is to install Visual Studio with the **WinUI application development** workload, which pulls in the Windows App SDK and Windows SDK components.

1. Install [Visual Studio](https://visualstudio.microsoft.com/downloads/) (2026 release or a version that ships the .NET 10 tooling).
2. In the **Visual Studio Installer**, choose **Modify** and select:
   - **WinUI application development** (this is the documented workload for WinUI / Windows App SDK projects).
   - On the **Individual components** tab, confirm the **Windows 10/11 SDK** matching the project's target (the project requests SDK package `10.0.26100.68-preview`, so a 26100 SDK or newer).
3. Finish the install and confirm `dotnet --version` reports 10.x in a new terminal.

Alternatives and shortcuts:

- `winget install --id Microsoft.DotNet.SDK.10 --exact --source winget` installs only the SDK. That is enough for restore, build, and tests, but not for MSIX deployment from the IDE.
- `winget configure -f https://aka.ms/winui-config` installs the documented WinUI workload set and enables Developer Mode.

Record what you installed; the reporting checklist in [section 9](#9-reporting-a-setup-problem) asks for it.

**Verified:** Visual Studio was installed and used for package deployment on the maintainer's machine. The exact version and workload list were not captured, so this page does not claim a minimum version.

## 4. Install the .NET 10 SDK

```powershell
dotnet --info
dotnet --list-sdks
```

You need a `10.0.x` SDK on `PATH`. The repository does not pin an SDK with `global.json`, so any current 10.0.x works; the maintainer used `10.0.403`. If more than one SDK is installed, `dotnet --info` shows which one the repository resolves to — include that in any bug report.

The `net462` companion plugin builds with `Microsoft.NETFramework.ReferenceAssemblies`, so you do **not** need a separately installed .NET Framework 4.6.2 targeting pack. You **do** need Windows PowerShell 5.1 for the framework smoke test; it ships with Windows.

## 5. Restore, build, and test with one command

From the repository root in PowerShell:

```powershell
.\scripts\ci.ps1
```

This is the same entry point Windows CI and the CodeQL workflow call. It runs, in order:

1. `dotnet --info`
2. `dotnet restore .\PlayniteCmdPal.sln`
3. `dotnet build .\PlayniteCmdPal.sln --configuration Release --no-restore`
4. `dotnet test .\PlayniteCmdPal.Core.Tests\PlayniteCmdPal.Core.Tests.csproj --configuration Release --no-build --no-restore`
5. `dotnet test .\PlayniteCmdPal.Playnite.Tests\PlayniteCmdPal.Playnite.Tests.csproj --configuration Release --no-build --no-restore`
6. `scripts/test-playnite-snapshot-netfx.ps1` against the built `net462` assemblies, in Windows PowerShell 5.1

It stops at the first failure, prints the step name and exit code, and returns that code. No Visual Studio, Playnite installation, personal library, PowerToys install, or secret is needed for these six steps.

Expected outcome: each step reports success and the script ends with `All local CI checks passed.` Each `dotnet test` summary should read `Failed: 0`.

Do not maintain a second copy of these commands in your workflow: if you need a new check, add a step to `scripts/ci.ps1` so CI and local runs stay identical. See [Testing](TESTING.md) for per-project and per-test invocation, plus the coverage map.

## 6. Debug builds and architecture variants (optional)

The shared script builds the solution's default configuration. To reproduce the maintainer's architecture-specific builds:

```powershell
dotnet build .\PlayniteCmdPal.sln --no-restore -c Debug -p:Platform=x64
dotnet build .\PlayniteCmdPal.sln --no-restore -c Debug -p:Platform=ARM64
```

Both should return exit code 0 and produce `win-x64` / `win-arm64` output. `-p:DefaultLanguage=en-US` is no longer required because the source manifest pins `<Resource Language="en-US"/>`; see [Manifest language fix](#manifest-language-fix). Only add the override when you are diagnosing a localization-related `APPX1685` failure.

**Verified:** restore plus all four build commands (x64 and ARM64, with and without the override) returned exit code 0 on the maintainer's machine.

## 7. Deploy the extension and reload Command Palette

1. Open `PlayniteCmdPal.sln` in Visual Studio.
2. Select **Debug** and the platform that matches your machine (`x64` or `ARM64`).
3. **Build > Deploy PlayniteCmdPal**. A plain build does not register or update the MSIX package.
4. Select the `PlayniteCmdPal (Package)` launch profile. It sets `doNotLaunchApp: true` because Command Palette activates the extension as an out-of-process COM server; launching it directly does nothing useful.
5. Start PowerToys and make sure Command Palette is enabled.
6. In Command Palette, type `Reload` and choose **Reload Command Palette extensions** (the entry whose subtitle mentions extensions).
7. Scroll to the bottom of the command list and open `PlayniteCmdPal`. The unmodified template shows `TODO: Implement your extension here`.

**Verified:** package deployment, extension reload, and the placeholder-page load succeeded once on the maintainer's machine. From this point the repository's search/launch page is still the template placeholder; see the README status for the current UI scope.

### The debug loop

1. Edit code, then redeploy with **Build > Deploy**.
2. Reload Command Palette extensions (step 6 above). Command Palette caches extension instances; without a reload your change is not visible.
3. Watch **View > Output > Debug** in Visual Studio, and the Windows **Event Viewer** if the process fails to start.
4. For breakpoints in the extension process, attach to the `PlayniteCmdPal` process after Command Palette has loaded it, or start the packaged profile under the debugger if your Visual Studio configuration supports it.

## 8. Package and install the Playnite companion plugin (optional, Windows)

Only needed to exercise a real Playnite library:

```powershell
.\scripts\package-playnite-plugin.ps1
```

The script first runs the synthetic .NET Framework smoke test against the published assemblies, then writes `artifacts/playnite-plugin/PlayniteCmdPalBridge.pext`.

Install it by dragging the `.pext` onto the main Playnite window and confirming the prompts, or run `Playnite.DesktopApp.exe --installext <path-to.pext>`. For iterative work you can instead register `artifacts/playnite-plugin/publish` under **Settings → For developers → External extensions → Add**, enable it, and restart Playnite.

Then follow the real-library checklist in [Catalog Bridge](CATALOG-BRIDGE.md#manual-playnite-verification-still-required) and, if you want an aggregate-only readout, run:

```powershell
dotnet run --project .\tools\CatalogVerifier\CatalogVerifier.csproj --configuration Release
```

The verifier prints availability, counts, staleness, and an issue code — never titles, IDs, or file contents. Do not commit the `.pext`, publish output, or any catalog export.

## 9. Reporting a setup problem

Run this and include the output, along with the failing step from `scripts/ci.ps1` and the full error text:

```powershell
dotnet --info
dotnet --list-sdks
winver

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (Test-Path $vswhere) { & $vswhere -all -products * -format json -utf8 } else {
    'vswhere.exe not found; record the Visual Studio version from Help > About and workloads from Visual Studio Installer.'
}

Get-AppxPackage Microsoft.PowerToys | Select-Object Name, Version, Architecture
```

When opening an issue, state your Windows build, .NET SDK, Visual Studio version and workloads, PowerToys version, target platform (`x64`/`ARM64`), the exact command, and the sanitized error. Redact local paths, account names, and anything from a real library.

## Checked-in toolchain

Source of truth: `PlayniteCmdPal.sln`, `PlayniteCmdPal/PlayniteCmdPal.csproj`, `Directory.Build.props`, and `Directory.Packages.props`.

- Target framework: `net10.0-windows10.0.26100.0`.
- Target Windows SDK package property: `10.0.26100.68-preview`.
- Minimum and supported OS platform version: `10.0.19041.0` (Windows 10, build 19041).
- Runtime identifiers: `win-x64;win-arm64`.
- MSBuild platforms: `x64;ARM64`; the solution also declares `x86` configurations, but the shared props list only `x64;ARM64`.
- Central package management is enabled (`Directory.Packages.props`).
- Package versions pinned in `Directory.Packages.props`:

| Package | Version |
|---|---|
| `Microsoft.CommandPalette.Extensions` | `0.9.260303001` |
| `Microsoft.CodeAnalysis.NetAnalyzers` | `9.0.0-preview.24508.2` |
| `Microsoft.NET.Test.Sdk` | `17.13.0` |
| `Microsoft.NETFramework.ReferenceAssemblies` | `1.0.3` |
| `Microsoft.Web.WebView2` | `1.0.3719.77` |
| `Microsoft.Windows.CsWin32` | `0.3.183` |
| `Microsoft.Windows.CsWinRT` | `2.2.0` |
| `Microsoft.Windows.SDK.BuildTools` | `10.0.26100.4188` |
| `Microsoft.Windows.SDK.BuildTools.MSIX` | `1.7.20250829.1` |
| `Microsoft.WindowsAppSDK` | `2.0.1` |
| `PlayniteSDK` | `6.18.0` (companion plugin; runtime assembly supplied by Playnite) |
| `Shmuelie.WinRTServer` | `2.1.1` |
| `StyleCop.Analyzers` | `1.2.0-beta.556` |
| `xunit` | `2.9.3` |
| `xunit.runner.visualstudio` | `3.0.1` |

The repository does not pin an SDK with `global.json`. The maintainer reports using .NET SDK `10.0.403`, which the checked-in target framework requires because .NET 10 SDK support is needed for the `net10.0-windows` target.

## Solution layout

| Path | Target | Purpose |
|---|---|---|
| `PlayniteCmdPal/` | `net10.0-windows10.0.26100.0` | Generated CmdPal extension host (out-of-process COM server) |
| `PlayniteCmdPal.Core/` | `net10.0` | SDK-independent game model and deterministic search |
| `PlayniteCmdPal.Snapshot/` | `net462;net10.0` | Shared v1 snapshot contract, serializer, publisher, export coordinator |
| `PlayniteCmdPal.Playnite/` | `net10.0` | Strict parser and read-only catalog adapter/cache |
| `PlayniteCmdPal.PlaynitePlugin/` | `net462` | Playnite companion plugin (generic plugin) |
| `PlayniteCmdPal.Core.Tests/` | `net10.0` | Domain and search tests (no Playnite) |
| `PlayniteCmdPal.Playnite.Tests/` | `net10.0` | Snapshot/adapter/publisher contract tests (no Playnite) |
| `tools/CatalogVerifier/` | `net10.0` | Aggregate-only real-library diagnostic |
| `scripts/` | PowerShell | `ci.ps1`, plugin packaging, framework smoke test, bridge diagnostics, release helpers |

## Shared local CI checks

The exact steps and expected outcome are in [step 5](#5-restore-build-and-test-with-one-command). Requirements: Windows with Windows PowerShell 5.1 (the script itself may be launched from `pwsh` 7), the .NET 10 SDK, and network access for NuGet restore.

The Windows CI and CodeQL workflows install the SDK on `windows-latest` and invoke `scripts/ci.ps1`; CodeQL initializes its analyzer before running it. Neither workflow deploys the MSIX package, installs the Playnite plugin, or reads a real library.

## Playnite catalog bridge

The companion plugin targets `net462` and uses `PlayniteSDK` 6.18.0 with `ExcludeAssets="runtime"` so Playnite supplies the SDK assembly at runtime. Build-time targeting uses `Microsoft.NETFramework.ReferenceAssemblies`. The shared snapshot contract targets both `net462` and `net10.0`; the adapter and strict parser target `net10.0`. The plugin's writer uses framework-provided JSON APIs instead of `System.Text.Json` to avoid the reported extension dependency-binding failure. No Playnite installation or personal library is required for restore, build, or the automated tests.

Packaging, installation, and the real-library checklist are in [step 8](#8-package-and-install-the-playnite-companion-plugin-optional-windows) and [Catalog Bridge](CATALOG-BRIDGE.md). The adapter is consumed by the Command Palette presentation layer; this bridge does not add search/launch UI.

## Manifest language fix

The manifest originally used `<Resource Language="x-generate"/>`. The reported build selected `en-AE` as its default language and failed with `APPX1685`. This extension has no localized resources; `en-US` is a valid BCP-47 language tag and a suitable explicit default. The source manifest now declares `<Resource Language="en-US"/>`, removing dependence on the build machine's locale. Packaging validation remains enabled; no warning or error is suppressed.

Microsoft documents that `x-generate` is expanded using project resource qualifiers and that the first generated resource language is the package default. See [manifest generation](https://learn.microsoft.com/en-us/uwp/schemas/appxpackage/uapmanifestschema/generate-package-manifest) and [language/region management](https://github.com/MicrosoftDocs/windows-dev-docs/blob/docs/hub/apps/design/globalizing/manage-language-and-region.md).

## Restore and build verification

Run from a clean Windows checkout using PowerShell and capture exit codes with the complete output.

```powershell
dotnet --info
dotnet restore .\PlayniteCmdPal.sln
$restoreExit = $LASTEXITCODE
dotnet build .\PlayniteCmdPal.sln --no-restore -c Debug -p:DefaultLanguage=en-US -p:Platform=x64
$x64Exit = $LASTEXITCODE
dotnet build .\PlayniteCmdPal.sln --no-restore -c Debug -p:DefaultLanguage=en-US -p:Platform=ARM64
$arm64Exit = $LASTEXITCODE
"$restoreExit $x64Exit $arm64Exit"
```

The explicit `DefaultLanguage=en-US` is kept here only as a deterministic diagnostic. Run the normal builds without it to prove the source fix is sufficient:

```powershell
dotnet build .\PlayniteCmdPal.sln --no-restore -c Debug -p:Platform=x64
dotnet build .\PlayniteCmdPal.sln --no-restore -c Debug -p:Platform=ARM64
```

**Observed on the maintainer's Windows machine (SDK 10.0.403, Windows 10 build 19041):** restore completed with exit code 0, and all four builds returned exit code 0. Outputs targeted `win-x64` and `win-arm64`. The earlier `APPX1685` failure with default language `en-AE` was resolved by the explicit `en-US` manifest resource language. These results were produced locally by the maintainer, not by an independent reviewer.

## Verification evidence

Recorded run identifiers, with what each run actually proves:

| Run | Workflow | Commit/branch | Outcome | Proves |
|---|---|---|---|---|
| [41177945707](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/41177945707) | Windows CI | `arena/496569d8-playnitecmdpal` | success, 122 s | The shared script passed restore, Release build, both test projects, and the .NET Framework smoke test on a GitHub-hosted Windows runner. |
| [41177945516](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/41177945516) | CodeQL | `arena/496569d8-playnitecmdpal` | success | The shared script plus CodeQL analysis passed in the same PR. |
| [41179024025](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/41179024025) | Windows CI | `sync-main-pr-template-policy` | success | The baseline check still passes after the policy sync. |
| [41179024026](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/41179024026) | CodeQL | `sync-main-pr-template-policy` | success | Analysis is clean after the policy sync. |
| [41184115994](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/41184115994) | Windows CI | `arena/c6f57282-playnitecmdpal`, commit `aec765b` | success | The shared script passed restore, Release build, 66 core tests, 183 adapter tests, and the .NET Framework smoke step — the first green run with the expanded suites from [Testing](TESTING.md). |
| [41184115933](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/41184115933) | CodeQL | `arena/c6f57282-playnitecmdpal`, commit `aec765b` | success | The same shared script plus CodeQL analysis passed with the expanded suites. |
| [41184813275](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/41184813275) | Windows CI | `arena/c6f57282-playnitecmdpal`, commit `ad1caa5` | success, 1 m 57 s | The final pull-request head still passes every step after the documentation updates and the removal of the temporary diagnostic workflow. |
| [41184813264](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/41184813264) | CodeQL | `arena/c6f57282-playnitecmdpal`, commit `ad1caa5` | success | Analysis also passes on the final head. |
| [41190710365](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/41190710365) | Windows CI | `arena/c6f57282-playnitecmdpal`, commit `82d5afa` | success | Still 66 core and 183 adapter tests passing after the concurrency tests were rewritten to use completion signals instead of fixed sleeps. |
| [41190710362](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/41190710362) | CodeQL | `arena/c6f57282-playnitecmdpal`, commit `82d5afa` | success | The same shared script plus analysis passes on that commit. |
| [41016300900](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/41016300900) | Windows CI | PR #10 head | success | The catalog bridge implementation passed the shared checks before merge. |
| [41016300844](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/41016300844) | CodeQL | PR #10 head | success | Analysis passed for the merged bridge change. |

Earlier runs that matter for context: [40351115311](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/40351115311) (Windows CI, default-branch push), [40351115315](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/40351115315) (CodeQL, default-branch push), [40833436815](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/40833436815) (Windows CI, pull request, first core test), [40833436859](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/40833436859) (CodeQL, pull request), [40835007856](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/40835007856) (shared-script implementation), [40835007751](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/40835007751) (CodeQL on the shared script), and [40490182715](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/40490182715) (scheduled CodeQL).

What these runs do **not** prove: that a clean machine follows this page successfully (deferred, see [operating-system requirements](#operating-system-requirements-and-what-has-actually-been-tested)), that the extension renders a working search page, that the companion plugin behaves correctly against a specific real library, or that any minimum version of Windows, Visual Studio, PowerToys, or Playnite is supported. Those are covered by manual steps in [Catalog Bridge](CATALOG-BRIDGE.md) and tracked under known gaps in [Testing](TESTING.md).

## Dependabot

Dependabot is configured for NuGet and GitHub Actions. Whether it correctly parses Central Package Management in `Directory.Packages.props` remains unverified until a NuGet version-update PR or a relevant Dependabot run appears. The Dependabot *alerts* API returns 403 because alerts are disabled, which is unrelated to version updates. Do not claim NuGet CPM support is verified before that evidence exists.

## Windows build warning notes (captured 2026-10-27)

Windows CI run [40514661047](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/40514661047) used .NET SDK 10.0.403 on a GitHub-hosted Windows x64 runner. Restore and Release build succeeded with exit code 0, targeting `win-arm64`, and reported one `APPX1707` and two `IL2104` warnings.

- **APPX1707:** the MSIX packaging targets report that `Microsoft.CommandPalette.Extensions.winmd` has no implementation file metadata. The build succeeds and the extension was deployed and loaded. Do not invent an implementation file or add `Implementation` metadata for a contract WinMD just to silence it; change this only if documented SDK/template guidance requires a mapping.
- **IL2104:** trim analysis warnings from `Microsoft.Windows.SDK.NET` and `WinRT.Runtime` reference assemblies during the Release build. They are dependency warnings, not build errors. Keep them visible; revisit if trimming becomes part of release acceptance.
- **NETSDK1198:** not present in the captured output for that run. Do not state it was reproduced or fixed. If it appears locally, record the exact command, the selected `Platform`, and the referenced `Properties\PublishProfiles\win-*.pubxml`; the project sets `PublishProfile` from `$(Platform)`, so check for a profile/platform mismatch before changing packaging settings.

The manifest language fix was validated by that CI build without a `DefaultLanguage` override: it used the checked-in `en-US` manifest. CI builds the default solution configuration and does not replace architecture-specific local builds or the manual deployment check.

## Contributor environment reporting

The versions below are the environment in which the template was last validated, not a universal minimum. Record your own versions when reporting results.

| Component | Validated environment / current evidence |
|---|---|
| Windows | Windows 10 build 19041 (maintainer machine) — the project's development and test environment. Windows 11 has not been independently validated. CI uses `windows-latest`, which is not evidence about either desktop OS. |
| .NET SDK | 10.0.403 (SDK 10.0.302 was also installed; the validation commands used 10.0.403) |
| Visual Studio | Installed and used for package deployment; exact version and workloads not captured |
| PowerToys / Command Palette | Package deployed and extension loaded; exact PowerToys version not captured |
| Playnite | 10.63 reported for manual bridge checks; other versions untested |
| Target architectures | x64 and ARM64 builds succeeded; runtime tests were on the maintainer's host architecture |

For Visual Studio, use **Help > About Microsoft Visual Studio** for the version and **Visual Studio Installer > Modify > Workloads** for the workloads. Do not describe a particular release or workload as the minimum supported requirement until it is verified against current Microsoft template documentation and a clean setup.

## Deploy, reload, and debug

Covered step by step in [section 7](#7-deploy-the-extension-and-reload-command-palette). In short: **Build > Deploy** (not just Build) → use the `(Package)` launch profile → `Reload` the Command Palette extensions → open `PlayniteCmdPal` → attach the debugger and watch **Output > Debug**. Record the PowerToys version, Windows build, Visual Studio version/workloads, deploy result, and runtime behavior after each loop.

## Template lifecycle and search behavior

- `PlayniteCmdPal` implements `IExtension` and `IDisposable`; `GetProvider(ProviderType.Commands)` returns the single `PlayniteCmdPalCommandsProvider`. Disposal signals the hosting event.
- The manifest registers the extension as an out-of-process COM server and an app extension named `com.microsoft.commandpalette`. Its CLSID must match the `[Guid]` in the extension class and the manifest registrations.
- `PlayniteCmdPalCommandsProvider` derives from `CommandProvider` and returns one cached top-level `CommandItem` from `TopLevelCommands()`. It does not override `FallbackCommands()`.
- `PlayniteCmdPalPage` derives from static `ListPage`; `GetItems()` returns the placeholder item. It does not override `UpdateSearchText` and does not implement dynamic filtering. For a search-reactive list, Microsoft documents `DynamicListPage.UpdateSearchText(oldSearch, newSearch)`; after changing backing items, call `RaiseItemsChanged()` so the host fetches the new list.
- The official overview says only the Commands provider type is currently supported. Top-level commands come from `TopLevelCommands()`; fallback commands are optional. Context actions are attached to command items and return a `CommandResult` to control dismissal/navigation.

Sources: [create an extension](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension), [extensibility overview](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview), [DynamicListPage.UpdateSearchText](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/microsoft-commandpalette-extensions-toolkit/dynamiclistpage_updatesearchtext), [updating a list of commands](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/update-a-list-of-commands).

## Troubleshooting

| Symptom | Check |
|---|---|
| `dotnet` not found, or an unexpected SDK version | Open a new terminal after installing; run `dotnet --list-sdks` and confirm a `10.0.x` entry. |
| NuGet restore fails | Confirm network/proxy access to your configured feeds (`nuget.config`). Re-run `dotnet restore .\PlayniteCmdPal.sln` alone to isolate it. |
| `APPX1685` mentioning `en-AE` or another locale | Verify the manifest declares `<Resource Language="en-US"/>`; do not disable packaging validation. |
| `NETSDK1198` (publish profile) | Record the command, `Platform`, and referenced `Properties\PublishProfiles\win-*.pubxml`. The project derives `PublishProfile` from `$(Platform)`, so a mismatched `Platform` is the usual cause. |
| Deploy fails with a developer-license or registration error | Enable **Developer Mode** ([section 2](#2-enable-developer-mode)), then retry **Build > Deploy**. |
| Extension does not appear in Command Palette | **Build > Deploy**, then `Reload` → **Reload Command Palette extensions**. Confirm PowerToys is running and Command Palette is enabled. Launching the `(Unpackaged)` profile does not register the package. |
| Extension loads but nothing happens | Verify the three CLSID occurrences agree (C# `[Guid]`, manifest COM server, manifest app extension). |
| Change has no effect after redeploy | Command Palette caches the extension instance; reload extensions after every deploy. |
| Snapshot missing or adapter reports `Missing` | Install/enable the companion plugin ([section 8](#8-package-and-install-the-playnite-companion-plugin-optional-windows)); run `scripts\diagnose-playnite-bridge.ps1`. See [Catalog Bridge troubleshooting](CATALOG-BRIDGE.md#troubleshooting-a-missing-snapshot). |
| Test failure only in CI | Compare `dotnet --info` locally with the CI log; the script prints it first. Check for a path or culture assumption in the new test. |

Diagnostics to capture: **View > Output > Debug**, Windows **Event Viewer**, package registration state (`Get-AppxPackage *PlayniteCmdPal*`), and the architecture you selected. Redact local paths and library data before sharing.

For Playnite-side diagnostics, Playnite writes host loading errors to `playnite.log` and extension logger output to `extensions.log`, under `%APPDATA%\Playnite` for a normal per-user install (or the configured data root for portable installs).

## References

- [Create a Command Palette extension](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension)
- [Extensibility overview](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview)
- [Command Palette overview](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/overview)
- [Set up your Windows development environment](https://learn.microsoft.com/en-us/windows/apps/get-started/start-here)
- [Enable Developer Mode](https://learn.microsoft.com/en-us/windows/apps/get-started/enable-your-device-for-development)
- [Install the .NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [PowerToys Command Palette source](https://github.com/microsoft/PowerToys/tree/main/src/modules/cmdpal)
- [MSIX manifest generation](https://learn.microsoft.com/en-us/uwp/schemas/appxpackage/uapmanifestschema/generate-package-manifest)

## Validation status

- [x] Checked-in solution, project, shared props, package versions, manifest, provider, page, extension lifecycle, and package launch profile inspected.
- [x] Deterministic manifest language fix applied.
- [x] Windows restore succeeded after the fix (`dotnet restore .\PlayniteCmdPal.sln`, exit code 0).
- [x] x64 and ARM64 builds succeeded with and without the `DefaultLanguage=en-US` override (all four builds exit code 0).
- [x] Visual Studio package deployment succeeded; Command Palette extension reload and placeholder-page runtime check succeeded.
- [ ] Exact Windows build recorded. The Windows 10 build 19041 environment is the documented development and test environment, but earlier notes on this page also mention `10.0.19044`, and the two have not been reconciled. Both are Windows 10 2004-family builds; re-check with `winver` before citing a specific build as verified.
- [x] Shared script, both test projects, and the .NET Framework smoke test pass in Windows CI and the CodeQL workflow.
- [ ] Exact installed Visual Studio version/workloads and PowerToys version recorded.
- [ ] **Deferred, not passed:** this page replayed from a fresh Windows image by someone other than the maintainer. Deferred on 2026-10-31 because the maintainer's development and test machine is the Windows 10 build 19041 environment above and no separate clean Windows machine was available. This is environmental, not a recorded technical failure — do not mark it complete without a real clean-machine run.
