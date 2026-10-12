# Translation guide

Language packs are UTF-8 JSON files in `languages/`. You can edit translations without rebuilding the mod. The format is defined in [language-pack.schema.json](language-pack.schema.json).

## Editing a pack

Edit the values in `strings`, leaving the source keys unchanged. Keys are case-sensitive and may contain significant punctuation, spaces, line breaks and markup. Use valid JSON without comments, duplicate keys or trailing commas.

Most keys contain the original game text. Context keys such as `Computer list column: Base` and `Baked artwork: ...` identify specific UI elements. For these keys, the value in `en.json` provides the displayed English text. Keep the entire key and translate its value. Entries with and without animation tags may both be needed because the game removes some tags before displaying text.

Preserve the following in translated values:

- Placeholders such as `{0}`, `{1}` and `**playername**`, including their spelling and number of occurrences. Reorder them only where the sentence requires it.
- Rich-text tags and their parameters, such as `<b>` and `<color=...>`.
- Leading and trailing spaces used to join sentence fragments, and `\n` or `\r\n` line breaks.
- Quantities, units, percentages, comparisons and gameplay conditions.

Use consistent terms across labels, descriptions and tutorial references. Adapt jokes and fictional titles where needed, while preserving their meaning and tone. Do not change player-entered names, model identifiers or gameplay rules. Keep short labels concise; check the affected screen if a change makes one longer. Use ordinary spaces and punctuation rather than invisible formatting characters.

If the source is unclear or contradictory, explain the issue in the pull request. Do not guess how the game works.

## Adding a language

1. Copy `languages/en.json` to `languages/<id>.json`.
2. Set `id`, `name`, `nativeName` and `font`.
3. Translate values in `strings` and add dynamic rules where needed. The English template does not supply translated rules; use existing packs to identify applicable patterns.
4. Validate the file and load it in the game.

| Field | Meaning |
| --- | --- |
| `schemaVersion` | Must be `1` |
| `id` | Unique language identifier, such as `pl`; ASCII letter followed by up to 31 ASCII letters, digits or hyphens |
| `name` | Language name in English |
| `nativeName` | Language name shown in the menu |
| `font` | Installed system font name, not a file path; defaults to `Arial` |
| `strings` | Source keys mapped to translated values |
| `rules` | Ordered dynamic translation rules; may be empty |

Language IDs are case-insensitive and must be unique. Match the filename to the identifier. Use a font containing the required characters; font files are not distributed with packs. Packs are limited to 16 MiB each, with a combined input limit of 128 MiB. A pack may contain up to 50000 strings and 1024 rules. Source keys and translated values may each contain up to 32768 characters; names and font metadata are limited to 128 characters.

A minimal example:

```json
{
  "schemaVersion": 1,
  "id": "pl",
  "name": "Polish",
  "nativeName": "Polski",
  "font": "Arial",
  "strings": {
    "Language": "Język"
  },
  "rules": []
}
```

Packs can be incomplete. Missing or empty entries leave the original text visible unless another rule matches. Selecting English bypasses translation and does not require `en.json` in the installed mod. Keep that file in the source repository as the reference template.

For newly discovered game text, add the exact source key and English value to `en.json`, then add the translation. Do not guess whitespace or add library diagnostics and game release-note bodies to the dictionary.

## Dynamic text

Prefer exact `strings` entries for fixed text. Use `rules` for sentences containing changing numbers or other captured values. Patterns use .NET regular expressions and must start with `^` and end with `$`.

```json
{
  "pattern": "^(?<count>[0-9]+) days$",
  "replacement": "Liczba dni: ${count}"
}
```

Rules run in order after exact and normalized dictionary matching. The first matching rule is used. Avoid unrestricted patterns such as `^(.*)$`. Each expression has a 30 ms timeout. A rule that times out is disabled until the language is selected again or packs are reloaded. Patterns are limited to 4096 characters; replacement templates and expanded text are limited to 32768 characters. Excessive recursive expansion leaves the original text visible. JSON requires doubled backslashes: `\\d` in a JSON string represents `\d` in the regular expression.

Replacements use .NET syntax: `$1` or `${name}` inserts a captured value; `$$` inserts a literal dollar sign. Keep variable numbers and user-entered names in captures rather than replacing them with fixed text.

If a captured value is itself a translatable game term, list its group in `translateGroups`:

```json
{
  "pattern": "^(?<person>.+) has released a new (?<kind>Home Computer|Personal Computer)\\.$",
  "replacement": "${person}: ${kind}.",
  "translateGroups": ["kind"]
}
```

This example illustrates capture handling: `kind` is translated through the pack, while `person` is preserved. Write a complete sentence appropriate to the target language in an actual rule.

Use `${kind}` for a translated named group or `${1}` for a translated numbered group. A plain `$1` inserts the capture without applying `translateGroups`. Listed groups must exist in the pattern, and group `0` is not allowed. Do not translate captures containing player, company or product names. Nested translation is limited to four levels; avoid self-referential rules.

The engine also supports numeric templates, text inside markup, lines and sentence fragments. A missing full-sentence entry can therefore produce a partially translated result. Do not assume that deleting one entry disables all translation of that sentence.

## Optional map previews

Place replacement PNGs in `languages/<id>-assets/` next to the JSON file:

| File | Map selection preview |
| --- | --- |
| `map-vanilla.png` | Randomized, post-2000 borders |
| `map-historical.png` | Historical |
| `map-simplified.png` | Simplified |

Each image must be at most 8 MiB and no larger than 4096 pixels on either axis. Preserve the original composition and aspect ratio. These files replace selection previews only, not gameplay maps or scenario data. Missing or invalid files leave the original preview in place. English restores the original images.

Reload packs after changing images. Check country labels against the corresponding map mode. Identify asset provenance and licensing in the pull request; see [NOTICE.md](../NOTICE.md).

## Validation and in-game review

From the repository root, using Python 3:

```powershell
python tools/validate_languages.py
```

The validator checks for duplicate or unknown keys, missing metadata, changed placeholders and markup, and missing spaces at fragment boundaries. Numerical differences are written to `work/numeric-review.json` for manual review. It does not assess translation quality, execute .NET regex rules or confirm that text fits in the UI.

Copy the edited JSON and optional asset directory to `BepInEx/plugins/ComputerTycoon.LanguageMod/Languages/` in the game installation. Press **F8 > Reload packs**, then select the language. Opening F8 alone does not reload files.

Inspect the changed text and its surrounding context. For rule changes, include representative variable values and names. For a new pack, also check font coverage and switching back to English. State what you checked. For a text change, checking the affected screen is enough.

If the pack is missing from the menu, check its metadata, identifier uniqueness and `BepInEx/LogOutput.log`. See [CONTRIBUTING.md](../CONTRIBUTING.md) for submitting changes.
