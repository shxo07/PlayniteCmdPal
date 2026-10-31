# PlayniteCmdPal brand guidelines

## 1. Concept
PlayniteCmdPal finds a game and launches it from the keyboard. The symbol joins two ideas in one rounded Windows-style plate:

1. **A command prompt chevron** (`>`): invoking a command. It also reads as a play marker, which is the only gaming reference.
2. **A 2 x 2 library grid**: the result list. One cell is brighter than the others: the selected result, ready to launch.

At 16 to 32 px the grid collapses to a single solid block, a text cursor beside the chevron (`>|`). The shape language is one stroke weight, one corner radius family and one colour.

No controller, magnifying glass, sparkle, gradient or mascot is used.

## 2. Construction
Authoritative geometry (256 x 256 canvas, full variant):

| Element | Geometry |
|---|---|
| Plate | 224 x 224 at (16, 16), corner radius 56 |
| Chevron | polyline (58,92) (96,128) (58,164), stroke 24, round caps and joins |
| Grid cells | 32 x 32, radius 8, gap 12, origin (120,90) |
| Selected cell | top-right cell, white; others Index Tint |
| Optical offset | glyph group translated +7 px on x to centre its visual mass |

Small variant (16 to 32 px): plate 240 x 240 at (8, 8), chevron stroke 38, one block 56 x 76 at (146,90), radius 14. Heavier weight, no tiny gaps.

## 3. Variants

| File | Use |
|---|---|
| `logo-symbol.svg`, `logo-symbol-light.svg` | Light backgrounds (default) |
| `logo-symbol-dark.svg` | Dark backgrounds |
| `logo-symbol-small*.svg` | 16 to 32 px rendering |
| `logo-monochrome.svg` | One-colour print, stamps, watermarks. Plate with glyph knocked out |
| `logo-glyph-cobalt.svg`, `logo-glyph-white.svg` | Plate-less glyph (unplated icons, tray) |
| `wordmark-horizontal*.svg` | Symbol + wordmark lockup |
| `wordmark-compact*.svg` | Wordmark only, tight crop, for headers and footers |

`-light` means "for use on light backgrounds". `-dark` means "for use on dark backgrounds".

## 4. Clear space
Keep a margin of **x** on all sides, where x = 1/4 of the symbol plate height (equal to the plate corner radius). In the horizontal lockup, x is measured from the plate.

## 5. Minimum size

| Asset | Minimum |
|---|---|
| Symbol, small variant | 16 px |
| Symbol, full variant | 40 px |
| Horizontal lockup | 120 px wide (24 px symbol height) |
| Compact wordmark | 110 px wide |
| Print symbol | 8 mm |

## 6. Light and dark
- Light backgrounds (white, Mist, light Mica): use the `light` files.
- Dark backgrounds (Graphite, dark Mica): use the `dark` files. The plate lightens to Cobalt Light so edge contrast stays above 3:1.
- Placing the light symbol on a similar blue is not allowed. Use the monochrome white version on Launch Cobalt.

## 7. Misuse
See `previews/logo-misuse.png`. Do not: stretch, recolour outside the palette, rotate, add shadows or glows, place on low-contrast or busy backgrounds, retype the wordmark, outline or rebuild the symbol, add controllers or other game motifs.

## 8. Relationship to other marks
PlayniteCmdPal is an independent extension. The symbol is original and does not reproduce or imitate Microsoft PowerToys, Windows or Playnite marks. Use the product name in text to refer to those products; do not use their logos inside this identity.
