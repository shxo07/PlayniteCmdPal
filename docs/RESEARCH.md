# Research: Market and Feasibility

Last reviewed: 2026-10-25. This is a verified initial baseline, not final proof of every integration detail.

## Findings
- Command Palette is an extensible Windows launcher with a .NET/WinRT extension API: https://learn.microsoft.com/en-us/windows/powertoys/command-palette/overview
- PowerToys 0.100 introduced the built-in Extension Gallery. The current utility page surfaced version 0.102.2933.0 with release notes dated 2026-10-21; verify the installed version before development: https://github.com/microsoft/PowerToys/releases/ and https://powertoy.net/utilities/command-palette/
- The official gallery is a curated listing, not a binary host; supported install sources include WinGet and Microsoft Store: https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extension-gallery
- Public gallery catalog reported 91 extensions across 9 categories, updated 2026-10-12. This is not an active-user estimate: https://microsoft.github.io/CmdPal-Extensions/
- Playnite SDK has game database APIs and IPlayniteAPI.StartGame(Guid). This is available in Playnite's plugin context; external access is not proven by that fact alone: https://api.playnite.link/docs/api/Playnite.SDK.IPlayniteAPI.html
- Playnite docs warn against referencing non-SDK internal assemblies from plugins: https://github.com/Playnite/Docs/blob/main/docs/tutorials/extensions/plugins.md
- The historical PlayniteExtensions GitHub repository was archived on 2026-09-21 and points toward Codeberg: https://github.com/JosefNemec/PlayniteExtensions

## Market
Alternatives are Playnite itself, storefront-specific launchers, Windows Start/search, and general launchers. The differentiator is not launching executables; it is searching the Playnite library and delegating launch to Playnite's configuration.

The CmdPal gallery creates a distribution path but listing does not prove demand. Validate through a demo, feedback, and available install signals. Do not equate GitHub stars or catalog size with active users.

## Architecture recommendation
Use C# and the official CmdPal template/toolkit. Put Playnite behind a project-owned adapter. First investigate a supported external API/URI/CLI. If none supports both catalog lookup and launch, evaluate a companion Playnite plugin with local IPC. Reject UI automation and avoid direct database reads as a production contract.

## Questions to resolve
- Supported external catalog and launch contract?
- Can plugin bridge use stable game IDs and launch through Playnite?
- Named pipe security/lifecycle requirements?
- CmdPal result-provider refresh behavior in the current release?
- One-package vs two-component installation?
- Any existing equivalent extension in the current gallery?

## Source list
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension
- https://github.com/microsoft/CmdPal-Extensions
- https://api.playnite.link/docs/api/index.html
- https://api.playnite.link/docs/api/Playnite.SDK.IGameDatabaseAPI.html