# PlayniteCmdPal typography

## Wordmark
- Typeface: **Inter SemiBold** (Inter 4.0, static TTF `fonts/Inter-SemiBold.ttf`).
- Licence: SIL Open Font License 1.1 (`LICENSES/Inter-OFL-1.1.txt`). Embedding, outlining and redistribution are permitted.
- The wordmark in every SVG is **outlined to vector paths**. No font is required to render it.
- Spelling is exactly `PlayniteCmdPal`. No spaces, hyphens or case changes.
- Tracking: -1.2 % (-0.012 em). Kerning: on (OpenType `kern`). Ligatures and contextual alternates: off.
- Two-tone treatment: "Playnite" in the text colour, "CmdPal" in the accent colour. Monochrome version uses one colour.
- Regenerate with `scripts/generate-assets.py`. Do not retype the wordmark.

## Product UI
- Use the Windows system font **Segoe UI Variable** inside the Command Palette extension and any Windows UI. It ships with Windows 11 and is **not** redistributed in this package.
- Fallback stack: `"Segoe UI Variable Text", "Segoe UI", system-ui, sans-serif`.

## Documentation and marketing
- **Inter** Regular / Medium / SemiBold (included in `fonts/`).

## Scale (documentation, web)

| Role | Size / line | Weight |
|---|---|---|
| Display | 40 / 48 px | SemiBold |
| Title | 28 / 36 px | SemiBold |
| Subtitle | 20 / 28 px | SemiBold |
| Body | 16 / 24 px | Regular |
| Caption | 13 / 18 px | Medium |
