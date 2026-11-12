# Testing

This page explains what is tested, where each check runs, and how to run it. For installing the toolchain and deploying the extension, see [Developer Setup](DEVELOPER-SETUP.md).

**WERD-86 automated status (2026-11-12):** [Windows CI 43265177473](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/43265177473) and [CodeQL 43265177609](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/43265177609) passed on prototype commit `fd34e36240cfbe99dc66eec6585478a77bbb6bf1`. Both workflows execute `scripts/ci.ps1`, including restore, Release build, all three test projects, and the .NET Framework snapshot smoke test. This validates the pinned-SDK prototype and synthetic safety tests, not installed-host coexistence or a real-game launch. Those desktop checks remain pending in [the investigation](CMDPAL-GLOBAL-SEARCH.md#reproduce-on-an-installed-windows-host-not-executed-here). No local .NET run was possible in the Linux workspace.

**Prior verification context (2026-11-11):** Automated coverage is synthetic and runs without Playnite, PowerToys, a personal game library, or secrets. The latest previously verified Windows CI and CodeQL runs are the WERD-80 runs ([CI run 42948025706](https://github.com/shxo07/PlayniteCmdPal/actions/runs/42948025706), [CodeQL run 42948025712](https://github.com/shxo07/PlayniteCmdPal/actions/runs/42948025712)); they predate the current WERD-74 merge and do not validate it. The current WERD-74 head must pass a fresh full Windows CI run (restore, Release build, Core, Playnite/adapter, Command Palette tests, and the .NET Framework snapshot smoke test) and CodeQL before merge. The test commands below are executed by `scripts/ci.ps1`. The user previously confirmed the populated Recently Played list and relative badges in 0.0.7.0; the 0.0.8.0 tooltip-removal build still needs installation verification. See the verification evidence section of [Developer Setup](DEVELOPER-SETUP.md) for the evidence trail and its limits.

## Test projects

| Project | Target | References | Needs Playnite installed? |
|---|---|---|---|
| `PlayniteCmdPal.Core.Tests` | `net10.0` | `PlayniteCmdPal.Core` | No |
| `PlayniteCmdPal.Command.Tests` | `net10.0-windows10.0.26100.0` | Core/read-only adapter, linked production commands/fallback/artwork loader, Command Palette SDK | No |
| `PlayniteCmdPal.Playnite.Tests` | `net10.0` | `PlayniteCmdPal.Playnite`, `PlayniteCmdPal.Snapshot` | No |

`PlayniteCmdPal.Core.Tests` and `PlayniteCmdPal.Playnite.Tests` do not reference `PlayniteCmdPal/` (the CmdPal host) or `PlayniteCmdPal.PlaynitePlugin/` (the `net462` companion plugin). `PlayniteCmdPal.Command.Tests` links the production `ToggleFavoriteCommand.cs`, `LaunchGameCommand.cs`, `PlayniteGameFallback.cs`, and `ArtworkIconLoader.cs` into a focused Windows test assembly. It asserts real SDK command/result/row values without loading the full WinUI host. Launch dispatch is injected; tests never start Playnite or a real game. The companion plugin is only compiled; its SDK usage is exercised indirectly by the behavior it delegates to the shared snapshot assembly.

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

### Command results and global fallback (`PlayniteCmdPal.Command.Tests`)

| File | Covers |
|---|---|
| `ToggleFavoriteCommandTests` | Successful and failed pin/unpin invocations return `CommandResult.KeepOpen()`; the notification is displayed separately, preserving the current page stack and query. |
| `LaunchGameCommandTests` | Exact canonical-ID URI and shell-dispatch options, invalid-ID rejection without dispatch, and actionable process/URI-handler failure feedback. |
| `PlayniteGameFallbackTests` | Matched title and **Open with Playnite** subtitle/action, existing stable-ID launch path, hidden weak/no/ambiguous matches, retained-command invalidation, older-query publication and invocation races, stale/missing/replaced/ambiguous catalog rejection, consumed generations, disposal, read failure, and preservation of the existing launch-failure toast. |

### Global direct-launch policy (WERD-86)

| File | Project | Covers |
|---|---|---|
| `ExactGameMatchTests` | Core | Case-insensitive exact match and query trimming, rejection of partial/fuzzy/unknown titles, duplicate-title ambiguity independent of order/install/source, identical-ID deduplication, conflicting records, invalid IDs/records, empty catalog, and preservation of stored titles. |
| `PlayniteDirectLaunchTests` | Playnite/adapter | Healthy matches, explicit refresh inside the cache interval, same-ID revalidation, removal/rename/replacement/new ambiguity, stale and partial catalog rejection, last-known-good suppression after missing/malformed refresh, and no-match/unavailable behavior. |

These tests are synthetic, not installed-host integration tests. The [global-search investigation](CMDPAL-GLOBAL-SEARCH.md) includes a minimal two-row host probe and a competing Apps-provider control. Record the **installed** CmdPal version, whether `UpdateQuery` runs with another match, and whether the row is displayed. Neither adjacency nor a particular rank is an assertion. Windows deployment/host probes and a real-game launch remain pending; the Linux workspace has no `dotnet` executable. The existing `scripts/ci.ps1` includes all new tests without a new workflow step.

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

### Favorites UI smoke test (manual)

Run with a local Playnite library and the PowerToys Command Palette extension:

1. Open **Search games**, open a game's More actions, and choose **Pin to favorites**. Confirm the toast appears, **Command Palette stays visible on the same Search Games page with the same query**, and the game is still launchable.
2. Open **Favorites** without changing its query. Confirm the newly pinned game appears immediately.
3. Unpin the game from **Favorites**. Confirm the toast appears while **Favorites remains visible with its existing query**, and the game disappears immediately; pin it again from **Search games** and confirm it reappears without closing the palette.
4. Search Favorites for a non-matching title and confirm the no-match state; clear the query and confirm the pinned game returns.
5. Restart the Command Palette extension and Playnite, then confirm the favorite remains.
6. Remove a favorited game from the current Playnite library (or test with a known missing ID) and confirm it is hidden from Favorites without deleting its saved preference.
7. With an unsupported future-schema or unreadable preferences file, confirm normal Search results still appear and can still launch; the Favorites action should report that preferences are unavailable rather than breaking the result list. Confirm its error toast also leaves the current page/query open.
8. Rapidly change the Favorites query and pin/unpin several games while artwork is loading. Confirm obsolete Favorites requests are cancelled, the page never remains stuck on its loading status, and current favorite artwork appears.
9. Open and close Favorites repeatedly, then pin/unpin a game. Confirm retired Favorites pages do not refresh or retain active work.

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

The suites currently contain **293 authored test cases** (facts plus `[InlineData]` rows) split across three
test projects: **107** for domain/search, **3** for Command Palette favorite command results,
and **183** for the snapshot contract, publication, refresh, and adapter policy. Of those, 148 were added for
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
- Search-page rendering/ranking and installed CmdPal behavior still require manual deploy/reload and the launch-by-ID smoke test. Focused command tests now cover launch dispatch and fallback row construction/state, but do not render the host UI. WERD-86's competing-provider invocation/visibility probe is explicitly pending; versioned source inspection is not an installed-host pass.
- No test verifies the real Playnite library contents against the snapshot (by design: that would require personal data).
- Minimum supported versions of Windows, Visual Studio, PowerToys, and Playnite are recorded as "validated once", not as tested minimums. See the contributor environment table in [Developer Setup](DEVELOPER-SETUP.md).


- [42331229094](https://github.com/shxo07/PlayniteCmdPal/actions/runs/42331229094) — Windows CI on the pre-schema-fix WERD-75 head: Release build, `101/101` Core tests, `183/183` adapter tests, `3/3` Command Palette command tests, and the .NET Framework snapshot smoke step all passed.
- [42331229144](https://github.com/shxo07/PlayniteCmdPal/actions/runs/42331229144) — CodeQL on the same pre-schema-fix head passed.
- **WERD-74 current-head regression coverage:** recently-played ordering and relative-time presentation, schema v2 `lastActivity` parsing/serialization, adapter race/revalidation, and the combined .NET Framework smoke path. Execution remains pending on the final-review head; earlier CI totals are not evidence for this revision.

### Recently Played activity refresh

1. Open PlayniteCmdPal's **Recently played games** page and note the current newest entry.
2. Launch a game from Playnite, close it, and confirm Playnite records a new **Last activity** value.
3. Leave Recently Played open. Allow up to 30 seconds for the companion plugin's periodic export and adapter revalidation; the page polls its cache every 10 seconds and should update without a full Command Palette reload.
4. Select the top-level **Recently played games** command and open its **More commands** menu (Ctrl+K); use **Refresh Playnite catalog**. Confirm it refreshes the page. There should be no refresh command attached to individual game or status rows.
5. Confirm game rows show only relative-time badges (`just now`, `N minutes ago`, `N hours ago`, `N days ago`, `N weeks ago`, `N months ago`, or `N years ago`). No absolute date/time, timezone, or hover tooltip should be exposed.
6. Confirm each Recently Played game row displays the fallback icon immediately, then replaces it with the saved Playnite icon when the artwork is available. Games without a valid saved icon must retain the fallback; cover artwork must not be substituted.
