# WERD-86: installed Playnite game fallback in global search

**Reviewed:** 2026-11-12. **Current choice:** one dynamic fallback for a unique installed exact-title match; ordinary per-game top-level results are removed. The maintainer accepted the host's separate **Fallbacks** section even though another provider's app result may remain higher in regular **Results**. Current installed-host retest is pending.

## Current behavior

The provider keeps only **Search games**, **Favorites**, and **Recently played games** in `TopLevelCommands()`. It returns one `FallbackCommandItem` in `FallbackCommands()`; that item updates for each search query and can display one game result at a time.

A fallback row appears only when the trimmed query exactly matches one installed game's title, ignoring case. It shows:

- **Title:** the saved Playnite game title.
- **Subtitle/action:** **Open with Playnite** only. No platform, source, `PC`, or installation-state labels.
- **Icon:** the saved Playnite game icon, or a neutral symbol while loading/missing/invalid.

Uninstalled and unknown-installation games never appear in this global direct-launch fallback. A same-title uninstalled entry does not block a unique installed match. If two installed game IDs have the same exact title, the fallback is suppressed rather than choosing an arbitrary game. Partial, unknown, blank, unhealthy, stale, incomplete, invalid, or conflicting catalog data produces no row. The existing library pages retain their broader browse/filter behavior.

Fallback results normally appear in the host-managed **Fallbacks** section, separate from regular **Results**. A Windows/Apps result can still appear above them. The extension does not control ordering or modify other providers. We do not opt this fallback into the host's global-results promotion setting. Searching **`playnite`** no longer floods Results with every game: there are no per-game root rows, and the fallback only returns an exact installed title match.

## Matching, launch, and icon safety

- `InstalledGameMatch` scans the catalog once, requires canonical stable IDs, deduplicates identical same-ID records, and fails closed for invalid or conflicting records. Only `IsInstalled == true` is eligible; ambiguity is evaluated among installed titles.
- The fallback captures the selected game ID and query generation. Immediately before dispatch, `PlayniteDirectLaunch` force-refreshes the snapshot and requires the same ID to remain the unique installed exact-title match. It never substitutes a different ID, even if the title is unchanged.
- Launch reuses `LaunchGameCommand` and `playnite://playnite/start/{id}`. Accepted shell dispatch is not proof the game actually started.
- Saved icon decoding uses the existing bounded `ArtworkIconLoader` asynchronously; the host query callback is not blocked on images. The opaque saved icon is the sole artwork source—covers, store logos, and executables are not substitutes. Query changes, launch, and disposal invalidate old icon work. SDK property notifications occur outside the state lock so host callbacks can re-enter safely.
- Catalog snapshots must be available, healthy, non-stale, and complete. Stale or failed data cannot authorize a fallback launch. This is a one-result exact matcher, not a full multi-result root catalog or general ranking provider.

## SDK and host evidence

The repository pins `Microsoft.CommandPalette.Extensions` **0.9.260303001**. It supports ordinary top-level navigation and `IFallbackHandler.UpdateQuery(string)` on fallback items. The proposed provider-level `UpdateQuery(string, ICommandContext)`, `ICommandContext.AddResult`, and arbitrary per-query root injection do not exist on the pinned `ICommandProvider`/`IExtensionHost`. The newer main-branch `IFallbackQueryHandler.QueryAsync` is not in the pinned package and remains fallback-scoped.

- The [pinned SDK IDL](https://github.com/microsoft/PowerToys/blob/f6309ac549ae98fdc4925ac290546319affc4ed7/src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions/Microsoft.CommandPalette.Extensions.idl#L365-L419) places `UpdateQuery` on `IFallbackHandler`, not `ICommandProvider`.
- The [pinned toolkit `FallbackCommandItem`](https://github.com/microsoft/PowerToys/blob/f6309ac549ae98fdc4925ac290546319affc4ed7/src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions.Toolkit/FallbackCommandItem.cs) implements that fallback callback.
- In reviewed PowerToys **v0.102.3101**, [`TopLevelViewModel.SafeUpdateFallbackTextSynchronous`](https://github.com/microsoft/PowerToys/blob/e84973c4f3724eeedd34d836559dac32734c3e22/src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/TopLevelViewModel.cs#L417-L465) returns early for non-fallback items and invokes `IFallbackHandler.UpdateQuery` only for fallback items.
- The versioned host [`MainListPage`](https://github.com/microsoft/PowerToys/blob/e84973c4f3724eeedd34d836559dac32734c3e22/src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/Commands/MainListPage.cs), [`MainListPageResultFactory`](https://github.com/microsoft/PowerToys/blob/e84973c4f3724eeedd34d836559dac32734c3e22/src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/Commands/MainListPageResultFactory.cs), and [`MainListRanker`](https://github.com/microsoft/PowerToys/blob/e84973c4f3724eeedd34d836559dac32734c3e22/src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/Commands/MainListRanker.cs) show normal top-level scoring and the separate default Fallbacks section. The installed user's host version has not been confirmed.
- The earlier query-flooding cause was host scoring against each root item's title, subtitle, alias, and extension display name. Both the old `Open with Playnite` subtitle and extension name `Playnite` matched all root game rows. Removing game root rows resolves that design issue without a subtitle workaround or cross-provider manipulation.

Host-source inspection is evidence about the reviewed revision, not an installed desktop pass. The current fallback's coexistence with the maintainer's other providers, exact UI section, icon rendering, and real launch still need a desktop retest.

## Tests and manual verification

Synthetic tests cover:

- `InstalledGameMatchTests`: unique installed exact match, query trimming/case-insensitivity, uninstalled/unknown exclusion, installed/uninstalled same-title behavior, duplicate installed-title ambiguity, identical/conflicting IDs, invalid records, and no/partial matches.
- `PlayniteDirectLaunchTests`: healthy snapshot match, force-refresh revalidation, removal/rename/replacement/uninstall/ambiguity, optional-artwork updates, stale/partial/failed snapshots, and cancellation.
- `PlayniteGameFallbackTests`: actual SDK callback, title/action display, no-match/ambiguous/installed-only behavior, same-ID launch, retained-command and overlap safety, query/disposal invalidation, async saved icon and neutral fallback, icon cancellation, reentrant property notification, and launch failure. All catalog data, artwork resolution/decoding, time, and launch dispatch are synthetic.
- `PlayniteTopLevelCommandsTests`: provider root contains navigation only; exactly one cached dynamic fallback is registered; `playnite` does not produce a game row; exact installed title shows the saved icon and **Open with Playnite**; uninstalled-only titles remain hidden.

On Windows, deploy the current PR head and run **Reload Command Palette extensions** (restart CmdPal if old registrations persist), then verify:

1. Search **`playnite`**: no flood of games; Playnite navigation remains available. A normal Results/Apps entry may still rank above the Fallbacks section.
2. Search an exact installed title: one game fallback with title, only **Open with Playnite**, and its saved game icon. Confirm the row appears in **Fallbacks** even when another provider has a normal match, if the installed version supports the source-observed coexistence.
3. Search an uninstalled-only title: no fallback row. Check partial/unknown titles, two installed duplicate-title IDs (no row), and one installed plus one uninstalled duplicate (installed one only).
4. Check saved, missing, and corrupt icons; a missing icon keeps the neutral symbol and does not block launch. Change queries during icon loading and confirm old artwork never appears on a newer match.
5. Select the fallback and confirm the intended game really starts through Playnite with Playnite already running and fully closed. Do not treat URI dispatch alone as confirmation.
6. Record installed CmdPal/Windows versions and outcome without committing real game titles, IDs, snapshots, paths, or private host logs.

## Historical root-row follow-up

Before the maintainer selected this fallback UX, the branch registered every eligible game as a top-level command and fixed a possible startup duplicate-registration race. A screenshot had shown duplicate Playnite rows plus a separate app-looking row. The race diagnosis was source-supported but not proven on the installed host. The current fallback-only architecture removes all per-game root rows, so the old duplicate-root retest is superseded; the separate Apps result is intentionally untouched. The history and decision change are recorded in [ADR-012](DECISIONS.md#adr-012-catalog-backed-top-level-game-commands) and [ADR-013](DECISIONS.md#adr-013-installed-game-fallback-result).
