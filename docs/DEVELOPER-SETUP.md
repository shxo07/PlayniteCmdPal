# Developer Setup

**Status (2026-10-29):** The maintainer's Windows validation with .NET SDK 10.0.403 and Windows 10.0.19044 succeeded: restore, x64/ARM64 builds with and without `-p:DefaultLanguage=en-US`, Visual Studio package deployment, and a Command Palette placeholder-page smoke test. GitHub-hosted Windows CI and CodeQL have successful runs on `main` and pull requests (details below). The shared `scripts/ci.ps1` entry point cannot be run locally in this Agent sandbox: it is Linux and has no `dotnet`, `pwsh`, or Windows PowerShell executable. Do not regenerate the template.

## Checked-in toolchain

Source of truth: `PlayniteCmdPal.sln`, `PlayniteCmdPal/PlayniteCmdPal.csproj`, `Directory.Build.props`, and `Directory.Packages.props`.

- Target framework: `net10.0-windows10.0.26100.0`.
- Target Windows SDK package property: `10.0.26100.68-preview`.
- Minimum and supported OS platform version: `10.0.19041.0` (Windows 10, build 19041).
- Runtime identifiers: `win-x64;win-arm64`.
- MSBuild platforms: `x64;ARM64`; the solution also declares `x86` configurations, but the shared props list only `x64;ARM64`.
- Central package management is enabled.
- Package versions pinned in `Directory.Packages.props`:
  - `Microsoft.CommandPalette.Extensions` — `0.9.260303001`
  - `Microsoft.CodeAnalysis.NetAnalyzers` — `9.0.0-preview.24508.2`
  - `Microsoft.Web.WebView2` — `1.0.3719.77`
  - `Microsoft.Windows.CsWin32` — `0.3.183`
  - `Microsoft.Windows.CsWinRT` — `2.2.0`
  - `Microsoft.Windows.SDK.BuildTools` — `10.0.26100.4188`
  - `Microsoft.Windows.SDK.BuildTools.MSIX` — `1.7.20250829.1`
  - `Microsoft.WindowsAppSDK` — `2.0.1`
  - `Shmuelie.WinRTServer` — `2.1.1`
  - `StyleCop.Analyzers` — `1.2.0-beta.556`
  - `System.Text.Json` — `9.0.8`
- Exact SDK reported by maintainer: .NET SDK `10.0.403`. The repository does not pin an SDK with `global.json`.
- Visual Studio: solution metadata records Visual Studio `17.13.35507.96` in the generating environment, not a verified minimum. Use Visual Studio with the .NET desktop/.NET 10 SDK and Windows application development tooling required for MSIX/Windows SDK packaging. Exact installed workloads/components still need recording from the machine that successfully builds.
- Runtime requirement: Windows with Command Palette available in a current PowerToys installation, Command Palette enabled/running, and Windows Developer Mode enabled for local deployment as directed by Microsoft.

## Shared local CI checks

Run the shared check script from the repository root in PowerShell:

```powershell
.\scripts\ci.ps1
```

Prerequisites: Windows, PowerShell 5.1 or later (`pwsh` 7 or Windows PowerShell), the .NET 10 SDK (`10.0.x`), and network access for NuGet restore. The script runs the same commands as the Windows CI job, in order:

1. `dotnet --info`
2. `dotnet restore .\PlayniteCmdPal.sln`
3. `dotnet build .\PlayniteCmdPal.sln --configuration Release --no-restore`
4. `dotnet test .\PlayniteCmdPal.Core.Tests\PlayniteCmdPal.Core.Tests.csproj --configuration Release --no-build --no-restore`

It stops at the first failed command, reports the step and its exit code, and returns that code. The Windows CI and CodeQL workflows install the .NET 10 SDK on `windows-latest` and invoke this same script; CodeQL also initializes its analyzer before running the script. No Visual Studio, Playnite installation, personal game library, or secrets are required for these checks. The test step covers the platform-independent core tests; the script does not deploy the MSIX package or verify Playnite integration.

## Manifest language fix

The manifest originally used `<Resource Language="x-generate"/>`. The reported build selected `en-AE` as its default language and failed with `APPX1685`. This extension has no localized resources; `en-US` is a valid BCP-47 language tag and a suitable explicit default. The source manifest now declares `<Resource Language="en-US"/>`, avoiding dependence on the build machine's locale. Packaging validation remains enabled; no warning/error is suppressed.

Microsoft documents that `x-generate` is expanded using project resource qualifiers and that the first generated resource language is the package default. See [manifest generation](https://learn.microsoft.com/en-us/uwp/schemas/appxpackage/uapmanifestschema/generate-package-manifest) and [language/region management](https://github.com/MicrosoftDocs/windows-dev-docs/blob/docs/hub/apps/design/globalizing/manage-language-and-region.md).

## Restore and build verification

Run from a clean Windows checkout using PowerShell. Capture exit codes and complete output.

```powershell
dotnet --info
dotnet restore .\PlayniteCmdPal.sln
$restoreExit = $LASTEXITCODE
dotnet build .\PlayniteCmdPal.sln --no-restore -c Debug -p:DefaultLanguage=en-US -p:Platform=x64
$x64Exit = $LASTEXITCODE
dotnet build .\PlayniteCmdPal.sln --no-restore -c Debug -p:DefaultLanguage=en-US -p:Platform=ARM64
$arm64Exit = $LASTEXITCODE
"restore=$restoreExit x64=$x64Exit arm64=$arm64Exit"
```

The explicit `DefaultLanguage=en-US` is retained in the reproduction command as a deterministic diagnostic, although the source manifest now pins the language. Also run the normal build without the property to prove the source fix is sufficient:

```powershell
dotnet build .\PlayniteCmdPal.sln --no-restore -c Debug -p:Platform=x64
dotnet build .\PlayniteCmdPal.sln --no-restore -c Debug -p:Platform=ARM64
```

**Observed on the maintainer's Windows machine (SDK 10.0.403, OS build 19044):** `dotnet restore .\PlayniteCmdPal.sln` completed successfully (`RESTORE_EXIT=0`). The x64 and ARM64 builds each succeeded with `-p:DefaultLanguage=en-US` and again without the override; all four build commands returned exit code `0`. Outputs targeted `win-x64` and `win-arm64`, respectively. The earlier `APPX1685` failure with default language `en-AE` was resolved by the explicit `en-US` manifest resource language. These results were run locally by the maintainer.

## GitHub Actions verification (2026-10-29)

The GitHub run records were inspected, including actual job conclusions:

- **Windows CI, default-branch push:** [run 40351115311](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/40351115311) on `main` completed successfully (restore and Release build).
- **Windows CI, pull request:** [run 40833436815](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/40833436815) completed successfully, including restore, Release build, and the core search test.
- **Windows CI, WERD-64 implementation PR:** [run 40835007856](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/40835007856) completed successfully on a GitHub-hosted Windows runner; the shared-script step passed.
- **CodeQL, default-branch push:** [run 40351115315](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/40351115315) completed successfully.
- **CodeQL, scheduled main run:** [run 40490182715](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/40490182715) completed successfully.
- **CodeQL, pull request:** [run 40833436859](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/40833436859) completed successfully.
- **CodeQL, WERD-64 implementation PR:** [run 40835007751](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/40835007751) completed successfully; both the shared-script and analysis steps passed.

The main/earlier PR runs establish the baseline workflow status. The WERD-64 implementation PR runs executed the new shared script on Windows and completed restore, Release build, core search tests, and CodeQL analysis successfully. This verifies the script in GitHub-hosted Windows CI; the separate local Windows invocation remains unverified because the Agent sandbox is Linux without PowerShell or the .NET SDK. A default-branch run of the new script will occur after merge and remains a follow-up.

## Dependabot verification (2026-10-29)

`.github/dependabot.yml` requests weekly NuGet and GitHub Actions version updates. GitHub PR history confirms that Actions updates are being recognized: Dependabot PRs [#2](https://github.com/werdscopez7347/PlayniteCmdPal/pull/2) (`github/codeql-action`) and [#3](https://github.com/werdscopez7347/PlayniteCmdPal/pull/3) (`actions/setup-dotnet`) were merged; PR #1 for `actions/checkout` was closed. No NuGet version-update PR is visible in the available PR history, so recognition of this repository's Central Package Management file (`Directory.Packages.props`) remains unverified. The Dependabot alerts API returned 403 because alerts are disabled; that is separate from version-update parsing and does not resolve the NuGet question. Do not claim NuGet CPM is verified until a NuGet update PR or relevant Dependabot run evidence is available.

## Windows build warning notes (captured 2026-10-27)

Windows CI run [40514661047](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/40514661047) used .NET SDK 10.0.403 on a GitHub-hosted Windows x64 runner. Restore and Release build succeeded with exit code 0. The build targeted `win-arm64` and reported three warnings: one `APPX1707` and two `IL2104` warnings. At the time, the solution had no test project; core search tests have since been added and are now run by Windows CI.

- **APPX1707:** MSIX packaging targets report that the SDK package's `Microsoft.CommandPalette.Extensions.winmd` reference has no implementation file metadata. This is emitted by the generated template/package combination during packaging validation. The build still succeeds, and the extension was manually deployed and loaded according to the maintainer's report. Do not invent an implementation file or add `Implementation` metadata for a contract WinMD just to silence the warning; change this only if the SDK/template's documented packaging guidance establishes a required mapping.
- **IL2104:** the trimmer reports warnings in `Microsoft.Windows.SDK.NET` and `WinRT.Runtime` reference assemblies during the Release build. These are dependency/reference-assembly trim-analysis warnings, not build errors. Keep them visible; investigate separately if publishing/trimming becomes part of the release acceptance criteria.
- **NETSDK1198:** this warning was not present in the captured output for run 40514661047. Do not state that it was reproduced or fixed on CI. If it appears locally, capture the exact command, selected `Platform`, and the referenced `Properties\PublishProfiles\win-*.pubxml` file name; the project sets `PublishProfile` from `$(Platform)`, so diagnose the profile/platform mismatch before changing packaging settings.

The source manifest language fix was validated by that CI build without a command-line `DefaultLanguage` override: it used the checked-in manifest, which explicitly declares `en-US`. The maintainer separately reported successful local restore and x64/ARM64 Debug builds both with and without the override. CI builds the default solution configuration; it does not replace architecture-specific local builds or the manual Visual Studio deployment check.

## Contributor environment reporting

The versions below are the environment in which the template was last validated, not a universal minimum. Contributors should record their own versions when reporting build/deployment results; do not infer required minimums from one successful machine.

| Component | Validated environment / current evidence |
|---|---|
| Windows | Windows 10.0.19044 (maintainer machine) |
| .NET SDK | 10.0.403; SDK 10.0.302 was also installed, but the validation commands used 10.0.403 |
| Visual Studio | Installed and used for package deployment; exact version and installed workloads were not captured |
| PowerToys / Command Palette | Package deployed and extension loaded; exact PowerToys version was not captured |
| Target architectures | x64 and ARM64 builds succeeded; runtime test was on the maintainer's machine (record host architecture if reporting architecture-specific runtime tests) |

Run this PowerShell snippet and include the output when reporting a setup/build issue. Missing optional commands are expected; they do not by themselves indicate a broken build.

```powershell
dotnet --info
dotnet --list-sdks
winver

# Visual Studio installation details, if vswhere is present
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (Test-Path $vswhere) {
    & $vswhere -all -products * -format json -utf8
} else {
    'vswhere.exe not found; record the Visual Studio version from Help > About and workloads from Visual Studio Installer.'
}

# PowerToys package version, when installed as an MSIX package
Get-AppxPackage Microsoft.PowerToys | Select-Object Name, Version, Architecture

# Build results
dotnet restore .\PlayniteCmdPal.sln
$restoreExit = $LASTEXITCODE
dotnet build .\PlayniteCmdPal.sln --no-restore -c Debug -p:Platform=x64
$x64Exit = $LASTEXITCODE
dotnet build .\PlayniteCmdPal.sln --no-restore -c Debug -p:Platform=ARM64
$arm64Exit = $LASTEXITCODE
"restore=$restoreExit x64=$x64Exit arm64=$arm64Exit"
```

For Visual Studio, use **Help > About Microsoft Visual Studio** to capture the version and **Visual Studio Installer > Modify > Workloads** to record installed workloads relevant to Windows app/MSIX development. Do not describe a particular Visual Studio release or workload as the minimum supported requirement until verified against the current Microsoft template documentation and a clean setup.

## Deploy, reload, and debug

1. Open `PlayniteCmdPal.sln` in Visual Studio on Windows.
2. Select `Debug` and the intended platform (`x64` or `ARM64`).
3. Use **Build > Deploy PlayniteCmdPal**. A normal build alone does not register/update the MSIX package.
4. Select the `PlayniteCmdPal (Package)` launch profile, not `PlayniteCmdPal (Unpackaged)`. The package profile has `doNotLaunchApp: true` because Command Palette activates the extension as its out-of-process COM server.
5. Start/enable Command Palette in PowerToys. Run `Reload` and select **Reload Command Palette extensions**.
6. Find `PlayniteCmdPal` at the bottom of the command list and open it. The untouched template should display `TODO: Implement your extension here`.
7. For debugging, start the packaged profile as configured, attach/use the Visual Studio debugger for the extension process as appropriate, and inspect **View > Output** (Debug). Reload the extension after each redeploy to force re-instantiation.
8. Record the exact PowerToys version, Windows build, Visual Studio version/workloads, actual deploy result, and runtime behavior after this loop has been performed.

## Template lifecycle and search behavior

- `PlayniteCmdPal` implements `IExtension` and `IDisposable`; `GetProvider(ProviderType.Commands)` returns the single `PlayniteCmdPalCommandsProvider`. Disposal signals the hosting event.
- The manifest registers the extension as an out-of-process COM server and an app extension named `com.microsoft.commandpalette`. Its CLSID must match the `[Guid]` in the extension class and the manifest registrations.
- `PlayniteCmdPalCommandsProvider` derives from `CommandProvider` and returns one cached top-level `CommandItem` from `TopLevelCommands()`. It does not override `FallbackCommands()`.
- `PlayniteCmdPalPage` derives from static `ListPage`; `GetItems()` returns the placeholder item. It does not override `UpdateSearchText` and does not implement dynamic filtering. For a search-reactive list, Microsoft documents `DynamicListPage.UpdateSearchText(oldSearch, newSearch)`; after changing backing items, call `RaiseItemsChanged()` so the host fetches the new list.
- The official overview says only the Commands provider type is currently supported. Top-level commands come from `TopLevelCommands()`; fallback commands are optional. Context actions are attached to command items and return a `CommandResult` to control dismissal/navigation.

Sources: [create an extension](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension), [extensibility overview](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview), [DynamicListPage.UpdateSearchText](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/microsoft-commandpalette-extensions-toolkit/dynamiclistpage_updatesearchtext), [updating a list of commands](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/update-a-list-of-commands).

## Troubleshooting

- `APPX1685` with `en-AE`: verify the manifest's resource language is explicitly `en-US`; do not disable packaging validation.
- Extension absent after build: use **Build > Deploy**, then reload Command Palette extensions.
- Launching the unpackaged profile does not register the extension package; use `(Package)`.
- Verify the three CLSID occurrences agree between C# and manifest.
- Check Visual Studio Output (Debug), Windows event/debug output, Command Palette state, package registration, and architecture selection.
- Do not commit local game-library exports, private metadata, credentials, local paths, or build output.

## References

- [Create a Command Palette extension](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension)
- [Extensibility overview](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview)
- [PowerToys Command Palette source](https://github.com/microsoft/PowerToys/tree/main/src/modules/cmdpal)
- [MSIX manifest generation](https://learn.microsoft.com/en-us/uwp/schemas/appxpackage/uapmanifestschema/generate-package-manifest)

## Validation status

- [x] Checked-in solution, project, shared props, package versions, manifest, provider, page, extension lifecycle, and package launch profile inspected.
- [x] Deterministic manifest language fix applied on the issue branch.
- [x] Windows restore succeeded after fix (`dotnet restore .\\PlayniteCmdPal.sln`, exit code 0).
- [x] x64 and ARM64 builds succeeded with and without the `DefaultLanguage=en-US` override (all four builds exit code 0).
- [x] Visual Studio package deployment succeeded; Command Palette extension reload and placeholder-page runtime check succeeded.
- [x] Windows version recorded as 10.0.19044 and .NET SDK as 10.0.403.
- [ ] Exact installed Visual Studio version/workloads and PowerToys version recorded.
