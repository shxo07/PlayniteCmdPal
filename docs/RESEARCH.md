# Research: Market and Feasibility

**Last reviewed:** 2026-11-12. Findings below distinguish documented facts from implementation checks that remain.

## Technical findings

- Microsoft documents how to generate, deploy, reload, and develop a Command Palette extension: https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension
- The current repository contains the generated solution `PlayniteCmdPal.sln` and project `PlayniteCmdPal/PlayniteCmdPal.csproj`; build and local deployment are recorded in the developer setup guide.
- Checked-in target framework is `net10.0-windows10.0.26100.0`; minimum supported Windows platform is `10.0.19041.0`; runtime identifiers are `win-x64;win-arm64`.
- The central package file pins `Microsoft.CommandPalette.Extensions` to `0.9.260303001`, `Microsoft.Windows.CsWinRT` to `2.2.0`, and `Shmuelie.WinRTServer` to `2.1.1`. Source: https://github.com/werdscopez7347/PlayniteCmdPal/blob/main/Directory.Packages.props
- Playnite documents `--start <gameId>` and `playnite://playnite/start/<gameId>`, plus `showgame` and `search` URI commands: https://api.playnite.link/docs/manual/advanced/cmdlineArguments.html
- Playnite's plugin SDK exposes `IPlayniteAPI.Database` and `StartGame(Guid)` in plugin context: https://api.playnite.link/docs/api/Playnite.SDK.IPlayniteAPI.html
- The Playnite library tutorial demonstrates enumerating games via `PlayniteApi.Database.Games`: https://api.playnite.link/docs/tutorials/extensions/library.html
- These plugin-context APIs do not by themselves establish a general-purpose external catalog API.
- PowerToys CmdPal source: https://github.com/microsoft/PowerToys/tree/main/src/modules/cmdpal
- Gallery requirements and current distribution options must be checked against the official guide before release: https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extension-gallery

## Current architecture recommendation

Keep Playnite authoritative. Use its documented launch-by-ID URI; real-game smoke tests with Playnite running and fully closed have been reported successful. The selected catalog transport is a companion plugin writing a normalized read-only JSON snapshot; do not use direct database writes, UI automation, or IPC.

The launch mechanism and catalog transport decision are documented. The snapshot writer, reader, adapter cache, and companion plugin are implemented. Windows CI [run 41177945707](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/41177945707) and CodeQL [run 41177945516](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/41177945516) passed; maintainer-reported packaging and real-library verification are recorded in [Catalog Bridge](CATALOG-BRIDGE.md). Field-by-field metadata parity remains to be independently checked.

## Global direct-launch search finding (WERD-86, updated 2026-11-12)

The repository pins `Microsoft.CommandPalette.Extensions` **0.9.260303001**. The supported API includes multiple cached `TopLevelCommands()` items and `IFallbackHandler.UpdateQuery(string)` on fallback items. It does **not** provide the proposed provider-level `UpdateQuery(string, ICommandContext)`, `ICommandContext.AddResult`, or an `ICommandProvider` per-query hook. The newer main-branch `IFallbackQueryHandler.QueryAsync` is not in the pinned package and remains fallback-scoped. See the [versioned SDK and host evidence](CMDPAL-GLOBAL-SEARCH.md#sdk-and-host-evidence).

The maintainer previously selected ordinary per-game root commands, then reported that searching `playnite` flooded Results because the host scores subtitle and extension display name as well as command title. The provider's current design follows the maintainer's 2026-11-12 choice to use fallback results: only navigation pages remain at the root, and one query-reactive fallback can show the game. No game-specific root rows are registered, so `playnite` no longer matches every game's title/subtitle. Fallback results appear in the host-managed **Fallbacks** section by default; an Apps result may remain above them in regular Results. No relative rank is promised.

The fallback only displays one exact, case-insensitive title match among installed games. Uninstalled/unknown-installation records are excluded; a distinct installed duplicate title is ambiguous and yields no row. The row uses the saved game title, subtitle **Open with Playnite**, and the saved icon (neutral fallback while loading/unavailable), without platform/install/source labels. The command force-revalidates the same canonical Playnite ID and installed exact-title match before using the existing URI launch path. Partial, ambiguous, stale, malformed, or incomplete matches fail closed. This is a conservative single result, not a multi-game root list or a ranking provider.

Historical note: the earlier root implementation and duplicate-row startup investigation are preserved in [ADR-012](DECISIONS.md#adr-012-catalog-backed-top-level-game-commands) and the previous commits. The initial root-row timing hypothesis was source-supported but not proven as the user's installed-host cause. The new fallback-only path removes those per-game root rows, so the old root duplicate retest is no longer an acceptance gate; current fallback icon/placement/launch behavior still requires desktop testing. The separate app-looking screenshot row is not modified or suppressed.

## Duplicate-row startup finding (historical; root design superseded)

In the inspected PowerToys v0.102.3101 source, [initial provider registration appends its loaded snapshot](https://github.com/microsoft/PowerToys/blob/e84973c4f3724eeedd34d836559dac32734c3e22/src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/TopLevelCommandManager.cs#L462-L514), while a [provider change separately removes/reinserts that provider's rows](https://github.com/microsoft/PowerToys/blob/e84973c4f3724eeedd34d836559dac32734c3e22/src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/TopLevelCommandManager.cs#L153-L206). That made a startup timing race plausible for the earlier root design, but did not prove the maintainer's installed version/cause. The `f954d9e` fix prepared root commands before first registration and passed its Windows CI/CodeQL. Per-game root rows are now removed by ADR-013, so this source-level theory is historical only.

## `playnite` query and fallback boundaries

The inspected host [search page](https://github.com/microsoft/PowerToys/blob/e84973c4f3724eeedd34d836559dac32734c3e22/src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/Commands/MainListPage.cs) and [ranker](https://github.com/microsoft/PowerToys/blob/e84973c4f3724eeedd34d836559dac32734c3e22/src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/Commands/MainListRanker.cs) score top-level items by title, subtitle, alias, and extension display name. The older per-game root commands therefore matched `playnite` through their **Open with Playnite** subtitle and the extension's **Playnite** display name. The current implementation removes per-game root commands and uses the supported fallback query callback instead; its matcher searches only installed exact game titles. Navigation rows remain the only PlayniteCmdPal root items.

In PowerToys v0.102.3101, [`SafeUpdateFallbackTextSynchronous`](https://github.com/microsoft/PowerToys/blob/e84973c4f3724eeedd34d836559dac32734c3e22/src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/TopLevelViewModel.cs#L417-L465) rejects non-fallback items, then calls `IFallbackHandler.UpdateQuery` on a fallback. This is the query-aware API used here, not a way to inject or reorder arbitrary top-level results. The inspected host source shows fallback results are distinct from normal Results by default; actual installed-host coexistence/rank and whether the result is visible with another provider's match still need a Windows retest.

## Market

The differentiation is a keyboard-first search surface for Playnite's unified catalog and Playnite-configured launch workflow, not another storefront-specific launcher. The CmdPal gallery creates a distribution route but does not prove demand. Game Library Dock is an adjacent Steam-focused extension; its scope is not the same as Playnite's unified library: https://microsoft.github.io/CmdPal-Extensions/

Validate demand through a working demo and community feedback; do not equate gallery size, stars, or downloads with active users.

## Remaining implementation checks

- Record exact installed Visual Studio/PowerToys versions from the successful template validation; the template build/deploy itself was already reported successful.
- Investigate CLI fallback separately if needed; it was not covered by the URI smoke tests.
- Independently compare all exported metadata fields against a real Playnite library without committing private exports.
- Confirm provider refresh behavior from the current generated code and current Microsoft docs.
- Check current packaging/gallery requirements before release.

## Sources

- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extensibility-overview
- https://learn.microsoft.com/en-us/windows/powertoys/command-palette/extension-gallery
- https://github.com/microsoft/PowerToys/tree/main/src/modules/cmdpal
- https://api.playnite.link/docs/manual/advanced/cmdlineArguments.html
- https://api.playnite.link/docs/api/Playnite.SDK.IPlayniteAPI.html
- https://api.playnite.link/docs/api/Playnite.SDK.IGameDatabase.html
- https://api.playnite.link/docs/tutorials/extensions/library.html
- https://api.playnite.link/docs/api/Playnite.SDK.Models.Game.html

## Launch integration decision (2026-10-27)

Selected catalog transport: a companion Playnite plugin exports a normalized JSON snapshot; the out-of-process CmdPal extension reads it read-only. The official references reviewed document in-process database access but do not document a general external-process catalog endpoint. This is a finding about the reviewed documented API, not a claim that no third-party or undocumented interface exists.

The plugin reads PlayniteApi.Database.Games and exports only an allowlisted projection. Canonical path: %LOCALAPPDATA%\PlayniteCmdPal\catalog-v1.json. Use default per-user Local AppData permissions; CmdPal opens the file read-only. Write a complete temporary file in the same directory, flush and close it, then atomically replace the canonical file. Preserve the previous snapshot on write failure. No named pipe, network listener, database parsing, or UI automation.

Schema v1: `schemaVersion` integer 1, `generatedAtUtc` ISO-8601 UTC timestamp, and `games` array. Each record has `id` (Playnite GUID in canonical D format and sole identity key), non-empty `name`, Boolean `isInstalled`, and optional string `source` and `platform`. Do not export full SDK models, executable paths, configured actions, or private library data.

Refresh on plugin initialization and `OnLibraryUpdated`; also subscribe to game collection-change/update events, debounce bursts, serialize writes, and re-enumerate the database on export. Write a complete temporary file in the same directory, flush and close it, then atomically replace the canonical file (or move it into place on first creation); preserve the previous snapshot on write failure. Reject snapshots larger than 32 MiB or containing more than 100,000 game records. Reject malformed top-level JSON, missing/invalid envelope fields, and unsupported schema versions without replacing last-known-good in-memory data. Skip invalid individual records and report them. If the file is missing and no valid snapshot is cached, report integration unavailable, not an empty library; an empty `games` array is valid. If a prior valid snapshot is cached, retain it when a later read is missing or invalid and surface the integration error. Valid snapshots older than 24 hours remain searchable but must be marked stale.

Playnite documents --start <gameId> and playnite://playnite/start/<gameId>. Use the URI as primary launch path, validate the ID as a GUID, and construct playnite://playnite/start/{id:D}. URI dispatch is not proof the game started. References: https://api.playnite.link/docs/manual/advanced/cmdlineArguments.html ; https://api.playnite.link/docs/api/Playnite.SDK.IPlayniteAPI.html ; https://api.playnite.link/docs/tutorials/extensions/library.html ; https://github.com/JosefNemec/Playnite/blob/master/source/Playnite/App/PlayniteApplication.cs

Manual check (user-reported, 2026-10-29): the documented URI launched the intended Steam-installed game on a normal installation of Playnite 10.63 with Playnite already running. The user also reports that, with Playnite fully closed, including its system-tray process, the same URI reopened Playnite and then launched the selected game. CLI fallback was not tested. Steps and scope are in docs/TECHNICAL-DESIGN.md.

## Catalog bridge implementation notes (2026-10-30)

The implementation follows ADR-005 rather than inventing a new transport. Official Playnite 10.63 source was reviewed for the SDK surface used by the companion plugin: `GenericPlugin` lifecycle methods, `IPlayniteAPI.Database.Games`, `IGameDatabase.IsOpen/DatabaseOpened`, `IItemCollection<T>.ItemCollectionChanged/ItemUpdated`, `ItemUpdateEvent<T>.OldData/NewData`, `Game.IsInstalled/SourceId/PlatformIds`, `IMainViewAPI.UIDispatcher`, and extension manifest fields. Source: https://github.com/JosefNemec/Playnite/tree/10.63/source/PlayniteSDK and https://github.com/JosefNemec/Playnite/tree/10.63/source/Playnite.

The shared snapshot project targets `net462` and `net10.0` so the Playnite plugin and adapter can share the same contract without putting Playnite SDK types into core. The Playnite writer uses framework-provided JSON serialization; the strict `System.Text.Json` parser is isolated in the `net10.0` adapter. The adapter uses a 30-second revalidation interval, explicit refresh, coalesced concurrent reads, last-known-good retention, and a 24-hour stale threshold. The plugin debounces exports by 750 ms, projects on Playnite's UI dispatcher, and performs serialization/flush/atomic replacement in the background. These are implementation choices documented in [Catalog Bridge](CATALOG-BRIDGE.md), not claims of runtime verification.

User-reported Windows evidence: the 0.1.0 plugin loaded, but `extensions.log` showed `FileNotFoundException` for `System.Text.Json, Version=9.0.0.0` during publication, despite the DLL being present in its extension directory. Playnite 10.63's [assembly resolver](https://github.com/JosefNemec/Playnite/blob/10.63/source/Playnite/App/PlayniteApplication.cs) handles Magick.NET, not arbitrary extension dependencies. The 0.1.1 export path removes the `System.Text.Json` dependency rather than adding a process-wide resolver. A maintainer later reported that the snapshot exists and the real adapter verifier loaded four records, used its cache, and explicitly refreshed without errors or skipped records. Verification across library changes and recovery remains outstanding.

Validation status: C#/PowerShell syntax and project XML/solution consistency checks were run in the Linux workspace; the .NET build and xUnit tests could not be run there because no .NET SDK is installed. A maintainer reported Windows CI and packaging passed at `398044e`, before the dependency change. The current revision's .NET Framework smoke test and the real-library checklist in [Catalog Bridge](CATALOG-BRIDGE.md) must be completed before claiming the acceptance criterion.

## Recently played view feasibility (2026-11-01)

Playnite's supported SDK exposes `Playnite.SDK.Models.Game.LastActivity` as `DateTime?` and documents it as the last-played date; `Modified` is a separate property for the date of the last game metadata modification. Playnite's own launch completion code sets `LastActivity = DateTime.Now`, confirming the property is updated from actual Playnite launches. The user also confirmed that the Playnite game editor shows a last-played date for a recently played library entry.

The SDK docs do not establish a universal timezone/`DateTime.Kind` contract for values after database persistence. The implementation preserves the round-trip `DateTime` representation and its kind, then compares values as UTC instants (treating `Unspecified` as local machine time). It cannot reconstruct an original timezone offset that Playnite's `DateTime?` value does not carry. To avoid breaking older readers, the plugin keeps `catalog-v1.json` frozen and publishes `catalog-v2.json` with optional `lastActivity`; the current adapter reads both and chooses the newest valid export, supporting old plugins and rollback. Automated tests cover serialization/parsing, missing history, ordering, tie-breaks, and adapter mapping. Real-library ordering still requires a Windows/Playnite smoke test.

References: https://api.playnite.link/docs/api/Playnite.SDK.Models.Game.html ; https://github.com/JosefNemec/Playnite/blob/master/source/Playnite/GamesEditor.cs

## Extension metadata and description display (2026-10-31)

Investigated for WERD-70: Command Palette shows the extension's logo but never its manifest `Description`. Finding: the host does not read that attribute. `Microsoft.CmdPal.UI.ViewModels/Models/ExtensionWrapper.cs` projects `AppExtension.DisplayName` and `Package.DisplayName` into `IExtensionWrapper`, which has no description member, and every secondary line in the UI is computed from the command model — `TopLevelViewModel.Subtitle` from `ICommandItem.Subtitle`, and `ProviderSettingsViewModel.ExtensionSubtext` from `Resources.builtin_extension_subtext` (`"{0}, {1} commands"`). Microsoft's `uap3:AppExtension` schema makes `DisplayName` required and `Description` optional without requiring a host to display it, and `uap3:Properties` is documented as stored but not read by the OS. The behaviour is identical at the `v0.103.3012` tag, so it is not a regression. The declared description is registered correctly and is left unchanged; it remains useful for package inspection and Store/Gallery review. Full write-up: [Command Palette extension metadata](CMDPAL-EXTENSION-METADATA.md).
