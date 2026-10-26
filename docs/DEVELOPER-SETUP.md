# Developer Setup

**Status:** The generated CmdPal template is committed. The clean build and local deploy/debug loop still need to be verified. Do not regenerate the template.

## Repository layout

- `testExtension.sln` — generated solution.
- `testExtension/testExtension.csproj` — generated C# extension project.
- `Directory.Build.props` — shared build settings.
- `Directory.Packages.props` — centrally managed package versions.
- `.github/copilot-instructions.md` and `.github/instructions/cmdpal-extension.instructions.md` — repo-specific implementation guidance.
- `.github/skills/` — focused extension workflows and publishing references.
- `docs/` — PRD, technical design, research, decisions, and developer setup.

## Observed project configuration

These values are read from the checked-in files; they do not imply a successful clean build.

- Target framework: `net10.0-windows10.0.26100.0`.
- Minimum/supported Windows platform: `10.0.19041.0`.
- Windows SDK package version property: `10.0.26100.68-preview`.
- Runtime identifiers: `win-x64;win-arm64`.
- Build platforms: `x64;ARM64`.
- `Microsoft.CommandPalette.Extensions`: `0.9.260303001`.
- `Microsoft.Windows.CsWinRT`: `2.2.0`.
- `Shmuelie.WinRTServer`: `2.1.1`.
- `Microsoft.Windows.SDK.BuildTools.MSIX`: `1.7.20250829.1`.
- Additional central versions are in [Directory.Packages.props](../Directory.Packages.props).

Use a .NET SDK capable of targeting .NET 10 and a current Visual Studio installation with the Windows/WinUI and MSIX components required by the generated template. Record the exact installed SDK, Visual Studio version, workloads, and components after the build/deploy check; do not guess or replace generated package versions.

## First-time setup and validation

1. Install Git, a compatible .NET 10 SDK, Visual Studio with the generated template's Windows/WinUI and MSIX components, and current stable PowerToys with Command Palette enabled.
2. Clone the repository and open `testExtension.sln` in Visual Studio.
3. In a terminal, record `dotnet --info`.
4. Restore and build the checked-in solution:
   ```powershell
   dotnet restore .\testExtension.sln
   dotnet build .\testExtension.sln -c Debug
   ```
5. In Visual Studio, follow the generated project's deployment path. The repository's instructions say to use **Build > Deploy**, not Build alone, because deployment registers the MSIX package.
6. In Command Palette, run **Reload** and choose the command to reload Command Palette extensions.
7. Debug in Visual Studio using the generated launch settings; inspect the Output window and relevant Windows event/debug output if startup fails.
8. Record exact successful commands, SDK/Visual Studio versions, deployment path, and any machine prerequisites in this guide.

## Playnite integration testing

1. Install current stable Playnite and create a small test library with at least two similarly named games.
2. Verify the documented launch-by-ID URI against a real game ID and decide how the extension reads a catalog.
3. Keep Playnite-specific integration tests separate from normal CI. CI must not need a personal library or Playnite installation.
4. Test with Playnite both running and closed only if those behaviors are in the supported scope. Record results rather than assuming multi-install/portable behavior.

## CI and tests

- Windows GitHub Actions CI builds `testExtension.sln`.
- Core search tests should run without PowerToys or Playnite.
- Adapter/protocol tests should use deterministic fixtures and cover missing, malformed, stale, and empty data.
- Real Playnite launch smoke tests are manual/local and should never run against private library data in CI.

## Debugging checklist

- Confirm Command Palette is enabled and running.
- Confirm the generated manifest uses the template's app-extension registration and unique CLSID.
- Use **Build > Deploy**, then reload Command Palette extensions.
- Check the Visual Studio Output window and Windows event/debug output.
- Check whether the failure is in extension activation, catalog loading, or Playnite launch.
- Do not add local game-library exports, machine-specific paths, credentials, or private metadata to Git.

## References

- [Create a Command Palette extension](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension)
- [Command Palette extensibility overview](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview)
- [PowerToys Command Palette source](https://github.com/microsoft/PowerToys/tree/main/src/modules/cmdpal)
- [Playnite CLI/URI arguments](https://api.playnite.link/docs/manual/advanced/cmdlineArguments.html)

## Before first implementation PR

- [ ] Generated solution restores and builds from a clean checkout.
- [ ] Local deploy/reload/debug loop works.
- [ ] Exact SDK, Visual Studio version, and required components are recorded.
- [ ] Core search tests are added.
- [ ] Architecture decisions and research findings are reflected in `docs/DECISIONS.md` and `docs/RESEARCH.md`.
- [ ] No private game data or machine-specific configuration is committed.
