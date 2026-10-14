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

### Formatting and lint

Use .NET SDK 8 or newer and Python 3.10 or newer for the development tools. The plugin still targets the game's .NET 6 runtime.

Install the pinned tools once, from the repository root:

```powershell
dotnet tool restore
python -m pip install -r requirements-dev.txt
```

Check C# formatting, Python lint and formatting, and PowerShell syntax:

```powershell
./tools/Check-Code.ps1
```

Use `./tools/Check-Code.ps1 -Fix` to apply formatting and safe Python lint fixes. The check script does not download dependencies or run the game. CSharpier formats both C# sources and project files; Ruff checks the Python tools. PowerShell scripts must remain compatible with Windows PowerShell 5.1.

### Runtime structure

- `Localization.Core` has no Unity dependency. `LanguagePack.cs` defines the JSON model. `LanguageCatalog.Loading.cs` validates files, `LanguageCatalog.Indexing.cs` prepares lookup tables, and `LanguageCatalog.Rules.cs` applies replacement rules. `LanguageCatalog.cs` coordinates matching and caching. Keep its cache, regex timeouts and input limits bounded.
- `Plugin.cs` registers the hooks in `Integration/Hooks.cs`. `Integration/LanguageBehaviour.cs` forwards Unity callbacks to `Runtime/Runtime.cs`, which coordinates language selection and refreshes. Unity objects must only be accessed on the game thread.
- `Text/` contains text tracking, matching, eligibility checks and presentation rules. Editable values and logical game identifiers must stay unchanged.
- `Languages/` integrates the native selector and the F8 window. Closing the window restores the input system it suspended.
- `Fonts/FontProvider.cs` owns the UI font cache, fallback selection and multilingual menu font. `Maps/MapCaptionLocalizer.cs` owns original country captions, historical rename refreshes and a separate primary font atlas. The map renderer cannot safely handle fallback submeshes. Both components are created once by `Runtime.Initialize` and keep their resource state private.
- `Maps/MapPreviewLocalization.cs` handles preview images; `Artwork/` handles translated text over artwork.
- `Layout/` groups screen-specific adjustments. These handlers and the text/menu integration still use `Runtime` partials to share tracked text and refresh state. Layout handlers store original geometry before modifying it; language changes must remain reversible.
- `Runtime/RuntimeMaintenance.cs` isolates layout refresh failures by handler and tracked instance. Ordinary text refreshes run at most four times per second, with immediate refresh requests for language changes. Object discovery runs once every two seconds.

Keep profiling tools, game copies and fault-injection code outside the public project. Report measurements with their workload; timings from a menu or a standalone translation benchmark are not gameplay FPS measurements.

### Recovery

F8 > Reload packs rereads installed files, clears failed font attempts and refreshes the current language. A temporarily invalid or locked JSON file keeps its last valid contents. A repaired file is picked up on the next reload; a deleted pack is removed. If the selected pack was deleted, English is selected. Invalid files on first launch are skipped and reported in the BepInEx log.

Malformed replacement artwork keeps the last working image for that language. Missing artwork uses the game's original image. Resource failures are retried on explicit reload, not every frame. Existing valid fonts stay cached because Unity components can still reference them.

UI refresh failures are isolated by feature and by tracked instance. Text records are committed after successful writes, and layout restoration flags must be set before changing native properties. Closing or disabling the language chooser restores the EventSystem it suspended.

Reloading packs cannot reload plugin DLLs or repair a failed native game initialization. Those cases require a restart. Newly installed system fonts may also require a restart if Unity has cached the system font list.

## Reports and pull requests

Bug reports should include game and mod versions, selected language, reproduction steps, expected behavior and actual behavior. Quote the affected text where possible. Share only relevant log excerpts and remove personal information.

Pull requests should explain the change and list the checks performed or limitations encountered. Do not commit game DLLs, extracted resources, generated interop assemblies, saves, logs, credentials or build output.

Original code contributions are accepted under [MIT](LICENSE). Identify the source and licensing of added assets; see [NOTICE.md](NOTICE.md).
