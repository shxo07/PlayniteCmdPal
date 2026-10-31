# Testing

This page explains what is tested, where each check runs, and how to run it. For installing the toolchain and deploying the extension, see [Developer Setup](DEVELOPER-SETUP.md).

**Status (2026-10-31):** Automated coverage is synthetic and is designed to run without Playnite, PowerToys, a personal game library, or secrets. The test commands below are the ones executed by `scripts/ci.ps1`, which Windows CI and CodeQL run on `windows-latest`. The latest verified Windows CI run passed restore, Release build, 66 core tests, 183 adapter tests, and the .NET Framework smoke test ([run 41190710365](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/41190710365)); see the verification evidence section of [Developer Setup](DEVELOPER-SETUP.md) for the evidence trail and its limits.

## Test projects

| Project | Target | References | Needs Playnite installed? |
|---|---|---|---|
| `PlayniteCmdPal.Core.Tests` | `net10.0` | `PlayniteCmdPal.Core` | No |
| `PlayniteCmdPal.Playnite.Tests` | `net10.0` | `PlayniteCmdPal.Playnite`, `PlayniteCmdPal.Snapshot` | No |

Neither test project references `PlayniteCmdPal/` (the CmdPal host) or `PlayniteCmdPal.PlaynitePlugin/` (the `net462` companion plugin). The companion plugin is only compiled; its SDK usage is exercised indirectly by the behavior it delegates to the shared snapshot assembly.

Test data is generated in the test process and written to a temporary directory (`%TEMP%\PlayniteCmdPal.Tests\<guid>`). Each test class that touches disk owns and deletes its directory. No test reads `%LOCALAPPDATA%`, Playnite storage, `playnite.log`, or any real library export.

## Coverage map

### Domain and search (`PlayniteCmdPal.Core.Tests`)

| File | Covers |
|---|---|
| `GameEntryTests` | Required-field validation and parameter names, optional fields defaulting to unknown, record value equality/hashing, `with` expressions preserving identity, display text not being normalized by the model. |
| `GameSearchTests` | Baseline ranking order (exact, prefix, word-prefix, substring), case-insensitivity, duplicate titles, tie-breaks, blank/null query, empty catalog, invalid entries. |
| `GameSearchRankingTests` | Query trimming versus untrimmed stored titles, multi-word and partial queries, word-boundary rules (only non-alphanumeric characters start a word), earliest-match index ordering, ordinal ID ordering for duplicate titles, canonical duplicate-ID selection independent of input order, returned collection immutability, single enumeration of the catalog, argument precedence (blank query short-circuits), and a 5,000-entry determinism check. |

### Snapshot contract (`PlayniteCmdPal.Playnite.Tests`)

| File | Covers |
|---|---|
| `CatalogSnapshotContractTests` | Non-UTC timestamps rejected, UTC converted from another offset accepted, defensive copy of the game list, read-only collections, duplicate/null records, `MaxGames` boundary, canonical-GUID identity rules (trimming must not change identity), name and optional-field normalization, install-state preservation, and the pinned schema constants. |
| `CatalogSnapshotParserTests` (pre-existing) | Envelope validation, timestamp formats, allowlisted fields, unsupported schema, over-limit records, JSON depth, serializer round trip. |
| `CatalogSnapshotParserProtocolTests` | Property order independence, empty input, trailing content, near-miss property names not being allowlisted, Unicode escapes, `null` versus absent optional fields, wrong optional-field types, missing required fields skipping only that record, non-object array items, first-valid-record-wins for duplicate IDs, model-validation failures counted as skipped, any non-1 schema reported as `UnsupportedSchema`, non-numeric schema reported as `Malformed`, calendar/timezone-invalid timestamps, the exact 32 MiB and 100,000-record limits, and parsed results surviving mutation of the input buffer. |
| `CatalogSnapshotSerializerTests` (pre-existing) | Escaping and round trip, isolated surrogate replacement, empty library, cancellation, write-failure visibility, and the `System.Text.Json`-free reference check for the shared assembly. |
| `CatalogSnapshotSerializerContractTests` | Argument validation before writing, envelope field set and order, UTC wire format, no BOM, per-record field set, deterministic output, destination left open, non-writable destination failure. |

### Publication and refresh

| File | Covers |
|---|---|
| `SnapshotFilePublisherTests` (pre-existing) | Atomic create/replace, canceled publication preserving the previous file, concurrent publications, oversized publication cleanup. |
| `SnapshotFilePublisherContractTests` | Validation before filesystem contact, pre-canceled publication creating nothing, repeated replacement, overwriting a corrupt file, concurrent first-creation races, omitted optional fields, and temporary files never remaining after a failed replacement. |
| `CatalogExportCoordinatorTests` (pre-existing) | Burst coalescing, follow-up scheduling, failure isolation, dispose semantics. |
| `CatalogExportCoordinatorLifecycleTests` | Constructor validation, zero debounce, requests made before the loop starts, a burst inside the debounce window collapsing to one export, requests during an export scheduling exactly one follow-up, exports never overlapping, error reporting without escaping the loop, faulted-task handling, disposal canceling the delegate's token, single-run enforcement, idempotent disposal, and disposing before or while running. |
| `PlayniteCatalogAdapterTests` (pre-existing) | Missing file, mapping to `GameEntry`, empty library, staleness boundary, last-known-good retention, reconnect, cache interval, coalescing, skipped records. |
| `PlayniteCatalogAdapterPolicyTests` | Default path and policy constants, the adapter never creating or deleting the snapshot, reading while a writer holds the file open, a directory at the snapshot path being reported as unavailable rather than missing, invalid records keeping the library usable, empty/unsupported files, adapter output ranking through `GameSearch`, interval boundary behavior, repeated failures retaining the same last-known-good data, concurrent callers sharing a read, pre-canceled callers, immutability of returned results, and a 2,000-game round trip through the real publisher and adapter. All of it passed on Windows in run [41184115994](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/41184115994). |

## What runs where

| Check | Local | Windows CI | CodeQL workflow | Requires Windows | Requires Playnite / PowerToys |
|---|---|---|---|---|---|
| `dotnet restore` / `dotnet build` (Release) | Yes, via `scripts/ci.ps1` | Yes | Yes | Yes (MSIX/WinUI targets) | No |
| `PlayniteCmdPal.Core.Tests` | Yes | Yes | Yes | No (runs on any OS with the .NET 10 SDK, but the repository's shared script is Windows-only) | No |
| `PlayniteCmdPal.Playnite.Tests` | Yes | Yes | Yes | No for the unit/contract tests; the shared script itself is Windows-only | No |
| .NET Framework snapshot smoke test (`scripts/test-playnite-snapshot-netfx.ps1`) | Yes | Yes | Yes | Yes (Windows PowerShell 5.1 / .NET Framework CLR) | No |
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

The suites hold **249 test cases** (facts plus `[InlineData]` rows) split across the two
projects: **66** for domain/search and **183** for the snapshot contract, publication, refresh,
and adapter policy. Of those, 148 were added for
[WERD-62](https://linear.app/werdscopez7347/issue/WERD-62/expand-automated-tests-and-finalize-the-developer-setup-guide).

How that number is established, so it can be re-derived rather than trusted:

| Source | Core | Adapter | Total |
|---|---|---|---|
| Count of `[Fact]` plus `[InlineData]` rows in each project in this commit | 66 | 183 | 249 |
| `dotnet test` totals in Windows CI run [41184115994](https://github.com/werdscopez7347/PlayniteCmdPal/actions/runs/41184115994) | 66 | 183 | 249 |
| `dotnet test` totals re-captured on commit `82d5afa` | 66 | 183 | 249 |

All three agree. The count was previously stated here and in an earlier pull-request description
as 251; that figure was produced by an editing-time estimate before any test had executed, not by
`dotnet test`, and it was wrong. It has been corrected to 249.

Counts drift as tests change, so treat the per-file table above as the authoritative map and
re-read the `Passed!` line rather than trusting a number written here. If the written count and
`dotnet test` disagree, the `dotnet test` output wins: it is the only measurement that actually
executed the tests.

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
- The CmdPal presentation layer (search page, ranking display, launch action) has no automated tests; it is exercised by manual deploy/reload and the launch-by-ID smoke test.
- No test verifies the real Playnite library contents against the snapshot (by design: that would require personal data).
- Minimum supported versions of Windows, Visual Studio, PowerToys, and Playnite are recorded as "validated once", not as tested minimums. See the contributor environment table in [Developer Setup](DEVELOPER-SETUP.md).
