# WERD-86: direct launch from global Command Palette search

**Investigated:** 2026-11-12. **Status:** API/source finding and conservative fallback prototype; installed-host verification pending. This is not a claim of control over global ranking or placement.

## Answer

**There is no general query-provider or result-placement API in the inspected SDK.** PlayniteCmdPal cannot request a ranked result immediately above/below another provider, enumerate its matches, or modify them.

**An ordinary fallback can coexist with other results in the inspected PowerToys v0.102.3101 host.** That host updates enabled fallbacks during normal global searches without first requiring zero matches, and appends nonempty ordinary fallbacks in a separate **Fallbacks** section even when its **Results** section is populated. This is narrower than arbitrary ranked injection and differs from the overview documentation's “when no other results match” description. It is **source-level evidence**, not a desktop observation or a compatibility guarantee for every host version, setting, or aliased query.

Returning a static top-level command per game would also use a supported registration shape, but would prepopulate the home command catalog rather than supply query-dependent matches; it is rejected here for noise/duplication and freshness management.

The prototype therefore registers **one standard SDK fallback**, not one top-level command per game. A unique exact title can display:

- Title: the catalog's game title, e.g. `FIFA 16`.
- Subtitle and command action: **Open with Playnite**.

CmdPal decides if/where this appears. On a host that only offers fallbacks after other matches are exhausted, a competing match may prevent this row from appearing. The dependable alternative is **Search games → type the title → choose the explicit game row → Enter**. The existing page remains the route for partial queries, ambiguous titles, stale/offline browsing, metadata disambiguation, and recovery.

## Evidence and version boundaries

### Repository baseline

At base commit `6affa451c0bf014c936321be3625bc40d1b12053`:

| Item | Observed value |
| --- | --- |
| SDK NuGet package | `Microsoft.CommandPalette.Extensions` **0.9.260303001**, centrally pinned in [Directory.Packages.props](../Directory.Packages.props) |
| Toolkit | `Microsoft.CommandPalette.Extensions.Toolkit` is shipped inside that SDK package, not a separately pinned toolkit dependency |
| Extension target | `net10.0-windows10.0.26100.0`, minimum platform `10.0.19041.0`, x64/ARM64 |
| Provider | Cached `TopLevelCommands()` for Search games, Favorites, Recently played games; no fallback override before this spike |
| Existing launch path | `LaunchGameCommand` → `PlayniteLaunchUri.TryCreate` → `playnite://playnite/start/{canonical-game-guid}` with `UseShellExecute = true` |
| Installed host in this workspace | **None.** This workspace is Linux; no Windows package catalog or CmdPal desktop is available. Previous project deployment notes do not identify an installed PowerToys version. |

No template regeneration, SDK upgrade, manifest change, or new transport is part of this spike.

### Public documentation is not a concurrent-result guarantee

Reviewed Microsoft documentation at revision [`a0de70df089aff14491b12c546c1905eaa2af5d9`](https://github.com/MicrosoftDocs/windows-dev-docs/tree/a0de70df089aff14491b12c546c1905eaa2af5d9/hub/powertoys/command-palette):

1. [Extensibility overview](https://github.com/MicrosoftDocs/windows-dev-docs/blob/a0de70df089aff14491b12c546c1905eaa2af5d9/hub/powertoys/command-palette/extensibility-overview.md) lists top-level commands, fallbacks, and context-menu items, describing fallbacks as triggered when no other results match.
2. [IFallbackHandler.UpdateQuery](https://github.com/MicrosoftDocs/windows-dev-docs/blob/a0de70df089aff14491b12c546c1905eaa2af5d9/hub/powertoys/command-palette/microsoft-commandpalette-extensions/ifallbackhandler_updatequery.md) repeats the no-match condition.
3. [ICommandProvider.FallbackCommands](https://github.com/MicrosoftDocs/windows-dev-docs/blob/a0de70df089aff14491b12c546c1905eaa2af5d9/hub/powertoys/command-palette/microsoft-commandpalette-extensions/icommandprovider_fallbackcommands.md), however, describes query-reactive top-level items shown when the user types. Its return-order wording is not authority over another provider's placement.
4. The [toolkit constructor reference](https://github.com/MicrosoftDocs/windows-dev-docs/blob/a0de70df089aff14491b12c546c1905eaa2af5d9/hub/powertoys/command-palette/microsoft-commandpalette-extensions-toolkit/fallbackcommanditem_constructor.md) still shows the old one-argument constructor. Do **not** copy it over the pinned SDK's actual three-argument constructor.

The checked-in fallback skill has the same simplified no-match explanation. Use it for the pattern, not as evidence that the host suppresses fallbacks whenever another result exists.

### SDK API cross-check

PowerToys commit [`f6309ac549ae98fdc4925ac290546319affc4ed7`](https://github.com/microsoft/PowerToys/commit/f6309ac549ae98fdc4925ac290546319affc4ed7) introduced the upstream dependency pin to **0.9.260303001** on 2026-03-04. The relevant source at that revision provides:

- [`ICommandProvider.FallbackCommands() : IFallbackCommandItem[]`, `IFallbackCommandItem.FallbackHandler`, and `IFallbackHandler.UpdateQuery(string)`](https://github.com/microsoft/PowerToys/blob/f6309ac549ae98fdc4925ac290546319affc4ed7/src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions/Microsoft.CommandPalette.Extensions.idl#L369-L419).
- [`FallbackCommandItem(ICommand command, string displayTitle, string id)`](https://github.com/microsoft/PowerToys/blob/f6309ac549ae98fdc4925ac290546319affc4ed7/src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions.Toolkit/FallbackCommandItem.cs), `IFallbackCommandItem2.Id`, and virtual `UpdateQuery`.
- [`CommandProvider`](https://github.com/microsoft/PowerToys/blob/f6309ac549ae98fdc4925ac290546319affc4ed7/src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions.Toolkit/CommandProvider.cs) and the IDL expose top-level commands, fallback commands, command lookup, interface-cache stubs, and dock bands. They do not expose a per-query ranked-result collection or relative placement controls. `IDynamicListPage` is a page-scoped search API, not a global provider.
- The [SDK package specification](https://github.com/microsoft/PowerToys/blob/e84973c4f3724eeedd34d836559dac32734c3e22/src/modules/cmdpal/extensionsdk/nuget/Microsoft.CommandPalette.Extensions.SDK.nuspec) bundles the toolkit DLL and SDK WinMD in `Microsoft.CommandPalette.Extensions`.

This is a source/API cross-check, not inspection of a locally restored NuGet binary. The prototype deliberately uses only these existing entry points; newer SDK source interfaces are not presumed available merely because the host source contains them.

### Versioned host behavior

The [latest published release returned by GitHub during this investigation](https://github.com/microsoft/PowerToys/releases/tag/v0.102.3101) was **v0.102.3101**, published 2026-11-06, commit **`e84973c4f3724eeedd34d836559dac32734c3e22`**. This is the inspected source version, **not** the user's installed host version. Its [central package file](https://github.com/microsoft/PowerToys/blob/e84973c4f3724eeedd34d836559dac32734c3e22/Directory.Packages.props#L45) uses the same SDK pin.

Relevant call/merge path:

1. [`MainListPage.UpdateSearchTextCore`](https://github.com/microsoft/PowerToys/blob/e84973c4f3724eeedd34d836559dac32734c3e22/src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/Commands/MainListPage.cs#L650-L733) separates special/user-configured global fallbacks from ordinary enabled fallbacks, calls `_fallbackUpdateManager.BeginUpdate(SearchText, ...)`, and includes ordinary fallbacks independently of normal command/app matches. There is no “other result count is zero” gate on that path. Aliases can take a different earlier path; this is not an all-queries promise.
2. [`FallbackUpdateManager`](https://github.com/microsoft/PowerToys/blob/e84973c4f3724eeedd34d836559dac32734c3e22/src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/FallbackUpdateManager.cs#L65-L172) dispatches fallback updates on background workers. [`TopLevelViewModel`](https://github.com/microsoft/PowerToys/blob/e84973c4f3724eeedd34d836559dac32734c3e22/src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/TopLevelViewModel.cs#L417-L465) calls `FallbackHandler.UpdateQuery` and detects title changes.
3. [`GetSearchViewItems`](https://github.com/microsoft/PowerToys/blob/e84973c4f3724eeedd34d836559dac32734c3e22/src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/Commands/MainListPage.cs#L333-L354) drops empty fallback titles. [`MainListPageResultFactory.Create`](https://github.com/microsoft/PowerToys/blob/e84973c4f3724eeedd34d836559dac32734c3e22/src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/Commands/MainListPageResultFactory.cs#L34-L62) allocates space for normal results **and** fallbacks; [its final append](https://github.com/microsoft/PowerToys/blob/e84973c4f3724eeedd34d836559dac32734c3e22/src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/Commands/MainListPageResultFactory.cs#L141-L160) adds the fallback section even when normal results already exist.
4. Microsoft's own [`FallbackOpenFileItem`](https://github.com/microsoft/PowerToys/blob/e84973c4f3724eeedd34d836559dac32734c3e22/src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.Indexer/FallbackOpenFileItem.cs) uses the same public fallback pattern, changing the command/title for a direct result and clearing the title when inapplicable.

Host internals are read here **only as evidence**. The extension neither references those assemblies nor accesses settings/internal APIs to promote itself into the ranked Results section. User/host ranking configuration is not extension-owned placement.

## Prototype and safety contract

Implementation: [ExactGameMatch](../PlayniteCmdPal.Core/ExactGameMatch.cs), [PlayniteDirectLaunch](../PlayniteCmdPal.Playnite/PlayniteDirectLaunch.cs), [PlayniteGameFallback](../PlayniteCmdPal/PlayniteGameFallback.cs), and the provider's single `FallbackCommands()` item.

- **Exact only:** trim outer query whitespace, then ordinal case-insensitive full-title equality. No fuzzy, prefix, punctuation-folding, substring, or “pick first” auto-launch. Use the complete catalog, not page filters or installation-state preferences, when detecting ambiguity.
- **Identity:** require the existing canonical, lowercase, nonempty Playnite GUID policy. Identical repeated core records count once; conflicting records fail closed. Distinct IDs with the same title never yield a direct-launch row.
- **Health:** require an available catalog, no stale flag (the adapter threshold is **at least 24 hours**), no read issue, and **zero skipped records**. A skipped/invalid record could conceal ambiguity. Last-known-good data after a failed refresh stays browsable on the page but cannot authorize direct launch.
- **Invocation:** explicitly `RefreshAsync` the snapshot, re-run the same health/match checks, and require the *same captured ID*. Removal, rename, title ambiguity, replacement by another ID, or failed refresh rejects the action with recovery guidance. Refresh bypasses the normal 30-second cache interval but can coalesce with an in-flight adapter read.
- **Races/lifetime:** query changes cancel/obsolete prior work and retained commands. Each command captures its ID and query generation; it never reads a mutable “current game ID.” Revalidation cannot publish or launch over a newer query, and each selected generation is consumed once. Disposal cancels pending work.
- **Dispatch:** after revalidation, reuse `LaunchGameCommand` and its existing Playnite URI path. The only launch change is an injectable process-dispatch seam for synthetic tests. A Windows dispatch exception preserves the existing actionable toast; a successful dispatch is **not confirmation that the game started**.
- **No duplicates/noise:** one fallback item total; no per-game top-level registration, generic Search games fallback, or extra row inside Search/Favorites/Recently Played. Fallback query handling belongs to the host's main page, while page search remains unchanged. Empty/ambiguous/unhealthy queries hide the item rather than advertising a misleading generic action. `Title` explicitly permits empty without the toolkit substituting `Command.Name`.
- **Freshness limit:** re-reading a snapshot is not a live Playnite database check or a request to the plugin to export. Changes not yet exported cannot be detected; an absent game ID is still resolved by Playnite, never substituted by title. No new IPC or claim of game-start acknowledgement is introduced.

## Reproduce on an installed Windows host (not executed here)

### 1. Minimal host-only coexistence probe

Use a disposable local copy of the extension/template with the **same SDK pin**. Do not publish these probe commands in the production provider. No real library, Playnite install, or game launch is needed.

Add a normal top-level item:

```csharp
new CommandItem(new NoOpCommand())
{
    Title = "WERD-86 Probe",
    Subtitle = "Normal top-level match; no launch",
}
```

Return one `ProbeFallback` from that provider's `FallbackCommands()`:

```csharp
internal sealed partial class ProbeFallback : FallbackCommandItem
{
    private string _title = string.Empty;

    public ProbeFallback()
        : base(new NoOpCommand(), "WERD-86 fallback probe", "werd86.probe") { }

    public override string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public override void UpdateQuery(string query)
    {
        var exact = string.Equals(query.Trim(), "WERD-86 Probe", StringComparison.OrdinalIgnoreCase);
        System.Diagnostics.Debug.WriteLine($"WERD-86: UpdateQuery called; exact={exact}");
        Subtitle = "Fallback match; no launch";
        Title = exact ? "WERD-86 Probe" : string.Empty;
    }
}
```

1. Record Windows build, **actual installed** PowerToys/CmdPal version, extension package version, and fallback enabled/ranking settings. Build > Deploy and reload extensions with the packaged profile.
2. Type `WERD-86 Probe` at home (not inside an extension page). Observe whether `UpdateQuery` runs **while the normal top-level match exists**, and whether both rows are visible. Do not infer invocation solely from row visibility.
3. Remove the normal probe item, reload, and repeat as the no-competing-match control. Clear/change the query; the fallback must disappear.
4. For a **different provider** control, create a temporary Start Menu shortcut to a harmless application such as Notepad named `WERD-86 Probe`. Verify the built-in Apps provider actually finds it, then repeat with the normal probe item removed. Record both invocation and visibility. Remove the temporary shortcut and probe afterward.
5. Save only sanitized outcomes/version information; do not commit host logs with other queries or personal library data. If another installed version suppresses the fallback in the competing-match case, record that limitation rather than trying to override it.

### 2. Playnite integration smoke test

Use the production prototype with a small library managed in Playnite and the companion plugin. Do not hand-edit a real snapshot or run a synthetic GUID as if it were a real game.

1. Search at home for one unique full game title. If the host offers the fallback, verify the exact title and **Open with Playnite** subtitle/action. With an app/shortcut match for the same title, check both rows without asserting adjacency or order.
2. Select the Playnite row; confirm the intended game really starts through Playnite. Record dispatch errors separately from Playnite/game-start errors. Repeat with Playnite fully closed.
3. Use duplicate titles across two Playnite IDs, a partial title, and an unknown title; no direct-launch fallback should appear. Use Search games to disambiguate explicitly.
4. Display a matching row, then remove/rename the game or introduce a duplicate title in Playnite and wait for the bridge export. Invoke the retained row: it must not launch a replacement game. Exercise missing/malformed/stale snapshot recovery only in an isolated test environment.
5. Check query changes during reads, disable/re-enable the fallback, reload/dispose the extension, and verify Search games/Favorites/Recently Played have no extra fallback row.
6. With a missing/unusable URI handler in an isolated environment, verify actionable failure feedback and no false “game started” message. Do not damage the normal user's registration for testing.

## Validation record

- **Completed:** repository inspection; version-pinned official documentation, SDK-source, package-layout, and host-call/merge-path investigation. The documented API-level finding answers coexistence for the inspected source without claiming a desktop test.
- **Added automated coverage:** `ExactGameMatchTests` (exact/ambiguous/no match/deduplication/invalid IDs), `PlayniteDirectLaunchTests` (health, stale/partial/last-known-good rejection, removal/rename/replacement/ambiguity on forced revalidation), `PlayniteGameFallbackTests` (actual row/action, stable-ID dispatch, hidden states, overlapping queries/invocation, disposal, launch failure), and `LaunchGameCommandTests` (URI, invalid ID, process failure). All data is synthetic and all process dispatches are injected.
- **Not executed in this workspace:** .NET restore/build/xUnit, Windows deployment, either installed-host probe, or a real Playnite launch. No `dotnet` executable or Windows host is installed. Run `.\scripts\ci.ps1` on Windows; the existing CI entry point includes all these test projects. Static source checks are not a substitute for that run.
- **Release/issue gate:** record the current branch's Windows build/test result and the installed-host probe before treating runtime coexistence as verified or the spike's desktop checks as complete. Do not extrapolate prior CI or a source tag into a tested installed-host version.
