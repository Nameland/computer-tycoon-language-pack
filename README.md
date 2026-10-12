# Computer Tycoon Language Packs

An offline translation mod for Computer Tycoon on Windows. Choose a language in the game settings or edit the JSON files to improve a translation.

[Download the latest release](https://github.com/Nameland/computer-tycoon-language-pack/releases/latest) | [Report a problem](https://github.com/Nameland/computer-tycoon-language-pack/issues)

## Languages

Ukrainian, Simplified Chinese, German, French, Spanish, Italian, Turkish, Brazilian Portuguese and Hungarian. English uses the game's original text.

## Installation

You need Computer Tycoon 0.9.9.16 for Windows, 64-bit. Other game versions may need an updated mod.

### 1. Install BepInEx

BepInEx is the loader that lets the game run this mod. If you already use BepInEx 6 IL2CPP, skip to step 2.

1. Close the game.
2. In your Steam library, right-click **Computer Tycoon**, then choose **Manage > Browse local files**. Keep this folder open. It contains `Computer Tycoon.exe`.
3. [Download BepInEx 6 IL2CPP for Windows x64, build 788](https://builds.bepinex.dev/projects/bepinex_be/788/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788%2B5b766a3.zip).
4. Open the ZIP and extract all its contents into the game folder, next to `Computer Tycoon.exe`. Do not put them in a new subfolder.
5. Start the game once. The first launch may take longer while BepInEx prepares its files. When the main menu appears, close the game.

This is the BepInEx build used with the mod. If you download another build, choose **Unity.IL2CPP-win-x64**. Mono, x86 and BepInEx 5 packages are not compatible.

See the [official BepInEx installation guide](https://docs.bepinex.dev/master/articles/user_guide/installation/unity_il2cpp.html) or the [BepInEx downloads page](https://builds.bepinex.dev/projects/bepinex_be) for details.

### 2. Install the mod

1. Open [Releases](https://github.com/Nameland/computer-tycoon-language-pack/releases/latest).
2. Under **Assets**, download `ComputerTycoon.LanguageMod-0.1.0.zip`, or the equivalent ZIP for a newer version. The **Source code** downloads are not installable mod packages.
3. Extract the ZIP into the same game folder. Merge the `BepInEx` folders if Windows asks.
4. Check that this file exists:

```text
Computer Tycoon/
  BepInEx/
    plugins/
      ComputerTycoon.LanguageMod/
        ComputerTycoon.LanguageMod.dll
        Localization.Core.dll
        Languages/
```

5. Launch the game. Open **Settings > Language**, or press **F8**, and choose a language.

The Chinese pack uses Microsoft YaHei. This font must be installed for its characters to display.

## Updating and removing the mod

Close the game before updating. Replace `BepInEx/plugins/ComputerTycoon.LanguageMod/` with the folder from the new release. Your language selection is stored separately in `BepInEx/config/computer.tycoon.languagepacks.cfg`.

To uninstall, remove the plugin folder. You can also remove its configuration file. Keep BepInEx if another mod uses it. If a mod manager installed the mod, use that manager to update or remove it.

## Editing translations

Packs are JSON files in `Languages/`. After editing a pack, press **F8 > Reload packs**. Changes to the mod DLLs require a restart.

Missing translations leave the original text visible unless a matching text rule applies. The game's release notes stay in their original language. No online translation service is used.

Read [TRANSLATING.md](docs/TRANSLATING.md) for the file format and [CONTRIBUTING.md](CONTRIBUTING.md) for corrections and bug reports. The English template, `languages/en.json`, is kept in the source repository and is not installed with the mod.

## Building

You need a .NET SDK that supports `net6.0`, Windows PowerShell 5.1 or PowerShell 7, and a game installation with BepInEx already initialized.

```powershell
./tools/Build-Release.ps1 -BepInExDirectory 'X:\Games\Computer Tycoon\BepInEx'
```

The archive is written to `dist/`. It contains the mod, packs and documentation. BepInEx and game assemblies are not bundled.

## License

Original code, tools and documentation use the [MIT license](LICENSE). Game text and artwork are excluded; see [NOTICE.md](NOTICE.md).

This is an unofficial mod and is not affiliated with the game's developer.
