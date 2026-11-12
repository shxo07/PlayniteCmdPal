# WERD-86: direct game commands in global Command Palette results

**Reviewed:** 2026-11-12. **Current design:** individual catalog-backed game commands registered through `TopLevelCommands()`, not a fallback. Installed-host verification of this replacement is still pending.

## Answer and correction

**Yes: the documented top-level-command API can expose individual games directly in the main root/search results.** Returning only the Search games page was a presentation choice, not a platform limitation. The absence of a general per-query extension/ranking API does **not** prevent registering a searchable command per game.

The earlier fallback implementation and its “Include in the Global result” setup recommendation did not meet the requested UX. They are superseded: `FallbackCommands()` now returns an empty array, the fallback implementation is removed, and no fallback-promotion checkbox is needed. [ADR-012](DECISIONS.md#adr-012-catalog-backed-top-level-game-commands) replaces ADR-011.

The provider publishes the existing Search games, Favorites, and Recently played games navigation commands plus one ordinary `CommandItem` per eligible Playnite game:

- **Title:** the saved game title, for example `FIFA 16`.
- **Subtitle/action:** **Open with Playnite**, plus available source/platform/install metadata; equal-title groups also show the full Playnite ID to make every choice distinct.
- **Icon:** the saved per-game Playnite icon, or the neutral game symbol while missing/loading/invalid.
- **Command identity:** `playnitecmdpal.open-game.{canonical-playnite-guid}`.

CmdPal searches and ranks this root command catalog itself, including partial/fuzzy queries according to the host's normal rules. Explicitly selecting a row launches its captured stable ID; the extension never guesses a game from a partial query or substitutes a different ID. Other providers can contribute matching results normally. Registration supplies main/root commands, **not** control over first place, adjacency, or another provider's results.

**Tradeoff:** these are real root commands, not items conditionally injected only when a game query is typed. They can also be exposed in the host's unfiltered/home command catalog. That is the supported registration model selected for this request. The extension does not pretend to receive global query text through `TopLevelCommands()`.

## Use it

1. Deploy the updated extension and reload Command Palette extensions. Keep the companion Playnite catalog bridge enabled.
2. At the Command Palette home/root, type a game title. Select its ordinary game row with **Open with Playnite**; no page entry or fallback setting is required.
3. To force a reread, select the top-level **Search games** command, open **More commands** (`Ctrl+K`), and choose **Refresh game commands**. This stays in the palette and refreshes root registration/artwork; it does not ask Playnite to export or write to its database.
4. Manage missing/incorrect saved game icons in **Playnite**, not the extension. Let the bridge export the updated catalog/artwork. Missing artwork does not prevent launching.

Same-title entries under different IDs remain separate root choices, with disambiguating metadata and full IDs, even when their metadata or GUID prefixes coincide. Selecting a row is an explicit ID-based choice, not a unique-title guess. Stale/offline browsing, filters, richer details, and integration recovery remain available through **Search games → title → explicitly choose the game**.

## Public API evidence and version boundaries

The repository still pins `Microsoft.CommandPalette.Extensions` **0.9.260303001**, including its bundled toolkit. No SDK upgrade, template regeneration, host-internal dependency, manifest change, host-settings edit, UI automation, or new catalog transport is introduced.

- The user-linked [Microsoft Learn guide: Add top-level commands](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/add-top-level-commands-to-your-extension) explicitly documents returning multiple `CommandItem`s, caching them, and replacing the catalog with `RaiseItemsChanged()` when it changes. The [reviewed immutable documentation](https://github.com/MicrosoftDocs/windows-dev-docs/blob/a0de70df089aff14491b12c546c1905eaa2af5d9/hub/powertoys/command-palette/add-top-level-commands-to-your-extension.md) is the same supported pattern used here.
- The pin-era [SDK IDL](https://github.com/microsoft/PowerToys/blob/f6309ac549ae98fdc4925ac290546319affc4ed7/src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions/Microsoft.CommandPalette.Extensions.idl#L380-L419) exposes `ICommandProvider.TopLevelCommands()`, `INotifyItemsChanged`, command lookup, and `ICommandProvider4.GetCommandItem`. The [toolkit `CommandProvider`](https://github.com/microsoft/PowerToys/blob/f6309ac549ae98fdc4925ac290546319affc4ed7/src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions.Toolkit/CommandProvider.cs) exposes `RaiseItemsChanged(int)`.
- The [ICommandProvider reference](https://github.com/MicrosoftDocs/windows-dev-docs/blob/a0de70df089aff14491b12c546c1905eaa2af5d9/hub/powertoys/command-palette/microsoft-commandpalette-extensions/icommandprovider.md) describes `Frozen`; this dynamic provider sets it to **false**.
- Microsoft's [Bookmarks provider in PowerToys v0.102.3101](https://github.com/microsoft/PowerToys/blob/e84973c4f3724eeedd34d836559dac32734c3e22/src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.Bookmark/BookmarksCommandProvider.cs) is a versioned example of individual data-backed top-level commands and rebuilding a cached array before `RaiseItemsChanged`. Host [TopLevelCommandManager](https://github.com/microsoft/PowerToys/blob/e84973c4f3724eeedd34d836559dac32734c3e22/src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/TopLevelCommandManager.cs#L159-L206) reloads a provider's top-level items when notified. These sources are evidence only, not referenced runtime assemblies.

The inspected published host source is **v0.102.3101**, commit `e84973c4f3724eeedd34d836559dac32734c3e22`, not a claim about the maintainer's installed version. The public registration contract establishes the supported main-results path; actual desktop rendering, competing-provider ranking, and real launches still require the checks below.

## Implementation and safety contract

Implementation: [provider](../PlayniteCmdPal/PlayniteCmdPalCommandsProvider.cs), [navigation composition](../PlayniteCmdPal/PlayniteCmdPalCommandsProvider.Navigation.cs), [eligibility policy](../PlayniteCmdPal.Core/DirectLaunchGames.cs), [catalog/revalidation service](../PlayniteCmdPal.Playnite/PlayniteDirectLaunch.cs), and [artwork loader](../PlayniteCmdPal/ArtworkIconLoader.cs).

### Registration and refresh

- `TopLevelCommands()` returns the cached array only: no file reads, image decoding, per-query work, or new command allocation in that getter. Initial catalog loading is asynchronous.
- Poll the adapter every **10 seconds** for the provider's lifetime. Normal reads respect its **30-second** disk revalidation interval. Slow timer reads coalesce; an explicit refresh can supersede an older read.
- Build the full replacement array before publishing it and raising `ItemsChanged` outside the provider lock. Reentrant host reads see the new snapshot. Unchanged ID/title/metadata/icon presentation reuses the array and command objects; unrelated LastActivity/cover changes do not churn the root catalog. Displayed metadata changes do trigger replacement.
- Canonical IDs are mandatory. Identical repeated records count once; conflicting records or invalid identity/display data fail closed. Different IDs with case-insensitively equal titles all register, without choosing by relevance/source/installation. Their subtitles add the full stable ID as well as available source/platform/install metadata; identical metadata or colliding short GUID prefixes cannot make the rows indistinguishable. Eligibility is independent of install state, source, page filters, and catalog input order. Candidate construction scans the catalog once and sorts deterministically; it does not rescan the whole library once per game.
- Only available, non-stale, complete snapshots with no read issue authorize root game commands. The stale threshold is **at least 24 hours**. A failed read withdraws game commands rather than advertising last-known-good launch rows; navigation/recovery pages remain available.
- One root command per eligible stable ID, no fallback registration, no duplicate generic Search games fallback, and no extra game row injected into existing pages. The same game can still be deliberately browsed on those pages; they are separate navigation surfaces, not duplicate global registrations.

### Invocation

- Each command captures its displayed title, stable ID, and published catalog generation. Removed/renamed/replaced entries, changed displayed metadata, and commands retained from an obsolete generation cannot retarget to a different game. Another game acquiring the same title is not an identity ambiguity: an explicitly selected ID can still be verified.
- Immediately before dispatch, explicitly `RefreshAsync` the snapshot, require a healthy catalog and the selected canonical ID with its displayed title/source/platform/install state unchanged, then use the existing `LaunchGameCommand`/`playnite://playnite/start/{id}` shell path. A failed validation forces a versioned root refresh, superseding any older timer read, and returns recovery guidance. Optional icon/cover/LastActivity changes do not invalidate the selected identity.
- Concurrent invocations of one command are guarded against double dispatch. A valid root command is reusable on later, sequential selections; unlike the old fallback it is not consumed after one launch.
- Catalog replacement or disposal during revalidation invalidates dispatch. Disposal stops polling and cancels pending read/image publications.
- Process/URI errors retain the existing actionable failure toast. Accepted shell dispatch is **not proof that the game started**. Rereading a bridge snapshot is not a live Playnite database check or a request for a new plugin export; not-yet-exported changes cannot be observed.

### Saved game icons

- Reuse the trusted opaque `GameEntry.IconReference` resolver and `ArtworkIconLoader`. Never substitute a cover, executable, launcher, storefront, or downloaded image.
- Publish usable root commands with the neutral symbol before image work. Load at most **four** images concurrently; reuse the loader's bounded **96-entry** successful-result cache. Each ordinary `CommandItem` uses the public SDK `Icon` property notification, without reloading the whole root catalog for each image. Icon state is committed atomically under the provider lock, then the synchronous SDK notification is raised outside it so COM listeners can re-enter the getter.
- Missing/corrupt/unreadable images keep the neutral symbol without changing launch eligibility. Unchanged catalog refreshes can retry failed images; explicit refresh invalidates cached resolution. Changed content hashes pick up new saved icons.
- Catalog replacement/disposal cancels obsolete image requests. Generation and token checks prevent late work from changing a newer row. Image decoding never blocks root registration, typing, or launch. Query changes are handled by CmdPal over the registered catalog, not by an extension fallback callback.

## Reproduce on an installed Windows host (not executed here)

Record the actual installed Windows build, PowerToys/CmdPal version, extension package/head, and display scaling. Deploy and reload the updated extension first. Do not commit real game names, GUIDs, paths, library snapshots, or host logs containing private queries.

### 1. Root registration and coexistence

1. Leave fallback inclusion disabled; there should be no Playnite fallback entry to enable. At home, type a unique saved game title. Confirm an ordinary game row with **Open with Playnite** and its saved icon appears in main **Results**, without entering Search games.
2. Check full and partial queries. CmdPal owns which registered commands match and their ranking; neither adjacency nor first place is an assertion. Inspect the unfiltered/home catalog too: root commands may be exposed there by the host.
3. For a harmless host-only control in a disposable local template, return a `CommandItem(new NoOpCommand())` titled `WERD-86 Root Probe` from `TopLevelCommands()`, alongside the normal page command. Do not add a fallback. Verify it is searchable at root. Add/remove it using a cached-array replacement and `RaiseItemsChanged`, and verify the host updates without duplicate rows.
4. For a different-provider control, temporarily add a Start Menu shortcut to a harmless application such as Notepad named `WERD-86 Root Probe`; verify Apps finds it, then verify both ordinary rows coexist. Remove the temporary shortcut/probe afterward. Do not modify another provider or production catalog to force a result order.

### 2. Catalog, icon, and real launch checks

1. Select a unique game's root row. Confirm that exact game actually starts through Playnite, first with Playnite running and then fully closed. Record dispatch errors separately from Playnite/game-start failures.
2. Add, rename, and remove games in Playnite; wait for bridge export and normal revalidation, or use **Search games → More commands → Refresh game commands**. Verify the root catalog updates without requiring fallback settings or accumulating duplicates. Verify Search games/Favorites/Recently Played still behave normally.
3. Introduce duplicate titles under different Playnite IDs, including identical source/platform metadata. Verify one root row per ID, distinguishable metadata/ID subtitles, and that selecting either launches only that exact ID; no arbitrary winner or fallback copy is allowed. In an isolated environment check stale, missing, malformed, and partially invalid snapshots: navigation remains, unsafe root game commands disappear, and retained commands do not launch a replacement ID.
4. Check saved icons for installed and uninstalled entries, no-icon and cover-only entries, missing cache files, corrupt images, and changed icons. Missing artwork must keep the neutral symbol without disabling a valid launch.
5. Refresh/change the catalog while icons or invocation revalidation are pending. Old work must not publish over a newer catalog or dispatch after disposal/reload. Confirm launch does not wait for image decoding and repeated later selections still work.
6. Check typing responsiveness with a large library and icon rendering at 100% and 150% or higher scaling. Synthetic deterministic-list tests are not a host performance benchmark.
7. In an isolated environment with an unusable URI handler, verify actionable failure feedback without a false “game started” claim. Do not damage the user's normal registration for testing.

## Validation record and history

- **Current automated coverage:** `TopLevelGameEligibilityTests`, `PlayniteDirectLaunchTests`, `PlayniteTopLevelCommandsTests`, `LaunchGameCommandTests`, and the existing icon-loader tests. The command tests exercise the actual provider through `ICommandProvider.TopLevelCommands()`, `ICommandProvider4.GetCommandItem`, and `ItemsChanged` using synthetic catalog/nav/timer/decoder/process seams, not a parallel fake provider. They do not instantiate user pages or render the Windows host.
- **Execution evidence:** require green current-head Windows CI and CodeQL in [PR #37's checks](https://github.com/werdscopez7347/PlayniteCmdPal/pull/37/checks). The shared `scripts/ci.ps1` restores, builds Release, runs all three test projects, and runs the .NET Framework snapshot smoke test. Per-commit outcomes are recorded in the PR and WERD-86; older fallback CI runs do not validate this replacement.
- **Environment limit:** the Linux workspace has no `dotnet` executable or installed Windows CmdPal/Playnite host. Local syntax/XML/link/whitespace checks are distinct from Windows CI execution and desktop validation.
- **Historical report only:** on 2026-11-12 the maintainer saw the former prototype under Fallbacks. Its host version and launch outcome were not supplied. That report and the successful pre-replacement [Windows CI](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/43272869507)/[CodeQL](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/43272869760) on `35136e9fbbb6ca8712dbacc7e3ec831f6de9fdba` do not verify the new root commands. The [previous investigation](https://github.com/werdscopez7347/PlayniteCmdPal/blob/35136e9fbbb6ca8712dbacc7e3ec831f6de9fdba/docs/CMDPAL-GLOBAL-SEARCH.md) retains that source history; its fallback recommendation is superseded.
- **Still pending:** installed-host root rendering/coexistence, icon rendering/lifecycle, large-library desktop responsiveness, and a real game launch for this revision. The PR remains draft pending those checks; do not extrapolate public API evidence or synthetic CI into a desktop pass.
