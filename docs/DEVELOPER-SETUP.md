# Developer Setup

Status: Initial checklist. Exact versions/workloads must be filled in after the official template is generated and tested.

## Prerequisites
- Windows 11 development machine.
- Current stable PowerToys with Command Palette enabled.
- Visual Studio with workloads/components required by the current official extension template.
- Git.
- Playnite stable for integration testing.

## Bootstrap
1. Open Command Palette and run its built-in Create extension command.
2. Generate a new C# extension project in a temporary folder.
3. Build and run the untouched generated project before modifying it.
4. Record the generated target framework, Microsoft.CommandPalette.Extensions and Toolkit versions, manifest/packaging setup, and debugging steps.
5. Move/merge the generated template into this repository without overwriting docs or Git metadata.
6. Add the core game model and search-ranking tests before integrating Playnite.
7. Install Playnite and populate a small test library with multiple sources and at least one pair of similarly named games.
8. Run the Playnite integration spike before committing to a transport.

## References
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview
- https://github.com/microsoft/PowerToys/tree/main/src/modules/cmdpal

## Debugging checklist
- Confirm Command Palette is enabled and running.
- Confirm the generated extension manifest uses the correct app extension registration and unique CLSID from the template.
- Check extension process logs and Windows event/debug output.
- Test with Playnite closed and open.
- Do not assume a CLI flag or URI exists until verified in current official documentation.

## Before first PR
- [ ] Record exact tested versions.
- [ ] Build and run the untouched template.
- [ ] Add tests for core search logic.
- [ ] Keep secrets, local game data, binaries, and private paths out of Git.
- [ ] Update this guide with the actual working steps.