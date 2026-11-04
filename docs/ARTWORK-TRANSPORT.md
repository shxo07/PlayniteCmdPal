# Artwork transport and trust boundary

Status: Implementation in review. Automated checks must be rerun against the final PR head; live Playnite verification remains pending.

## Decision

Use a bridge-owned, local artwork cache populated only from Playnite database file references. Do not export Playnite's original image reference, absolute path, URL, SDK object, or arbitrary filesystem path.

The companion plugin resolves a game image reference through Playnite's documented database API, copies a validated image into the bridge cache, and places only a content-derived opaque identifier in the catalog snapshot. The Command Palette adapter resolves that identifier against the active cache directory, or the bridge-generated rollback directory during the brief snapshot/cache publication window.

This is deliberately narrower than Playnite's Game.CoverImage, Game.Icon, and Game.BackgroundImage input surface. In particular, arbitrary local paths and remote URLs are not accepted as cache inputs. Background images are not part of this issue.

## Playnite SDK evidence and input policy

Playnite documents Game.CoverImage and Game.Icon as strings that may represent a local path, HTTP URL, or database file ID. Its library tutorial documents resolving database-managed image references with IGameDatabase.GetFullFilePath. Playnite's stored database reference is a game-owned relative path in the form `<game-guid>\\<file-guid>.<extension>` (for example, the game GUID directory and a generated GUID filename). The bridge accepts only exactly two canonical GUID path segments, requires the first segment to equal the owning game's ID, and permits only PNG/JPEG/GIF/WebP filename extensions. It validates this complete relative reference before calling `IGameDatabase.GetFullFilePath`; it then verifies that the resolved path is exactly the expected file beneath `<DatabasePath>\\files\\<game-guid>`. Raw paths, URLs, rooted paths, traversal segments, mismatched game directories, and mixed/extra path segments are rejected before the SDK resolver is called. The source file, game directory, and database files directory must not be reparse points.

A missing, unresolvable, malformed, unsupported, or inaccessible reference produces a null artwork identifier. Artwork is optional; it must never make a valid game record or catalog export fail.

## Supported SDK evidence

The bridge targets .NET Framework 4.6.2 and pins `PlayniteSDK` **6.18.0** in `Directory.Packages.props`. The package is the SDK version used for the plugin compile contract; do not infer a different SDK version from the installed Playnite application version.

Evidence for the API surface used by the producer:
- [PlayniteSDK 6.18.0 package](https://www.nuget.org/packages/PlayniteSDK/6.18.0) — exact package version and net462 target.
- [IGameDatabaseAPI reference](https://api.playnite.link/docs/api/Playnite.SDK.IGameDatabaseAPI.html) — documents `DatabasePath` and `GetFullFilePath(string)`.
- [Playnite library extension tutorial: Handling Files](https://api.playnite.link/docs/tutorials/extensions/library.html) — documents that game-related images are database-managed and demonstrates resolving `Game.CoverImage` with `GetFullFilePath`.
- The producer compile contract is pinned to the immutable `PlayniteSDK` 6.18.0 NuGet package above. Its version-specific reference assembly is the compile-time API evidence; the moving GitHub `master` source is intentionally not used as release evidence.

The public API documentation does **not** formally guarantee the exact serialized database-reference grammar. The accepted `<game-guid>\\<file-guid>.<extension>` shape is therefore a deliberately narrow, fail-closed producer policy validated against the SDK-backed database layout; it is not presented as a general promise that all possible SDK reference forms are accepted. If a supported Playnite release changes that layout, the bridge omits artwork until the policy and tests are updated. The plugin must continue to use the documented SDK resolver rather than parsing Playnite's private database files.

## Cache layout and ownership

- Root: %LOCALAPPDATA%\PlayniteCmdPal\artwork-v2\
- The plugin is the only writer. The Command Palette adapter is read-only. The exporter must enforce single-writer ownership across plugin instances/processes before recovery or publication.
- Each asset filename is the lowercase SHA-256 digest of its validated bytes plus the extension inferred from the file signature. The snapshot contains only the 64-character digest; the adapter resolves it by trying the fixed allowlisted extensions beneath the fixed cache root.
- The reader accepts only exactly 64 lowercase hexadecimal characters as an identifier and constructs the candidate path beneath the fixed cache root. It verifies the normalized candidate remains beneath that root.
- No cache root, filename, source path, or original Playnite reference is serialized into the catalog.
- Temporary files are unique, created in the cache directory, and atomically moved into place. Existing content-addressed files are reused.

## Validation and resource limits

- Accepted formats: PNG, JPEG, GIF, and WebP, based on file signatures rather than filename extensions.
- Maximum source file size: 5 MiB. Empty, larger, or unrecognized-signature files are skipped. Signature checks are transport validation only; they do not prove that a complete image can be decoded.
- Maximum active cache generation size: 256 MiB. If importing another image would exceed the limit, that image is omitted; catalog export continues. During atomic replacement, the previous generation is temporarily retained for rollback, so transient disk use can approach two generations; the backup is removed immediately after commit or recovery.
- Only regular files are accepted; reparse-point source files are rejected.
- Copying reads no more than the configured per-file limit plus one byte, so a changing or misleading file length cannot bypass the limit.
- The snapshot parser validates opaque IDs independently of the filesystem. Traversal strings, rooted paths, separators, alternate data stream syntax, and malformed identifiers are rejected before path construction.

The cache is not an image decoder. The transport validates container signatures and byte limits only. WERD-80 owns decoding, unsupported-codec handling, and fallback to another candidate image.

## Refresh, invalidation, and cleanup

Every catalog export re-enumerates Playnite's current library and builds a complete staging cache generation. In addition, a 30-minute producer timer requests revalidation so changed bytes behind an unchanged reference are eventually detected. The content hash naturally changes when image bytes change and remains stable when bytes do not. The producer must also re-read/hash each currently referenced image during each export, even when the Playnite reference string is unchanged; a producer-side scheduled/explicit revalidation export is required because the database reference alone is not a content-change signal. The new snapshot references the new content identifier. The plugin activates the new generation while retaining the previous cache as a rollback directory, then atomically publishes the v2 snapshot. The transaction marker records the snapshot timestamp token as well as the backup state. On startup, recovery checks the current v2 envelope for that exact timestamp to distinguish a crash before publication from a crash immediately after the atomic replace but before the commit marker was updated. It restores the old cache for an unpublished transaction, or keeps the new cache when the matching snapshot was published. After publication it removes the backup; if publication fails it restores the previous cache. This replaces the complete cache generation rather than accumulating old files.

If an export fails or is canceled, the previous catalog remains in place and cleanup is not run. The accepted threat model excludes a malicious process running as the same Windows user; pathname/reparse-point checks are defense-in-depth and do not claim race-proof containment against a same-user process that can replace path components between validation and open. The bridge opens source files itself and copies bytes immediately, but Windows path-based APIs do not provide a portable handle-relative containment guarantee here. This preserves all assets referenced by the last successfully published snapshot. A successful export with missing artwork removes stale unreferenced cache entries. Temporary files are cleaned on every import attempt and stale temporary files are eligible for cleanup on plugin startup. Cache deletion failures must remain observable and must prevent transaction bookkeeping from being discarded; an unresolved backup/staging directory must be accounted for and block another generation rather than accumulate without bound.

The adapter never deletes cache files. It resolves identifiers only beneath the fixed cache root or a matching bridge-generated backup directory, rejects reparse-point directories/files, and rechecks file size, signature, extension, and SHA-256 before returning a local path. The returned path is a short-lived locator, not a durable lease: after a successful generation replacement, a path held by an older reader may disappear. Consumers must treat a missing/unreadable path as missing artwork and re-resolve against the latest snapshot; bounded reader leases are not provided.

## Concrete v2 snapshot example

The producer writes `%LOCALAPPDATA%\PlayniteCmdPal\catalog-v2.json`. Artwork identifiers are optional lowercase SHA-256 digests; no source paths or Playnite references appear in the file.

```json
{
  "schemaVersion": 2,
  "generatedAtUtc": "2026-11-04T05:34:25.6650676Z",
  "games": [
    {
      "id": "11111111-1111-4111-8111-111111111111",
      "name": "Example",
      "isInstalled": true,
      "source": "Steam",
      "platform": "PC",
      "coverArtworkId": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
      "iconArtworkId": null
    }
  ]
}
```

## Snapshot schema migration

Schema v1 remains immutable: its envelope and game allowlist remain exactly as defined in ADR-005. The parser continues to accept valid v1 snapshots, mapping artwork fields to null.

Schema v2 adds two optional per-game strings: coverArtworkId and iconArtworkId. Each is either null/absent or a valid lowercase SHA-256 identifier. No other v1 or v2 fields are accepted. The v2 writer emits schemaVersion: 2; the parser explicitly dispatches v1 and v2 rather than interpreting v1 as v2. Unknown schema versions return the existing clear unsupported-schema error.

The plugin publishes the new schema to catalog-v2.json. The adapter checks the v2 location first and falls back to the existing catalog-v1.json only when the v2 file is absent. A malformed or unsupported v2 file is not silently hidden by falling back to stale v1 data. A valid v1 file remains readable during rollout. Downgrade policy: an older producer that writes only v1 must explicitly remove/retire the v2 snapshot it previously owned before v1 fallback is expected; the reader will not infer a downgrade from timestamps or silently ignore a surviving v2 file. If the v2 file remains, it remains authoritative until removed by the owner/admin or a later v2 producer replaces it. If artwork references alone make the v2 snapshot exceed the 32 MiB snapshot limit, the plugin retries publication once with the same game data and no artwork references; if that still exceeds the limit, publication fails and the previous snapshot/cache remain available.

## Producer/consumer compatibility and rollout

| Producer | Consumer | Expected behavior |
| --- | --- | --- |
| v1 producer | v1-capable consumer | Reads the v1 snapshot; no artwork transport |
| v2 producer | v2-capable consumer | Reads v2 and resolves opaque artwork IDs |
| v2 producer | old v1-only consumer | Reads only a retained v1 file, if one exists; it cannot see v2 artwork or v2-only catalog updates |
| v1 producer after v2 | v2-capable consumer | The v2 snapshot remains authoritative while it exists; the older producer must explicitly retire it before v1 fallback is used |

**Upgrade order:** deploy the v2-capable consumer first, while it can still read v1; then install/enable the v2 producer. Keep the v1 file during the compatibility window. Do not assume an older consumer understands the v2 filename or artwork identifiers.

## Failure behavior

| Condition | Result |
| --- | --- |
| No cover/icon reference | Null identifier; game remains in catalog |
| Non-GUID reference, raw path, or URL | Rejected; no filesystem access |
| Database reference cannot be resolved | Null identifier |
| Missing, unreadable, empty, oversized, or signature-invalid/unsupported image | Null identifier; export continues |
| Cache limit reached | Null identifier; export continues |
| Malformed snapshot artwork identifier | Only that optional artwork field is nulled; valid game metadata and the other artwork field are retained |
| Traversal or rooted identifier | Rejected before path resolution |
| Cache file deleted after publication | Adapter reports missing artwork; catalog remains available |
| Export fails before publication | Previous snapshot and its referenced cache assets remain intact |
| Valid v1 snapshot | Read normally with no artwork |

## Required contract tests

- Valid JPEG imports produce deterministic opaque identifiers and files inside the cache root; signature tests cover the supported PNG/JPEG/GIF/WebP container markers.
- Same bytes produce the same identifier; changed bytes produce a different identifier.
- Missing, malformed, URL, raw-path, and non-GUID references are rejected without opening the supplied path.
- Empty, oversized, and unknown-signature files are rejected. A signature-valid but undecodable image is a consumer-side decode failure, not a transport guarantee.
- Cache byte limits are enforced while reading and before publication.
- Identifier resolution rejects traversal, rooted paths, separators, malformed digests, and reparse-point files or cache roots.
- Cleanup preserves every asset referenced by the currently published snapshot and removes orphaned assets only after successful publication.
- v1 fixtures remain readable with null artwork; v2 valid and invalid artwork fields are covered, including explicit v1 fallback only when v2 is missing.
- Real Playnite reference grammar is covered: game GUID plus file GUID/extension; raw paths, URLs, traversal, mismatched game IDs, and unsupported extensions are rejected before SDK resolution.
- Supported image signatures and signature-invalid/unknown containers are covered; cache path resolution accepts only digest identifiers and fixed extensions. The transport does not guarantee detection of every structurally truncated image when its outer signature markers remain valid; decoder-level validation belongs to WERD-80.
- A malformed or unsupported v2 file does not trigger silent v1 fallback.
- Missing artwork never makes a valid catalog unavailable.

## Non-goals

No third-party artwork downloads, storefront scraping, cloud hosting, network listener, IPC, Playnite database writes, arbitrary file access, or Command Palette artwork UI changes. WERD-80 owns UI rendering, asynchronous/cancellable loading, bounded decoded-image caching, image fallback, and DPI verification.
