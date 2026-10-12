# Contributing

## Translation changes

Follow [the translation guide](docs/TRANSLATING.md). Keep source keys and substitutions intact. Explain ambiguous wording and identify the affected screens. For clipping or layout changes, include a screenshot and the resolution.

Run `python tools/validate_languages.py` and review any relevant numerical differences. Inspect the affected text in the game when possible, and state what was checked.

## Code changes

Describe the problem and resulting behavior. Keep changes focused and preserve the original text and presentation when switching to English. Translation must not modify game rules, player input or save data.

Build game integration changes against your local BepInEx IL2CPP installation using `tools/Build-Release.ps1`. For translation-engine changes, run:

```powershell
dotnet run --project tests/Localization.Core.Tests.csproj -c Release
```

Use checks appropriate to the change. Documentation-only changes do not require building or running the game.

## Reports and pull requests

Bug reports should include game and mod versions, selected language, reproduction steps, expected behavior and actual behavior. Quote the affected text where possible. Share only relevant log excerpts and remove personal information.

Pull requests should explain the change and list the checks performed or limitations encountered. Do not commit game DLLs, extracted resources, generated interop assemblies, saves, logs, credentials or build output.

Original code contributions are accepted under [MIT](LICENSE). Identify the source and licensing of added assets; see [NOTICE.md](NOTICE.md).
