# PlayniteCmdPal brand assets

Brand identity and production assets for **PlayniteCmdPal**, a Microsoft PowerToys Command Palette extension that searches a Playnite game library and launches games.

Vector source: `svg/`. Rasters, ICO and previews are generated from it by `scripts/generate-assets.py`.

## Design summary
Rounded Windows-style plate in Launch Cobalt. Inside: a command-prompt chevron (also a play marker) and a 2 x 2 library grid with one highlighted cell (the selected result). Below 32 px the grid becomes a single block cursor for legibility. Wordmark: Inter SemiBold, outlined, two-tone.

## Folder map

| Path | Content |
|---|---|
| `svg/` | Authoritative vector logos (symbol, lockups, compact wordmark, monochrome, glyph-only, small-size variant) |
| `icons/png/app-icon-<size>.png` | Plated app icon at 16, 24, 32, 40, 48, 64, 128, 150, 256, 300, 310, 400, 512 px. 16 to 32 px use the small optical variant |
| `icons/png/app-icon-dark-<size>.png` | Dark-background plate variant |
| `icons/png/glyph/` | Transparent, plate-less glyphs (`glyph-cobalt-*`, `glyph-white-*`) |
| `icons/png/msix/` | Windows asset set using standard Windows naming (Square44x44Logo, Square150x150Logo, StoreLogo, Wide310x150Logo, targetsize, unplated) |
| `icons/png/` (named files) | `square-44x44-logo.png`, `square-150x150-logo.png`, `store-logo-50.png`, `wide-tile-310x150.png` |
| `icons/ico/PlayniteCmdPal.ico` | Multi-resolution icon: 16, 24, 32, 48, 64, 128 (BMP/DIB) and 256 (PNG) |
| `icons/ico/PlayniteCmdPal-dark.ico` | Same sizes, dark-background plate |
| `icons/ico/PlayniteCmdPal-small.ico` | 16, 24, 32, 48 only |
| `previews/` | `brand-sheet.png`, `app-icon-preview.png`, `command-palette-mockup.png`, `logo-misuse.png`, `qc-small-sizes.png` |
| `brand/` | `brand-guidelines.md`, `colors.md`, `typography.md` |
| `fonts/` | Inter Regular, Medium, SemiBold (OFL) used by the generator |
| `scripts/generate-assets.py` | Rebuilds every file in this package |
| `LICENSES/` | Third-party licence notes and Inter OFL text |

## SVG usage
- `-light` = for light backgrounds. `-dark` = for dark backgrounds. Unsuffixed files equal the light version.
- All SVGs: explicit `viewBox`, no raster images, no external references, no filters, transparent background. Wordmarks are outlined paths. The monochrome symbol is a single even-odd compound path (no masks); edit its `fill` to recolour.

## Windows / MSIX usage
- This package was produced **without access to the application repository or its `Package.appxmanifest`**. File names in `icons/png/msix/` follow Microsoft's standard asset naming conventions; they have **not** been checked against your manifest or project file. Map them to the manifest `Logo`, `Square44x44Logo`, `Square150x150Logo`, `Wide310x150Logo` and `StoreLogo` entries yourself.
- Square tiles carry the rounded plate with transparent corners. Set the manifest tile `BackgroundColor` to `transparent` to avoid a plate inside a plate.
- `Wide310x150Logo*` is full-bleed Graphite (`#14181F`) with the lockup. Use it only if the manifest declares a wide tile.
- `*_altform-unplated` files are white glyphs, `*_altform-lightunplated` are Cobalt glyphs, both without plate.
- No application source or manifest file was modified.

## Regenerate
```
pip install cairosvg Pillow fonttools uharfbuzz
python scripts/generate-assets.py
```
Output is deterministic apart from PNG compression metadata.

## Licensing
Original artwork. Typeface Inter under SIL OFL 1.1. See `LICENSES/THIRD-PARTY-ASSETS.md`.

## Known limitations
- Rasterisation uses cairosvg (no manual pixel hinting). The small variant compensates with heavier strokes; verify at 100 % scaling on a real display.
- Wordmark is outlined, so text is not editable in the SVG. Edit and rerun the generator to change it.
- Mock-ups use fictional game titles and a drawn approximation of the Command Palette UI, not a screenshot.
- Segoe UI Variable is referenced, not bundled.

## File inventory
- `LICENSES/Inter-OFL-1.1.txt`  (4,380 bytes)
- `LICENSES/THIRD-PARTY-ASSETS.md`  (1,066 bytes)
- `README.md`
- `brand/brand-guidelines.md`  (3,216 bytes)
- `brand/colors.md`  (2,227 bytes)
- `brand/typography.md`  (1,343 bytes)
- `fonts/Inter-Medium.ttf`  (411,500 bytes)
- `fonts/Inter-Regular.ttf`  (407,056 bytes)
- `fonts/Inter-SemiBold.ttf`  (413,976 bytes)
- `icons/ico/PlayniteCmdPal-dark.ico`  (107,593 bytes)
- `icons/ico/PlayniteCmdPal-small.ico`  (17,542 bytes)
- `icons/ico/PlayniteCmdPal.ico`  (107,543 bytes)
- `icons/png/app-icon-128.png`  (2,595 bytes)
- `icons/png/app-icon-150.png`  (3,401 bytes)
- `icons/png/app-icon-16.png`  (462 bytes)
- `icons/png/app-icon-24.png`  (628 bytes)
- `icons/png/app-icon-256.png`  (5,393 bytes)
- `icons/png/app-icon-300.png`  (6,975 bytes)
- `icons/png/app-icon-310.png`  (7,151 bytes)
- `icons/png/app-icon-32.png`  (783 bytes)
- `icons/png/app-icon-40.png`  (1,034 bytes)
- `icons/png/app-icon-400.png`  (9,116 bytes)
- `icons/png/app-icon-48.png`  (1,165 bytes)
- `icons/png/app-icon-512.png`  (11,925 bytes)
- `icons/png/app-icon-64.png`  (1,316 bytes)
- `icons/png/app-icon-dark-128.png`  (2,633 bytes)
- `icons/png/app-icon-dark-16.png`  (467 bytes)
- `icons/png/app-icon-dark-24.png`  (633 bytes)
- `icons/png/app-icon-dark-256.png`  (5,443 bytes)
- `icons/png/app-icon-dark-32.png`  (780 bytes)
- `icons/png/app-icon-dark-48.png`  (1,150 bytes)
- `icons/png/app-icon-dark-512.png`  (11,953 bytes)
- `icons/png/app-icon-dark-64.png`  (1,323 bytes)
- `icons/png/glyph/glyph-cobalt-128.png`  (2,284 bytes)
- `icons/png/glyph/glyph-cobalt-150.png`  (2,355 bytes)
- `icons/png/glyph/glyph-cobalt-16.png`  (402 bytes)
- `icons/png/glyph/glyph-cobalt-24.png`  (487 bytes)
- `icons/png/glyph/glyph-cobalt-256.png`  (4,675 bytes)
- `icons/png/glyph/glyph-cobalt-300.png`  (4,780 bytes)
- `icons/png/glyph/glyph-cobalt-310.png`  (5,474 bytes)
- `icons/png/glyph/glyph-cobalt-32.png`  (631 bytes)
- `icons/png/glyph/glyph-cobalt-40.png`  (786 bytes)
- `icons/png/glyph/glyph-cobalt-400.png`  (6,414 bytes)
- `icons/png/glyph/glyph-cobalt-48.png`  (910 bytes)
- `icons/png/glyph/glyph-cobalt-512.png`  (10,303 bytes)
- `icons/png/glyph/glyph-cobalt-64.png`  (1,225 bytes)
- `icons/png/glyph/glyph-white-128.png`  (1,916 bytes)
- `icons/png/glyph/glyph-white-150.png`  (1,940 bytes)
- `icons/png/glyph/glyph-white-16.png`  (221 bytes)
- `icons/png/glyph/glyph-white-24.png`  (305 bytes)
- `icons/png/glyph/glyph-white-256.png`  (4,024 bytes)
- `icons/png/glyph/glyph-white-300.png`  (4,155 bytes)
- `icons/png/glyph/glyph-white-310.png`  (4,775 bytes)
- `icons/png/glyph/glyph-white-32.png`  (384 bytes)
- `icons/png/glyph/glyph-white-40.png`  (638 bytes)
- `icons/png/glyph/glyph-white-400.png`  (5,620 bytes)
- `icons/png/glyph/glyph-white-48.png`  (816 bytes)
- `icons/png/glyph/glyph-white-512.png`  (8,919 bytes)
- `icons/png/glyph/glyph-white-64.png`  (1,043 bytes)
- `icons/png/msix/Square150x150Logo.scale-100.png`  (3,401 bytes)
- `icons/png/msix/Square150x150Logo.scale-200.png`  (6,975 bytes)
- `icons/png/msix/Square150x150Logo.scale-400.png`  (14,911 bytes)
- `icons/png/msix/Square310x310Logo.scale-100.png`  (7,151 bytes)
- `icons/png/msix/Square44x44Logo.scale-100.png`  (1,116 bytes)
- `icons/png/msix/Square44x44Logo.scale-200.png`  (2,008 bytes)
- `icons/png/msix/Square44x44Logo.scale-400.png`  (3,878 bytes)
- `icons/png/msix/Square44x44Logo.targetsize-16.png`  (462 bytes)
- `icons/png/msix/Square44x44Logo.targetsize-16_altform-lightunplated.png`  (402 bytes)
- `icons/png/msix/Square44x44Logo.targetsize-16_altform-unplated.png`  (221 bytes)
- `icons/png/msix/Square44x44Logo.targetsize-24.png`  (628 bytes)
- `icons/png/msix/Square44x44Logo.targetsize-24_altform-lightunplated.png`  (487 bytes)
- `icons/png/msix/Square44x44Logo.targetsize-24_altform-unplated.png`  (305 bytes)
- `icons/png/msix/Square44x44Logo.targetsize-256.png`  (5,393 bytes)
- `icons/png/msix/Square44x44Logo.targetsize-256_altform-lightunplated.png`  (4,675 bytes)
- `icons/png/msix/Square44x44Logo.targetsize-256_altform-unplated.png`  (4,024 bytes)
- `icons/png/msix/Square44x44Logo.targetsize-32.png`  (783 bytes)
- `icons/png/msix/Square44x44Logo.targetsize-32_altform-lightunplated.png`  (631 bytes)
- `icons/png/msix/Square44x44Logo.targetsize-32_altform-unplated.png`  (384 bytes)
- `icons/png/msix/Square44x44Logo.targetsize-48.png`  (1,165 bytes)
- `icons/png/msix/Square44x44Logo.targetsize-48_altform-lightunplated.png`  (910 bytes)
- `icons/png/msix/Square44x44Logo.targetsize-48_altform-unplated.png`  (816 bytes)
- `icons/png/msix/StoreLogo.scale-100.png`  (1,195 bytes)
- `icons/png/msix/StoreLogo.scale-200.png`  (2,279 bytes)
- `icons/png/msix/StoreLogo.scale-400.png`  (4,508 bytes)
- `icons/png/msix/Wide310x150Logo.scale-100.png`  (11,075 bytes)
- `icons/png/msix/Wide310x150Logo.scale-200.png`  (23,998 bytes)
- `icons/png/msix/Wide310x150Logo.scale-400.png`  (50,513 bytes)
- `icons/png/square-150x150-logo.png`  (3,401 bytes)
- `icons/png/square-44x44-logo.png`  (1,116 bytes)
- `icons/png/store-logo-50.png`  (1,195 bytes)
- `icons/png/wide-tile-310x150.png`  (11,075 bytes)
- `previews/app-icon-preview.png`  (90,393 bytes)
- `previews/brand-sheet.png`  (503,345 bytes)
- `previews/command-palette-mockup.png`  (307,011 bytes)
- `previews/logo-misuse.png`  (187,650 bytes)
- `previews/qc-small-sizes.png`  (93,923 bytes)
- `scripts/generate-assets.py`  (53,616 bytes)
- `svg/logo-glyph-cobalt.svg`  (707 bytes)
- `svg/logo-glyph-white.svg`  (707 bytes)
- `svg/logo-monochrome.svg`  (870 bytes)
- `svg/logo-symbol-dark.svg`  (790 bytes)
- `svg/logo-symbol-light.svg`  (792 bytes)
- `svg/logo-symbol-small-dark.svg`  (587 bytes)
- `svg/logo-symbol-small.svg`  (589 bytes)
- `svg/logo-symbol.svg`  (792 bytes)
- `svg/wordmark-compact-dark.svg`  (5,977 bytes)
- `svg/wordmark-compact-light.svg`  (5,979 bytes)
- `svg/wordmark-compact.svg`  (5,979 bytes)
- `svg/wordmark-horizontal-dark.svg`  (6,680 bytes)
- `svg/wordmark-horizontal-light.svg`  (6,682 bytes)
- `svg/wordmark-horizontal-monochrome.svg`  (6,760 bytes)
- `svg/wordmark-horizontal.svg`  (6,682 bytes)
