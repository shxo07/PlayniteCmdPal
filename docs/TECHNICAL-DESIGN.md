# Technical Design

**Status:** Implementation plan; generated CmdPal template build and local deployment validated on Windows. The running-instance launch URI smoke test passed; catalog integration remains to be implemented and verified.
**Last reviewed:** 2026-10-27.

## Current generated-template baseline

The current repository contains `testExtension.sln` and `testExtension/testExtension.csproj`. Do not regenerate the template.

Observed from checked-in project files:
- Target framework: `net10.0-windows10.0.26100.0`.
- Minimum/supported Windows platform: `10.0.19041.0`.
- Windows SDK package property: `10.0.26100.68-preview`.
- Runtime identifiers: `win-x64;win-arm64`.
- Build platforms: `x64;ARM64`.
- Central package management is enabled.
- `Microsoft.CommandPalette.Extensions` 0.9.260303001.
- `Microsoft.Windows.CsWinRT` 2.2.0.
- `Shmuelie.WinRTServer` 2.1.1.
- `Microsoft.Windows.SDK.BuildTools.MSIX` 1.7.20250829.1.
- Additional versions are pinned in `Directory.Packages.props`.

Sources: [project](../testExtension/testExtension.csproj), [package versions](../Directory.Packages.props), [shared build properties](../Directory.Build.props). These values describe the checked-in template; local restore/build/deployment validation is recorded below.

## Template validation update (2026-10-27)

The generated project was inspected without regeneration. Its current source pins `net10.0-windows10.0.26100.0`, Windows SDK package property `10.0.26100.68-preview`, minimum/supported platform `10.0.19041.0`, and RIDs `win-x64;win-arm64`. Shared build platforms are `x64;ARM64`. Central package versions are recorded in `Directory.Packages.props`; the primary extension packages are `Microsoft.CommandPalette.Extensions` 0.9.260303001, `Microsoft.Windows.CsWinRT` 2.2.0, `Shmuelie.WinRTServer` 2.1.1, and `Microsoft.Windows.SDK.BuildTools.MSIX` 1.7.20250829.1. Maintainer-reported SDK: .NET SDK 10.0.403. The solution metadata reports VS 17.13.35507.96 as generator environment, not as a verified minimum.

The initial build failure was `APPX1685` because `x-generate` selected default language `en-AE`. The source manifest now explicitly declares `<Resource Language="en-US"/>`; this valid language tag avoids dependence on the build machine locale while keeping manifest validation enabled. On the maintainer's Windows 10.0.19044 machine with .NET SDK 10.0.403, restore succeeded and x64/ARM64 builds succeeded both with and without `-p:DefaultLanguage=en-US` (all build commands exit code 0). Visual Studio package deployment succeeded; Command Palette was reloaded, the extension opened, and the placeholder page was confirmed. Exact Visual Studio version/workloads and PowerToys version were not recorded. See [developer setup](DEVELOPER-SETUP.md) for commands and detailed results.

Generated lifecycle inspected: the `IExtension` implementation returns a single `CommandProvider` for `ProviderType.Commands`; its provider returns a cached top-level command; the placeholder page is a static `ListPage` whose `GetItems()` returns one placeholder and which does not implement dynamic search. Microsoft documents `DynamicListPage.UpdateSearchText(oldSearch, newSearch)` for reactive filtering and `RaiseItemsChanged()` to notify the host after data changes. The official guide requires **Build > Deploy**, then **Reload > Reload Command Palette extensions**; the packaged launch profile is required, not the unpackaged profile.

## Stack and framework

- C# extension using the current official Microsoft Command Palette extension template.
- `Microsoft.CommandPalette.Extensions` is referenced directly. Use the exact generated project pattern and consult the current Microsoft guide before implementing providers, pages, result actions, or navigation.
- Keep the generated COM server/hosting, manifest, and MSIX packaging structure unless the official docs show a required change.
- Add a Windows GitHub Actions build workflow in WERD-64 using the actual SDK/framework requirements established by WERD-57.
- Do not guess or casually upgrade template dependencies during MVP implementation.

References:
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extension-gallery
- https://github.com/microsoft/PowerToys/tree/main/src/modules/cmdpal

## Architecture and boundaries

1. **CmdPal presentation:** current-template command provider, search/results, actions, and clear unavailable/error states.
2. **Core:** game model, query normalization, deterministic ranking, and interfaces. No CmdPal or Playnite SDK types.
3. **Catalog adapter:** maps the selected read-only catalog transport into the core model.
4. **Playnite bridge/transport:** a companion Playnite plugin exports an atomic, normalized JSON snapshot for the extension to read. See [ADR-005](DECISIONS.md#adr-005-companion-plugin-json-snapshot-for-catalog-access) for the contract and its limits.

Playnite remains the source of truth for game identity, library metadata, and launch configuration. The extension must not independently recreate storefront launch logic.

## Search and model

- Preserve Playnite's stable game ID; never use the display title as identity.
- Match case-insensitively.
- Deterministic rank order: exact match, prefix match, word-prefix match, substring match; define stable tie-breaks.
- Handle duplicate display names and empty query/catalog explicitly.
- Keep search logic independently unit-testable.

## Playnite launch integration

Playnite documents `--start <gameId>` and `playnite://playnite/start/<gameId>`, as well as `showgame` and `search` URI commands:
https://api.playnite.link/docs/manual/advanced/cmdlineArguments.html

Use the documented URI as the primary launch request; a focused smoke test with a real game ID passed as described below. The purpose of the test is to validate our chosen invocation, not to re-prove that documented features exist. Do not launch the game's executable directly when Playnite should apply its configured launch behavior.

### Manual URI smoke test

1. On a Windows machine with Playnite installed, choose an installed game safe to start and obtain its **Playnite game GUID** (not a store ID) from a trusted Playnite-provided source, such as an existing game shortcut if it exposes the ID. Keep the ID and library details private.
2. With Playnite running, open PowerShell and enter the real game GUID when prompted:
   ```powershell
   $id = [guid](Read-Host 'Playnite game GUID')
   Start-Process "playnite://playnite/start/$($id.ToString('D'))"
   ```
3. Confirm that the intended game actually starts via Playnite. A successful `Start-Process` only shows that Windows accepted the request; it does not establish that the game launched.
4. Record the Playnite version, install mode, whether Playnite was running, and the observed outcome without committing the game ID or personal library data. If it fails, record the error and distinguish URI registration/dispatch from Playnite or game-launch failures.

**Result (user-reported, 2026-10-29):** With a normal installation of Playnite 10.63 already running, the URI test launched the intended Steam-installed game successfully. A closed-Playnite test and CLI fallback are separate follow-ups, not implied by this result.

## Catalog integration decision

Playnite's plugin SDK exposes `PlayniteApi.Database.Games` to an in-process plugin. That does not by itself establish a general-purpose external-process catalog API:
- https://api.playnite.link/docs/api/Playnite.SDK.IPlayniteAPI.html
- https://api.playnite.link/docs/api/Playnite.SDK.IGameDatabase.html
- https://api.playnite.link/docs/tutorials/extensions/library.html

WERD-56 selects a companion plugin that exports an atomic, normalized JSON snapshot. [ADR-005](DECISIONS.md#adr-005-companion-plugin-json-snapshot-for-catalog-access) is the canonical adapter contract: it specifies identity, schema, path, refresh, size limits, stale data, and error handling. The writer and reader are future implementation work; event availability and runtime behavior still need validation against the supported Playnite version. Do not add named pipes or another IPC protocol without evidence that the snapshot design is insufficient.

Rejected for production:
- Direct database writes.
- UI automation.
- Direct database parsing as the supported integration contract.
- Arbitrary shell commands or executable paths derived from untrusted catalog data.

## Proposed logical structure

Keep the generated project intact while the core boundary is introduced. Proposed code organization may be folders/projects in this solution or separate projects only when useful:
- Extension presentation/provider and result actions.
- Core game model and search.
- Catalog adapter and serialization contract.
- Unit/contract tests independent of Playnite.
- Manual local smoke tests for the actual Playnite launch.

The companion bridge is selected, but implementing it is outside the WERD-56 research spike.

## Security and reliability

- Treat catalog data as input; validate stable IDs and expected schema before launch.
- Never accept arbitrary shell commands over IPC.
- No network listener, telemetry, cloud dependency, or private game-library data in Git.
- Never write to Playnite's database.
- Distinguish missing catalog, stale catalog, malformed catalog, no matches, launch requested, and launch failure.
- Keep disk/network/process operations off the CmdPal UI thread.

## Testing and CI

- Core tests: ranking order, case normalization, duplicates, tie-breaks, empty query/catalog.
- Adapter/contract tests: valid, missing, malformed, stale, and empty catalog data.
- Manual integration: real Playnite library and launch-by-ID behavior.
- Normal CI must not require Playnite or personal library data.
- Windows CI builds the generated solution on pull requests and default-branch pushes; add test execution when test projects exist.

## Developer setup

See [DEVELOPER-SETUP.md](DEVELOPER-SETUP.md). Key loop:
1. Record `dotnet --info` and actual Visual Studio version/components.
2. Run `dotnet restore .\testExtension.sln` and `dotnet build .\testExtension.sln -c Debug`.
3. In Visual Studio use **Build > Deploy** to register the generated MSIX package.
4. Reload Command Palette extensions and debug using the generated launch settings.
5. Record the successful exact steps in WERD-57.

## Open decisions

- Validate the selected companion-plugin lifecycle and refresh events during implementation.
- Tested minimum PowerToys/Visual Studio/.NET SDK versions.
- Optional alias/fuzzy matching beyond title matching.
- Packaging and gallery submission route.
- Repository license.
