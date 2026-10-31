# PlayniteCmdPal colour palette

| Name | HEX | RGB | Use |
|---|---|---|---|
| Launch Cobalt | `#2F6BFF` | 47, 107, 255 | Primary brand colour. Symbol plate on light backgrounds. |
| Cobalt Deep | `#1E4FD6` | 30, 79, 214 | Pressed/hover state, text-safe accent on light backgrounds. |
| Cobalt Light | `#5B8CFF` | 91, 140, 255 | Symbol plate and accent text on dark backgrounds. |
| Index Tint | `#B5CBFF` | 181, 203, 255 | Secondary grid cells inside the symbol, subtle fills. |
| Graphite | `#14181F` | 20, 24, 31 | Primary text on light, dark backgrounds, wide-tile background. |
| Slate | `#5B6475` | 91, 100, 117 | Secondary text, captions, guides. |
| Mist | `#F3F5F9` | 243, 245, 249 | Light surface, page background. |
| White | `#FFFFFF` | 255, 255, 255 | Glyph colour on plate, text on dark. |

## Theme mapping

| Element | Light background | Dark background |
|---|---|---|
| Symbol plate | `#2F6BFF` | `#5B8CFF` |
| Symbol chevron and selected cell | `#FFFFFF` | `#FFFFFF` |
| Symbol secondary cells | `#B5CBFF` | `#CFDDFF` |
| Wordmark "Playnite" | `#14181F` | `#FFFFFF` |
| Wordmark "CmdPal" | `#2F6BFF` | `#5B8CFF` |
| Wide-tile background | n/a | `#14181F` |

## Contrast (WCAG 2.x, computed by `scripts/generate-assets.py`)

| Pair | Colours | Ratio | Result |
|---|---|---|---|
| White on Launch Cobalt (symbol glyph on plate) | `#FFFFFF` on `#2F6BFF` | 4.50:1 | AA large / graphics (3:1) |
| Launch Cobalt on White | `#2F6BFF` on `#FFFFFF` | 4.50:1 | AA large / graphics (3:1) |
| Cobalt Deep on White (text-safe accent) | `#1E4FD6` on `#FFFFFF` | 6.68:1 | AA |
| Graphite on Mist | `#14181F` on `#F3F5F9` | 16.30:1 | AAA |
| Slate on White | `#5B6475` on `#FFFFFF` | 5.96:1 | AA |
| Cobalt Light on Graphite (dark theme accent) | `#5B8CFF` on `#14181F` | 5.63:1 | AA |
| White on Graphite | `#FFFFFF` on `#14181F` | 17.79:1 | AAA |
| Launch Cobalt on Graphite | `#2F6BFF` on `#14181F` | 3.96:1 | AA large / graphics (3:1) |

Notes:
- Launch Cobalt carries the white glyph at roughly the 4.5:1 threshold. It is a graphic, not body text; use Cobalt Deep for text-size accent colour on light surfaces.
- The wordmark accent "CmdPal" switches to Cobalt Light on dark backgrounds to keep contrast above 4.5:1.
