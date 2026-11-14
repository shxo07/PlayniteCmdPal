# Testing

This page explains what is tested, where each check runs, and how to run it. For installing the toolchain and deploying the extension, see [Developer Setup](DEVELOPER-SETUP.md).

**WERD-85 unified navigation (2026-11-14):** the provider exposes exactly one Playnite top-level command. Search games, Recently played, and Favorites are keyboard-selectable views inside that page. The installed-game fallback remains a separate host-managed fallback result. Windows CI and CodeQL on [PR #39](https://github.com/werdscopez7347/PlayniteCmdPal/pull/39) are the authoritative automated verification; installed-host keyboard/UI checks remain pending. This environment cannot perform the local Windows build or installed Command Palette smoke test.

**Prior verification context (2026-11-11):** Automated coverage is synthetic and runs without Playnite, PowerToys, a personal game library, or secrets. The WERD-80 Windows CI ([run 42948025706](https://github.com/shxo07/PlayniteCmdPal/actions/runs/42948025706)) and CodeQL ([run 42948025712](https://github.com/shxo07/PlayniteCmdPal/actions/runs/42948025712)) results predate WERD-85 and do not validate this revision. The test commands below are executed by `scripts/ci.ps1`. The previously verified Recently Played list and relative badges are historical context, not a manual pass for the unified page. See [Developer Setup](DEVELOPER-SETUP.md) for the evidence trail and its limits.

## Test projects

| Project | Target | References | Needs Playnite installed? |
|---|---|---|---|
| `PlayniteCmdPal.Core.Tests` | `net10.0` | `PlayniteCmdPal.Core` | No |
| `PlayniteCmdPal.Command.Tests` | `net10.0-windows10.0.26100.0` | Core/read-only adapter, linked production provider/commands/artwork loader, Command Palette SDK | No |
| `PlayniteCmdPal.Playnite.Tests` | `net10.0` | `PlayniteCmdPal.Playnite`, `PlayniteCmdPal.Snapshot` | No |

`PlayniteCmdPal.Core.Tests` and `PlayniteCmdPal.Playnite.Tests` do not reference `PlayniteCmdPal/` (the CmdPal host) or `PlayniteCmdPal.PlaynitePlugin/` (the `net462` companion plugin). `PlayniteCmdPal.Command.Tests` links production `ToggleFavoriteCommand.cs`, `LaunchGameCommand.cs`, `PlayniteCmdPalCommandsProvider.cs`, and `ArtworkIconLoader.cs` into a focused Windows test assembly. The provider's navigation-construction partial is deliberately not linked: tests inject synthetic navigation, catalog, timer, decoder, and process dispatch into the real provider logic instead of starting pages/reading preferences. They exercise actual SDK root arrays, `ItemsChanged`, command lookup, rows/icons, and invocation without loading the WinUI host or starting Playnite/a real game. The companion plugin is compiled; its SDK usage is exercised indirectly through the shared snapshot assembly.

Test data is generated in the test process and written to a temporary directory (`%TEMP%\PlayniteCmdPal.Tests\<guid>`). Each test class that touches disk owns and deletes its directory. No test reads `%LOCALAPPDATA%`, Playnite storage, `playnite.log`, or any real library export.

## Coverage map

### Domain and search (`PlayniteCmdPal.Core.Tests`)

| File | Covers |
|---|---|
| `GameEntryTests` | Required-field validation and parameter names, optional fields defaulting to unknown, record value equality/hashing, `with` expressions preserving identity, display text not being normalized by the model. |
| `FavoritesStoreTests` | Missing-file behavior, pin/unpin by stable ID, persistence after store recreation, versioned JSON, successful-write temporary-file cleanup, malformed JSON recovery (including array, `null`, string, and numeric root values), missing-schema recovery and malformed schema-version recovery (null, string, boolean, object, array, and fractional number), missing-game filtering without deleting saved IDs, future-schema preservation before payload deserialization, and failed-write rollback plus temporary-file cleanup. The malformed-root and malformed-schema-version theories verify both read APIs recover to an empty set and that a subsequent toggle replaces invalid input with a valid version-1 object document. Schema-version validation checks `JsonValueKind.Number` before integer conversion, avoiding `InvalidOperationException` for non-numeric JSON kinds. |
| `LatestRequestGateTests` | Deterministic overlapping-request ordering: a stale request cannot publish after a newer query takes ownership. |
| `RecentlyPlayedGamesTests` | Newest-first ordering by UTC instant, stable tie-breakers, UTC/local/unspecified handling, relative-time formatting across minute/hour/day/week/month/year boundaries and future timestamps, status/recovery item retention during search, and empty-library/all-invalid/no-history states. |
| `GameSearchTests` | Baseline ranking order (exact, prefix, word-prefix, substring), case-insensitivity, duplicate titles, tie-breaks, blank/null query, empty catalog, invalid entries. |
| `GameSearchRankingTests` | Query trimming versus untrimmed stored titles, multi-word and partial queries, word-boundary rules (only non-alphanumeric characters start a word), earliest-match index ordering, ordinal ID ordering for duplicate titles, canonical duplicate-ID selection independent of input order, returned collection immutability, single enumeration of the catalog, argument precedence (blank query short-circuits), and a 5,000-entry determinism check. |

### Command results and provider registration (`PlayniteCmdPal.Command.Tests`)

| File | Covers |
|---|---|
| `ToggleFavoriteCommandTests` | Successful and failed pin/unpin invocations return `CommandResult.KeepOpen()`; the notification is displayed separately, preserving the current page stack and query. |
| `LaunchGameCommandTests` | Exact canonical-ID URI and shell-dispatch options, invalid-ID rejection without dispatch, and actionable process/URI-handler failure feedback. |
| `PlayniteTopLevelCommandsTests` | The actual SDK provider exposes exactly one unified Playnite navigation item at top level and one cached dynamic fallback. It also verifies that `playnite` does not produce a game row, an exact installed title produces a fallback with the saved icon and **Open with Playnite** only, uninstalled-only titles remain hidden, and launch uses the stable ID. |
| `PlayniteLibraryViewsTests` | Search uses the full catalog and metadata filters; Favorites narrows by stable saved IDs and preserves search ranking; Recently Played excludes games without activity, trims title queries, and preserves authoritative newest-first ordering; invalid inputs/modes fail explicitly. |
| `PlayniteGameFallbackTests` | Actual fallback-handler callback and title/action, exact/partial/no-match/installed-title ambiguity, installed/uninstalled duplicate policy, stable-ID revalidation, retained command/query/invocation/disposal races, read/launch failures, async saved-icon property notification, neutral icon behavior, cover exclusion, obsolete icon cancellation, and cross-thread/reentrant property changes. All catalog data, artwork, and dispatch are synthetic. |

### Global direct-launch policy (WERD-86)

| File | Project | Covers |
|---|---|---|
| `TopLevelGameEligibilityTests` | Core | Canonical stable-ID deduplication, invalid/conflicting identity rejection, deterministic ordering, and preservation of distinct equal-title IDs for library browsing. |
| `InstalledGameMatchTests` | Core | Exact unique installed title; case-insensitive trimmed query; exclusion of uninstalled/unknown state; uninstalled duplicates do not block an installed match; multiple installed same-title IDs, invalid/conflicting records, partial and blank queries fail closed. |
| `PlayniteDirectLaunchTests` | Playnite/adapter | Healthy exact installed-title match, forced same-ID revalidation, uninstalled/ambiguous suppression, removal/rename/replacement/uninstall, optional artwork/activity updates, stale/partial/failed snapshot rejection, and cancellation. |

These tests are synthetic, not installed-host integration tests. The [global-search design](CMDPAL-GLOBAL-SEARCH.md) uses the pinned SDK's fallback callback, not provider-level query injection. Record the installed CmdPal version, query `playnite`, fallback-section coexistence with another Results/Apps item, installed/uninstalled/ambiguous title behavior, saved and neutral icon rendering, query/icon lifecycle, and actual Playnite launch. Do not assert adjacency or a particular rank. Existing `scripts/ci.ps1` discovers the tests without a new workflow step.

### Command Palette artwork (WERD-80)

| File | Covers |
|---|---|
| `ArtworkIconLoaderTests` | Saved Playnite icon is the sole UI artwork source (cover artwork is never substituted), missing/invalid icon fallback, missing artwork isolation, successful-result caching, bounded LRU eviction, catalog-refresh invalidation, concurrent requests sharing one decode, rejection of malformed opaque identifiers, retry after transient resolver failure, cancellation of abandoned shared work, and preservation of shared work while another caller still needs it. |
| `ArtworkCacheGenerationTests` | Producer-side Playnite reference/path validation, reparse-point and size checks, supported-signature detection, ICO directory/entry-range validation, injected ICO-to-PNG conversion contract, converted-PNG validation, content-addressed cache publication, cache-capacity handling, cleanup, and exact-stage diagnostics for conversion/import failures. |

Manual verification requires a real Playnite library and the packaged Command Palette extension:

1. Search for games with saved Playnite icons and confirm each result shows the matching icon, including games that are not installed. Confirm cover artwork is never substituted for a missing icon.
2. Check games with no saved icon, a missing cache file, and an unreadable/unsupported image; confirm a consistent fallback appears and search/launch still work. Include an ICO-backed icon and confirm the companion plugin converts it to PNG before publication.
3. Keep a search query open while the catalog refreshes after changing a game's artwork in Playnite. Confirm the new image appears and the previous image is not reused.
4. Confirm typing remains responsive while images load; rapidly change the query and confirm obsolete artwork work is cancelled, artwork from the earlier query never appears in newer results, and the latest query is not delayed by an obsolete queue.
5. Confirm Enter still launches the selected game and source, platform, installed state, duplicate-title disambiguation, and More actions remain intact.
6. Repeat at 100% and 150% (or higher) Windows display scaling. Confirm icons remain correctly sized and do not clip or displace title/subtitle text.

Record the PowerToys version, Windows build, display scaling, and outcome. Do not include real game names, IDs, or library paths in committed evidence.
#### WERD-80 manual verification log (2026-11-11)

- **Verified:** One installed Steam library entry with a saved ICO-backed Playnite icon. The updated plugin logged the resolved Playnite icon path; the v2 snapshot contained the icon digest; the matching converted PNG existed in artwork-v2; and the maintainer confirmed the icon displayed in Command Palette after loading the updated extension/catalog.
- **Diagnostic cross-check:** The digest in the snapshot/cache matched the SHA-256 digest of the PNG produced by an independent local conversion of the same Playnite ICO.
- **Still pending:** Uninstalled-game display; missing/invalid/unreadable/unsupported icon fallback against a real library; changing an icon in Playnite and observing refresh without stale reuse; other supported raster formats; rapid query changes while images load; Favorites lifecycle checks; and 100% / 150% (or higher) display scaling.
- **Evidence hygiene:** Real game IDs, original file paths, and personal library contents are intentionally not committed. The test above proves one real ICO-backed Steam case only; do not generalize it to every library entry or format.

### Unified Playnite page and Favorites UI smoke test (manual)

Run with a local Playnite library and the PowerToys Command Palette extension:

1. Open the single top-level **Playnite** command. Confirm the page shows Search games, Recently played, and Favorites view selectors without returning to the Command Palette home page.
2. In **Search games**, open a game's More actions and choose **Pin to favorites**. Confirm the toast appears, the palette stays on the same page with the same query, and the game remains launchable.
3. Switch to **Favorites** using the in-page selector. Confirm the newly pinned game appears immediately. Unpin it and confirm the toast appears while the unified page remains open and the game disappears immediately; switch back to Search games and pin it again.
4. Search Favorites for a non-matching title and confirm the no-match state; clear the query and confirm the pinned game returns.
5. Use only the keyboard to move between all three view selectors and press Enter. Confirm each view changes in place, the page remains open, and game selection/launch still works.
6. In Search games, verify Installed/source/platform filters still apply immediately; in Recently played, verify newest-first ordering and relative-time badges; in Favorites, verify pin/unpin changes the visible list without leaving the page.
7. Restart the Command Palette extension and Playnite, then confirm the favorite remains.
8. Remove a favorited game from the current Playnite library (or test with a known missing ID) and confirm it is hidden from Favorites without deleting its saved preference.
9. With an unsupported future-schema or unreadable preferences file, confirm normal Search results still appear and can still launch; the Favorites view should report that preferences are unavailable rather than breaking the result list. Confirm the error state leaves the unified page/query open.
10. Rapidly change the Favorites query and pin/unpin several games while artwork is loading. Confirm obsolete requests are cancelled, the page never remains stuck on its loading status, and current favorite artwork appears.
11. Open and close the unified page repeatedly, then pin/unpin a game. Confirm retired page instances do not refresh or retain active work.

Capture screenshots of Search actions, populated Favorites, and empty/no-match states. A short before/after capture of pinning and unpinning without editing the query is useful evidence.

### Snapshot contract (`PlayniteCmdPal.Playnite.Tests`)

| File | Covers |
|---|---|
| `CatalogSnapshotContractTests` | Non-UTC timestamps rejected, UTC converted from another offset accepted, defensive copy of the game list, read-only collections, duplicate/null records, `MaxGames` boundary, canonical-GUID identity rules (trimming must not change identity), name and optional-field normalization, install-state preservation, and the pinned schema constants. |
| `CatalogSnapshotParserTests` (pre-existing) | Envelope validation, timestamp formats, allowlisted fields, unsupported schema, over-limit records, JSON depth, serializer round trip. |
| `CatalogSnapshotParserProtocolTests` | Property order independence, empty input, trailing content, near-miss property names not being allowlisted, Unicode escapes, `null` versus absent optional fields, wrong optional-field types, missing required fields skipping only that record, non-object array items, first-valid-record-wins for duplicate IDs, model-validation failures counted as skipped, unsupported schema reported as `UnsupportedSchema`, non-numeric schema reported as `Malformed`, calendar/timezone-invalid timestamps, the exact 32 MiB and 100,000-record limits, and parsed results surviving mutation of the input buffer. |
| `LastActivitySnapshotTests` | V2 `lastActivity` round-trips for UTC, local, and unspecified `DateTime` kinds; artwork identifiers and `lastActivity` coexist in one v2 record; missing values remain null; malformed dates skip only their record; v1 rejects v2-only fields. |
| `CatalogSnapshotSerializerTests` (pre-existing) | Escaping and round trip, isolated surrogate replacement, empty library, cancellation, write-failure visibility, and the `System.Text.Json`-free reference check for the shared assembly. |
| `CatalogSnapshotSerializerContractTests` | Argument validation before writing, envelope field set and order, UTC wire format, no BOM, per-record field set, deterministic output, destination left open, non-writable destination failure. |

### Publication and refresh

| File | Covers |
|---|---|
| `SnapshotFilePublisherTests` (pre-existing) | Atomic create/replace, canceled publication preserving the previous file, concurrent publications, oversized publication cleanup. |
| `SnapshotFilePublisherContractTests` | Validation before filesystem contact, pre-canceled publication creating nothing, repeated replacement, overwriting a corrupt file, concurrent first-creation races, omitted optional fields, and temporary files never remaining after a failed replacement. |
| `CatalogExportCoordinatorTests` (pre-existing) | Burst coalescing, follow-up scheduling, failure isolation, dispose semantics. |
| `CatalogExportCoordinatorLifecycleTests` | Constructor validation, zero debounce, requests made before the loop starts, a burst inside the debounce window collapsing to one export, requests during an export scheduling exactly one follow-up, exports never overlapping, error reporting without escaping the loop, faulted-task handling, disposal canceling the delegate's token, single-run enforcement, idempotent disposal, and disposing before or while running. |
| `PlayniteCatalogAdapterTests` | Missing file, mapping of artwork and `LastActivity` to `GameEntry`, empty library, staleness boundary, last-known-good retention, v1 fallback/rollback, v2 preference, old-v2/new-v1/new-v2 publication interleaving with bounded three-attempt v2 revalidation at 25 ms intervals, initially missing v2 revalidation, cache interval, coalescing, skipped records. |
| `PlayniteCatalogAdapterPolicyTests` | Default path and policy constants, the adapter never creating or deleting the snapshot, reading while a writer holds the file open, a directory at the snapshot path being reported as unavailable rather than missing, invalid records keeping the library usable, empty/unsupported files, adapter output ranking through `GameSearch`, interval boundary behavior, repeated failures retaining the same last-known-good data, concurrent callers sharing a read, pre-canceled callers, immutability of returned results, and a 2,000-game round trip through the real publisher and adapter. All of it passed on Windows in run [41184115994](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/41184115994). |

## What runs where

| Check | Local | Windows CI | CodeQL workflow | Requires Windows | Requires Playnite / PowerToys |
|---|---|---|---|---|---|
| `dotnet restore` / `dotnet build` (Release) | Yes, via `scripts/ci.ps1` | Yes | Yes | Yes (MSIX/WinUI targets) | No |
| `PlayniteCmdPal.Core.Tests` | Yes | Yes | Yes | No (runs on any OS with the .NET 10 SDK, but the repository's shared script is Windows-only) | No |
| `PlayniteCmdPal.Command.Tests` | Yes (Windows) | Yes | Yes | Yes (Command Palette SDK target) | No |
| `PlayniteCmdPal.Playnite.Tests` | Yes | Yes | Yes | No for the unit/contract tests; the shared script itself is Windows-only | No |
| .NET Framework v1/v2 artwork and `LastActivity` smoke test (`scripts/test-playnite-snapshot-netfx.ps1`) | Yes | Yes | Yes | Yes (Windows PowerShell 5.1 / .NET Framework CLR) | No |
| CodeQL C# analysis | No | n/a | Yes | Yes (workflow runs on `windows-latest`) | No |
| MSIX deploy + Command Palette extension reload | Manual | No | No | Yes | PowerToys |
| Companion plugin packaging (`scripts/package-playnite-plugin.ps1`) | Manual | No | No | Yes | No (publishes and smoke-tests the `.pext`) |
| Real-library verification (`tools/CatalogVerifier` and the [Catalog Bridge checklist](CATALOG-BRIDGE.md#manual-playnite-verification-still-required)) | Manual | No | No | Yes | Playnite (PowerToys only for the launch UI) |
| Launch-by-ID URI smoke test | Manual | No | No | Yes | Playnite + PowerToys, plus one real game |

Run status: the suites described here were authored in a Linux environment with no .NET SDK,
so they were never executed there — only parsed and reviewed. They have since run for real on
GitHub-hosted Windows runners:

- [41184115994](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/41184115994) — Windows CI on `aec765b`: restore, Release build, `66/66` core tests, `183/183` adapter
  tests, and the .NET Framework snapshot smoke step all passed.
- [41184115933](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/41184115933) — CodeQL on the same commit, running the same shared script, passed.
- [41184813275](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/41184813275) — Windows CI on the final head `ad1caa5`, after the documentation updates, passed again.

Three defects in the new tests had to be fixed before the first green run: a ranking theory
that asserted a query-independent result count, a parser test whose hand-built envelope was
missing its closing brace, and a contract test that expected the game ID to be trimmed. Each
was a defect in the test, not in the code under test — the suites are the first execution these
code paths have had, so expect that to keep happening until they have run a few more times.

The authoritative result is still the Windows CI run (and the .NET Framework smoke step)
attached to the pull request that adds or changes a test. A green run elsewhere proves the
code compiled and the assertions held on that runner; it does not prove your local machine is
set up. Do not mark a test as passing locally unless you actually ran it.

The real-library and launch smoke tests are intentionally outside the automated suites: they depend on a particular installed Playnite, a particular PowerToys version, and one user's library. Normal CI must never require them.

`scripts/ci.ps1` is the single source of truth for the automated checks. Windows CI and the CodeQL workflow invoke it instead of repeating commands, so a step cannot drift between a developer machine and CI. When you add a check, add it to that script rather than only to the workflow.

## Running the tests

### Everything CI runs

From the repository root in PowerShell on Windows:

```powershell
.\scripts\ci.ps1
```

It prints the exact `dotnet` command it runs before each step, stops at the first failure, reports the step name and exit code, and returns that exit code.

### One test project while iterating

```powershell
dotnet test .\PlayniteCmdPal.Core.Tests\PlayniteCmdPal.Core.Tests.csproj
dotnet test .\PlayniteCmdPal.Command.Tests\PlayniteCmdPal.Command.Tests.csproj
dotnet test .\PlayniteCmdPal.Playnite.Tests\PlayniteCmdPal.Playnite.Tests.csproj
```

Drop `--configuration Release --no-build --no-restore` (used by the script) when you have not built first, or add `--no-restore` after an initial restore for a faster loop.

### A single class or test name

```powershell
dotnet test .\PlayniteCmdPal.Playnite.Tests\PlayniteCmdPal.Playnite.Tests.csproj --filter "FullyQualifiedName~PlayniteCatalogAdapterPolicyTests"
dotnet test .\PlayniteCmdPal.Core.Tests\PlayniteCmdPal.Core.Tests.csproj --filter "FullyQualifiedName~GameSearchRankingTests"
```

### Verbose output for a failing case

```powershell
dotnet test .\PlayniteCmdPal.Core.Tests\PlayniteCmdPal.Core.Tests.csproj --logger "console;verbosity=detailed" --filter "FullyQualifiedName~QueryIsTrimmedBeforeMatching"
```

### Coverage at a glance

The historical count baseline below predates the later WERD-74/WERD-80/WERD-86 changes; it is **not the current suite total**. At that baseline there were **293 authored cases** (facts plus `[InlineData]` rows): **107** domain/search, **3** Command Palette favorite command results, and **183** snapshot/adapter cases. Of those, 148 were added for
[WERD-62](https://linear.app/werdscopez7347/issue/WERD-62/expand-automated-tests-and-finalize-the-developer-setup-guide).

How that number is established, so it can be re-derived rather than trusted:

| Source | Core | Command | Adapter | Total |
|---|---:|---:|---:|---:|
| Authored `[Fact]` plus `[InlineData]` cases after the schema-version regression cases | 107 | 3 | 183 | 293 |
| Existing Windows CI baseline before command-result tests ([run 41184115994](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/41184115994)) | 66 | — | 183 | 249 |
| Latest verified Windows CI before the schema-version regression cases ([run 42331229094](https://github.com/shxo07/PlayniteCmdPal/actions/runs/42331229094)) | 101 | 3 | 183 | 287 |

The earlier Windows CI baseline reported 249 passing cases (66 Core and 183 adapter). The branch then added 35 Core cases and 3 Command Palette command cases; Windows CI executed all 287 successfully in run [42331229094](https://github.com/shxo07/PlayniteCmdPal/actions/runs/42331229094). The WERD-74 merge adds further cases; re-count authored cases and record executed totals from the merged-head CI logs rather than extrapolating from earlier runs.

Counts drift as tests change. Treat the per-file table above as the authoritative map and re-read each `Passed!` line rather than trusting a number written here. If the written count and `dotnet test` disagree, the `dotnet test` output wins: it is the only measurement that actually executed the tests.

### Expected outcome

A passing run ends with `Passed! - Failed: 0, Passed: <n>, Skipped: 0, Total: <n>` for each project. Treat a skipped test as a gap: the suites are designed to have no conditional skips and no environment-gated tests, so a skip means the test was disabled and needs review.

## Test conventions

- **Synthetic data only.** Use obvious fictional titles and deterministic GUIDs built in-process. Never copy IDs, titles, source names, or paths from a real library, log, or snapshot.
- **No environment dependence.** Do not read `%LOCALAPPDATA%`, `%APPDATA%`, the registry, the clock, or the network. Pass an explicit path and use a fake `TimeProvider` when time matters.
- **Own your temporary files.** Create `%TEMP%\PlayniteCmdPal.Tests\<guid>` in the constructor and delete it in `Dispose`, ignoring `IOException` during cleanup.
- **Deterministic concurrency tests.** Prefer synchronization primitives (`TaskCompletionSource`, `SemaphoreSlim`) over fixed sleeps. When a test must prove *coalescing*, hold the in-flight export open until every extra request has been issued; a short sleep can let the export finish and split the burst into two exports. `TaskCompletionSource` is not `IDisposable`, so do not declare it with `using`.
- **Repeat concurrency tests before trusting them.** `CatalogExportCoordinatorLifecycleTests` is the suite most likely to be flaky, so it was run 10 consecutive times on Windows in a single job (commit `82d5afa`): all iterations passed. Re-run it in a loop after changing it rather than concluding "it passed once" means deterministic.
- **Assert after the loop exits, not after a sleep.** `RunAsync` only completes once the coordinator has stopped, and a stopped coordinator cannot start another export. For "and then nothing else happened" assertions, signal completion from the delegate or the error callback, then `Dispose()` and `await run` before reading the count. In `CatalogExportCoordinatorLifecycleTests` every such assertion now works this way; the only remaining `Task.Delay` is the infinite delay that *is* the cancellation behavior under test, plus the debounce window itself, which is time-based behavior and therefore needs a real interval.
- **Assert one contract per test, with the reason.** When a test pins a rule that a future refactor might be tempted to "fix" (for example, only non-alphanumeric characters start a word match), say so in a comment.
- **Do not assert behavior the code does not promise.** If a case depends on an unverified framework policy (for example, how a particular JSON reader treats a lone surrogate escape), do not encode it as a test. Move it to the "Known gaps" list instead.
- **Keep the suites fast.** Everything runs in seconds; total CI time is dominated by restore and build, not by tests. Avoid tests that would need multi-second timeouts.

## Platform-sensitive assertions

Two assertions depend on operating-system behavior rather than on project code. Both passed on
Windows in run
[41184115994](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/41184115994), so
they are recorded here only so a future failure reads as "check Windows semantics first" rather
than "the adapter regressed":

- `PlayniteCatalogAdapterPolicyTests.DirectoryPathInsteadOfFileIsReportedAsUnavailableRatherThanMissing` opens a **directory** as if it were the snapshot file. Windows reports that as `UnauthorizedAccessException`; the adapter maps both that and an I/O error to `Unavailable`, so the test holds on either platform.
- `SnapshotFilePublisherContractTests.TemporaryFilesNeverRemainAfterAFailedPublication` occupies the destination with a directory so `File.Replace`/`File.Move` fail deterministically, then asserts no temporary files remain.

Everything else asserts project-defined behavior (validation, ranking, parsing, publication,
caching) and should behave identically on any OS with .NET 10.

The lifecycle tests contain two deliberate uses of time. `RequestsInsideTheDebounceWindowCollapseIntoOneExport`
uses a 200 ms debounce because the window *is* the behavior: no export may start until it
elapses, which is what guarantees the burst lands inside one window. `DisposalCancelsTheTokenHandedToTheExport`
awaits `Task.Delay(Timeout.InfiniteTimeSpan, token)` because the only observable proof of
cancellation is an awaiting operation being canceled. Neither is used to "wait and hope" that an
assertion becomes true.

## Operating-system scope

The suites assert project-defined behavior, not operating-system support. Keeping these claims
separate matters when reporting results:

- **Tested environment.** The project is developed and tested on Windows 10 build 19041. That is
  the maintainer's development and test environment, and it is where the local runs behind this
  page come from.
- **Upstream requirement.** Microsoft's Command Palette documentation lists Windows 11 as a
  prerequisite. That is recorded here as an upstream documentation requirement. It is not
  evidence that these tests fail on Windows 10, and it is not evidence that they pass on
  Windows 11.
- **What automation proves.** Windows CI runs on `windows-latest`, a GitHub-hosted server image.
  It proves the code compiles and the assertions hold there. It says nothing about Windows 10,
  Windows 11, or any specific PowerToys version on a desktop.

Do not describe Windows 10 as officially supported by Microsoft for Command Palette extensions,
and do not describe this project as unable to run on Windows 10 — neither claim has evidence
here. Windows 11 compatibility has not been independently validated.

## Known gaps

These are deliberately not automated yet; each needs a Windows host and, in most cases, installed applications.

- **Clean-machine setup (deferred, not passed):** the [Developer Setup](DEVELOPER-SETUP.md) steps have not been replayed on a freshly imaged Windows machine by an independent contributor. Deferred on 2026-10-31: the maintainer develops and tests on Windows 10 build 19041 and no separate clean Windows machine was available. This is an environmental limitation, not a failure, and the step must not be reported as complete until it actually happens.
- The companion plugin's Playnite event wiring (`OnDatabaseOpened`, game/source/platform collection changes, relevance filtering of game updates) is compiled but not unit tested, because `PlayniteApi` is supplied by the Playnite host. Its observable behavior is covered manually through the [Catalog Bridge checklist](CATALOG-BRIDGE.md#manual-playnite-verification-still-required).
- Search-page rendering/ranking and installed CmdPal behavior require manual deploy/reload and the launch-by-ID smoke test. Focused command tests exercise the actual provider/fallback and asynchronous saved-icon loading, but do not render the host UI. The earlier root rows had a maintainer-reported duplicate; per-game root registration is now removed. Desktop verification of fallback-section visibility/coexistence, `playnite` query behavior, installed/uninstalled/ambiguous matching, saved/neutral icon lifecycle, and real-game launch remain pending. Historical fallback visibility and public source evidence are not a desktop pass for this revision.
- No test verifies the real Playnite library contents against the snapshot (by design: that would require personal data).
- Minimum supported versions of Windows, Visual Studio, PowerToys, and Playnite are recorded as "validated once", not as tested minimums. See the contributor environment table in [Developer Setup](DEVELOPER-SETUP.md).


- [42331229094](https://github.com/shxo07/PlayniteCmdPal/actions/runs/42331229094) — Windows CI on the pre-schema-fix WERD-75 head: Release build, `101/101` Core tests, `183/183` adapter tests, `3/3` Command Palette command tests, and the .NET Framework snapshot smoke step all passed.
- [42331229144](https://github.com/shxo07/PlayniteCmdPal/actions/runs/42331229144) — CodeQL on the same pre-schema-fix head passed.
- **WERD-74 current-head regression coverage:** recently-played ordering and relative-time presentation, schema v2 `lastActivity` parsing/serialization, adapter race/revalidation, and the combined .NET Framework smoke path. Execution remains pending on the final-review head; earlier CI totals are not evidence for this revision.

### Recently Played activity refresh

1. Open the single top-level **Playnite** command and switch to **Recently played** using the in-page selector. Note the current newest entry.
2. Launch a game from Playnite, close it, and confirm Playnite records a new **Last activity** value.
3. Leave Recently played selected. Allow up to 30 seconds for the companion plugin's periodic export and adapter revalidation; the unified page polls its cache every 10 seconds while this view is active and should update without a full Command Palette reload.
4. Open the unified page's **More commands** menu (Ctrl+K); use the single **Refresh Playnite catalog** action. Confirm it refreshes the current view. There should be no refresh command attached to individual game or status rows.
5. Confirm game rows show only relative-time badges (`just now`, `N minutes ago`, `N hours ago`, `N days ago`, `N weeks ago`, `N months ago`, or `N years ago`). No absolute date/time, timezone, or hover tooltip should be exposed.
6. Confirm each Recently Played game row displays the fallback icon immediately, then replaces it with the saved Playnite icon when the artwork is available. Games without a valid saved icon must retain the fallback; cover artwork must not be substituted.
