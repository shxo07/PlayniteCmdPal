# Research: Market and Feasibility

Last reviewed: 2026-10-25. Initial verified baseline; current-version smoke tests remain required.

## Findings
- Command Palette is an extensible Windows launcher with a .NET/WinRT extension API: https://learn.microsoft.com/en-us/windows/powertoys/command-palette/overview
- PowerToys 0.100 introduced the built-in Extension Gallery. The current utility page surfaced version 0.102.2933.0 with release notes dated 2026-10-21; verify the installed version before development: https://github.com/microsoft/PowerToys/releases/ and https://powertoy.net/utilities/command-palette/
- The official gallery is a curated listing, not a binary host; install sources include WinGet and Microsoft Store: https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extension-gallery
- The public catalog reported 91 extensions across 9 categories, updated 2026-10-12. This is not an active-user estimate: https://microsoft.github.io/CmdPal-Extensions/
- Adjacent competition exists: **Game Library Dock** surfaces a local Steam library and online friends in the CmdPal Dock. Its stated scope is Steam, while our intended value is the Playnite-unified library across supported sources: https://microsoft.github.io/CmdPal-Extensions/
- Official Playnite command-line docs list --start <gameId> and URI commands playnite://playnite/start/<gameId>, showgame, and search: https://api.playnite.link/docs/manual/advanced/cmdlineArguments.html
- Playnite SDK exposes IPlayniteAPI.Database and StartGame(Guid) in the plugin API: https://api.playnite.link/docs/api/Playnite.SDK.IPlayniteAPI.html
- IUriHandlerAPI allows Playnite plugins to register custom URI handlers; this is not a general external catalog API: https://api.playnite.link/docs/api/Playnite.SDK.IUriHandlerAPI.html
- Playnite's plugin docs caution against referencing non-SDK internal assemblies: https://github.com/Playnite/Docs/blob/main/docs/tutorials/extensions/plugins.md
- The historical PlayniteExtensions GitHub repository was archived on 2026-09-21 and points toward Codeberg: https://github.com/JosefNemec/PlayniteExtensions

## Market
Alternatives are Playnite itself, storefront-specific launchers, Windows Start/search, and generic launchers. The differentiator is not launching executables; it is searching the Playnite library and delegating launch to Playnite's configured behavior. Game Library Dock is a real adjacent competitor, but its listed scope is Steam library discovery.

The CmdPal gallery creates a distribution path but listing does not prove demand. Validate through a demo, community feedback, and available install signals. Do not equate GitHub stars or catalog size with active users.

## Architecture recommendation
Use C# and the official CmdPal template/toolkit. For launch, prefer the documented Playnite URI or --start CLI after smoke-testing current stable Playnite. The unresolved technical issue is retrieving a current library safely from an external process. First look for a documented external catalog API; if none exists, evaluate a companion Playnite plugin exposing a read-only catalog through local IPC. Reject UI automation and avoid direct database reads as a production contract.

## Questions to resolve
- Does any supported external catalog API exist?
- Does URI/CLI launch work for installed and portable Playnite, with the current app already running?
- Can a companion plugin expose stable IDs and minimal metadata securely?
- What are current CmdPal result-provider refresh behaviors?
- Can distribution be one package or does it need a Playnite plugin too?
- How much of the user experience is already met by the Steam-only Game Library Dock?

## Sources
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension
- https://github.com/microsoft/CmdPal-Extensions
- https://api.playnite.link/docs/manual/advanced/cmdlineArguments.html
- https://api.playnite.link/docs/api/Playnite.SDK.IPlayniteAPI.html
- https://api.playnite.link/docs/api/Playnite.SDK.IGameDatabaseAPI.html