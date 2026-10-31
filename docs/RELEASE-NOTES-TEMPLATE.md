# PlayniteCmdPal release notes template

Copy this template into the GitHub Release description only after the release candidate has passed the go/no-go checklist in [Release packaging](RELEASE-PACKAGING.md). Replace every placeholder and remove sections that do not apply. Do not publish a draft as a public release while licensing or signing gates remain open.

## PlayniteCmdPal <VERSION> — <YYYY-MM-DD>

### Highlights

- <User-visible change>

### Fixes

- <Bug fix>

### Requirements

- Windows: <tested and supported versions/builds>
- Microsoft PowerToys / Command Palette: <minimum tested version>
- Playnite: <minimum tested version>
- Architecture: x64 and/or ARM64, as actually packaged and tested

### Install or upgrade

- Install source: <live Microsoft Store product URL or verified WinGet package identifier>
- New installation: <verified user steps>
- Upgrade: <verified upgrade steps>
- Uninstall: <verified uninstall steps>

### Known issues

- <Known limitation, or "None known" only after review>

### Breaking changes

- <Breaking change and migration steps, or "None">

### Package verification

| Architecture | Package filename | Version | SHA-256 |
|---|---|---|---|
| x64 | <filename> | <version> | <hash> |
| ARM64 | <filename> | <version> | <hash> |

### Third-party notices

This release includes third-party components and notices. See the bundled `THIRD-PARTY-NOTICES.md`, `Microsoft-PowerToys-LICENSE.txt`, and the .NET runtime third-party notices. The .NET runtime is distributed under the applicable .NET Library License; see <https://dotnet.microsoft.com/dotnet_library_license.htm>.

### Support

- Report bugs: <repository issue URL>
- Troubleshooting: <link to docs/RELEASE-PACKAGING.md>
