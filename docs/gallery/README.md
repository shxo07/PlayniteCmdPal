# Command Palette Gallery preparation

This folder contains a **draft only** for a future listing in Microsoft's separate [CmdPal-Extensions](https://github.com/microsoft/CmdPal-Extensions) repository.

## Current state

- `extension.json.template` is shaped against the official Gallery extension schema.
- `listed` is deliberately `false`.
- The author identity and Microsoft Store product ID are placeholders.
- No install source has been published or approved, so this is **not ready to copy into a public Gallery submission**.

The Gallery is a catalogue, not a binary host. A submission must point to an already-published Microsoft Store or WinGet install source. The project is currently evaluating a Store-first path because the CmdPal extension is packaged as MSIX. WinGet is not selected until an installer and registration approach are validated for this packaged out-of-process COM extension.

## Completing the draft after release approval

1. Confirm the final publisher/author slug and use the same value in the Gallery folder path and the metadata ID. For example, `extensions/<author>/playnitecmdpal/` must use ID `<author>.playnitecmdpal`.
2. Replace the author display name and URL with the maintainer-approved identity.
3. Replace the placeholder Store product ID with the real Partner Center product ID, or replace the install source with a verified WinGet package identifier if that route is approved.
4. Copy `PlayniteCmdPal-Brand-Assets/icons/png/app-icon-256.png` to the Gallery folder as `icon.png`. Before submission, verify the actual file is a 256×256 PNG under 100 KB and inspect its appearance at small sizes.
5. Set `listed` to `true` only when the install source is live and the maintainer has approved the listing.
6. Copy the completed folder to the matching `extensions/<author>/playnitecmdpal/` path in a fork of `microsoft/CmdPal-Extensions`, then open a PR against that repository's `main` branch.
7. Complete Microsoft's CLA process if prompted and wait for Gallery maintainers and CI.

The Gallery schema is strict: it rejects unknown properties and requires an `installSources` array with at least one valid source. Do not use a fake ID to make a submission appear complete.

## Official references

- [Contribution guide](https://github.com/microsoft/CmdPal-Extensions/blob/main/docs/CONTRIBUTING.md)
- [Extension schema](https://github.com/microsoft/CmdPal-Extensions/blob/main/.github/schemas/extension.schema.json)
- [Sample extension](https://github.com/microsoft/CmdPal-Extensions/tree/main/extensions/microsoft/sample-extension)
