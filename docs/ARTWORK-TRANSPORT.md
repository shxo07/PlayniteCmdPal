# Artwork transport and trust boundary

Status: Proposed for WERD-71; implementation must match this contract.

## Decision

Use a bridge-owned, local artwork cache populated only from Playnite database file references. Do not export Playnite's original image reference, absolute path, URL, SDK object, or arbitrary filesystem path.

The companion plugin resolves a game image reference through Playnite's documented database API, copies a validated image into the bridge cache, and places only a content-derived opaque identifier in the catalog snapshot. The Command Palette adapter resolves that identifier against the one fixed cache directory owned by the current user.

This is deliberately narrower than Playnite's Game.CoverImage, Game.Icon, and Game.BackgroundImage input surface. In particular, arbitrary local paths and remote URLs are not accepted as cache inputs. Background images are not part of this issue.

## Playnite SDK evidence and input policy

Playnite documents Game.CoverImage and Game.Icon as strings that may represent a local path, HTTP URL, or database file ID. Its library tutorial documents resolving database-managed image references with IGameDatabase.GetFullFilePath. The bridge accepts only a non-empty canonical GUID reference and resolves it through that database method. It does not pass a raw game field to File.Open, File.Copy, or a URI loader.

A missing, unresolvable, malformed, unsupported, or inaccessible reference produces a null artwork identifier. Artwork is optional; it must never make a valid game record or catalog export fail.

## Cache layout and ownership

- Root: %LOCALAPPDATA%\PlayniteCmdPal\artwork-v2\
- The plugin is the only writer. The Command Palette adapter is read-only.
- Each asset filename is the lowercase SHA-256 digest of its validated bytes plus the extension inferred from the file signature. The digest is the opaque identifier; it is not a path.
- The reader accepts only exactly 64 lowercase hexadecimal characters as an identifier and constructs the candidate path beneath the fixed cache root. It verifies the normalized candidate remains beneath that root.
- No cache root, filename, source path, or original Playnite reference is serialized into the catalog.
- Temporary files are unique, created in the cache directory, and atomically moved into place. Existing content-addressed files are reused.

## Validation and resource limits

- Accepted formats: PNG, JPEG, GIF, and WebP, based on file signatures rather than filename extensions.
- Maximum source file size: 5 MiB. Empty, larger, truncated, or unrecognized files are skipped.
- Maximum cache size: 256 MiB. If importing another image would exceed the limit, that image is omitted; catalog export continues.
- Only regular files are accepted; reparse-point source files are rejected.
- Copying reads no more than the configured per-file limit plus one byte, so a changing or misleading file length cannot bypass the limit.
- The snapshot parser validates opaque IDs independently of the filesystem. Traversal strings, rooted paths, separators, alternate data stream syntax, and malformed identifiers are rejected before path construction.

The cache is not an image decoder. The transport validates container signatures and byte limits; the UI remains responsible for safe image decoding and should handle decode failures as missing artwork.

## Refresh, invalidation, and cleanup

Every catalog export re-enumerates Playnite's current library. The content hash naturally changes when image bytes change and remains stable when bytes do not. The new snapshot references the new content identifier. Only after the new catalog snapshot has been atomically published does the plugin prune cache files not referenced by that snapshot.

If an export fails or is canceled, the previous catalog remains in place and cleanup is not run. This preserves all assets referenced by the last successfully published snapshot. A successful export with missing artwork removes stale unreferenced cache entries. Temporary files are cleaned on every import attempt and stale temporary files are eligible for cleanup on plugin startup.

The adapter never deletes cache files. It resolves identifiers only beneath the fixed cache root and treats absent files as missing artwork, not as a catalog failure.

## Snapshot schema migration

Schema v1 remains immutable: its envelope and game allowlist remain exactly as defined in ADR-005. The parser continues to accept valid v1 snapshots, mapping artwork fields to null.

Schema v2 adds two optional per-game strings: coverArtworkId and iconArtworkId. Each is either null/absent or a valid lowercase SHA-256 identifier. No other v1 or v2 fields are accepted. The v2 writer emits schemaVersion: 2; the parser explicitly dispatches v1 and v2 rather than interpreting v1 as v2. Unknown schema versions return the existing clear unsupported-schema error.

The plugin publishes the new schema to catalog-v2.json. The adapter checks the v2 location first and falls back to the existing catalog-v1.json only when the v2 file is absent. A malformed or unsupported v2 file is not silently hidden by falling back to stale v1 data. A valid v1 file remains readable during rollout.

## Failure behavior

| Condition | Result |
| --- | --- |
| No cover/icon reference | Null identifier; game remains in catalog |
| Non-GUID reference, raw path, or URL | Rejected; no filesystem access |
| Database reference cannot be resolved | Null identifier |
| Missing, unreadable, empty, oversized, truncated, or unsupported image | Null identifier; warning/count only |
| Cache limit reached | Null identifier; export continues |
| Malformed snapshot artwork identifier | Invalid game record is skipped and counted |
| Traversal or rooted identifier | Rejected before path resolution |
| Cache file deleted after publication | Adapter reports missing artwork; catalog remains available |
| Export fails before publication | Previous snapshot and its referenced cache assets remain intact |
| Valid v1 snapshot | Read normally with no artwork |

## Required contract tests

- Valid PNG/JPEG/GIF/WebP imports produce deterministic opaque identifiers and files inside the cache root.
- Same bytes produce the same identifier; changed bytes produce a different identifier.
- Missing, malformed, URL, raw-path, and non-GUID references are rejected without opening the supplied path.
- Empty, oversized, truncated, and unknown-signature files are rejected.
- Cache byte limits are enforced while reading and before publication.
- Identifier resolution rejects traversal, rooted paths, separators, malformed digests, and symlink/reparse escapes.
- Cleanup preserves every asset referenced by the currently published snapshot and removes orphaned assets only after successful publication.
- v1 fixtures remain readable with null artwork; v2 valid and invalid artwork fields are covered.
- A malformed or unsupported v2 file does not trigger silent v1 fallback.
- Missing artwork never makes a valid catalog unavailable.

## Non-goals

No third-party artwork downloads, storefront scraping, cloud hosting, network listener, IPC, Playnite database writes, arbitrary file access, or Command Palette artwork UI changes.
