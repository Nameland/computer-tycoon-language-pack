using BepInEx.Logging;
using ComputerTycoon.Localization;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TMPro;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace ComputerTycoon.LanguageMod;

internal sealed class FontProvider
{
    private readonly LanguageCatalog _catalog;
    private readonly ManualLogSource _log;
    private readonly Action<string, Exception> _report;

    private string _languageMenuFontKey;
    private readonly HashSet<string> _failedFonts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (Font Font, TMP_FontAsset Tmp)> _fonts = new(
        StringComparer.OrdinalIgnoreCase
    );
    internal Font MenuFont { get; private set; }
    internal Font CurrentFont { get; private set; }
    internal TMP_FontAsset CurrentTmpFont { get; private set; }
    internal string CurrentFamily { get; private set; }

    internal FontProvider(
        LanguageCatalog catalog,
        ManualLogSource log,
        Action<string, Exception> report
    )
    {
        _catalog = catalog;
        _log = log;
        _report = report;
    }

    internal bool Owns(TMP_FontAsset font) =>
        font && _fonts.Values.Any(pair => pair.Tmp && pair.Tmp.Pointer == font.Pointer);

    internal void RetryFailedResources()
    {
        _failedFonts.Clear();
        _languageMenuFontKey = null;
    }

    internal void PrepareFont(string requestedFont = null)
    {
        var language = _catalog.Languages.FirstOrDefault(info =>
            info.Id == _catalog.ActiveLanguageId
        );
        var fontName =
            requestedFont ?? (string.IsNullOrWhiteSpace(language?.Font) ? "Arial" : language.Font);
        // Reserve the neutral fallback before filling the bounded font cache.
        if (_fonts.Count == 0 && !fontName.Equals("Arial", StringComparison.OrdinalIgnoreCase))
        {
            PrepareFont("Arial");
        }

        if (!_fonts.ContainsKey(fontName) && _fonts.Count >= 16)
        {
            fontName = "Arial";
        }

        if (_failedFonts.Contains(fontName))
        {
            UseFallbackFont(fontName);
            return;
        }

        Font createdFont = null;
        TMP_FontAsset createdTmp = null;
        var stage = "allocate Unity font";
        try
        {
            if (!_fonts.TryGetValue(fontName, out var pair))
            {
                // Both public factories are broken in this interop build: String is
                // unstrippable; String[] calls a constructor that was not generated.
                // Reproduce that constructor through the available native internal call.
                var font = createdFont = new Font(
                    IL2CPP.il2cpp_object_new(Il2CppClassPointerStore<Font>.NativeClassPtr)
                );
                stage = "initialize Unity font";
                Font.Internal_CreateDynamicFont(
                    font,
                    new Il2CppStringArray(new[] { fontName, "Arial", "Segoe UI" }),
                    32
                );
                if (!font)
                {
                    throw new InvalidOperationException(
                        "Native font creation returned an invalid font."
                    );
                }

                font.hideFlags = HideFlags.HideAndDontSave;
                UObject.DontDestroyOnLoad(font);
                // Commit both fonts only after initialization succeeds.
                stage = "create TMP font from OS family";
                var tmp = createdTmp = TMP_FontAsset.CreateFontAsset(fontName, "Regular", 32);
                if (!tmp)
                {
                    stage = "create TMP font from Unity font";
                    tmp = createdTmp = TMP_FontAsset.CreateFontAsset(font);
                }

                if (!tmp)
                {
                    throw new InvalidOperationException(
                        "TMP could not create a font asset for " + fontName
                    );
                }

                stage = "initialize TMP atlas";
                tmp.name = "ComputerTycoon Language Packs - " + fontName;
                tmp.hideFlags = HideFlags.HideAndDontSave;
                tmp.isMultiAtlasTexturesEnabled = true;
                UObject.DontDestroyOnLoad(tmp);
                _fonts[fontName] = pair = (font, tmp);
                _log.LogInfo($"Prepared local '{fontName}' font fallback for translated UI.");
            }

            CurrentFont = pair.Font;
            CurrentTmpFont = pair.Tmp;
            CurrentFamily = fontName;
        }
        catch (Exception error)
        {
            if (createdTmp)
            {
                UObject.Destroy(createdTmp);
            }

            if (createdFont)
            {
                UObject.Destroy(createdFont);
            }

            if (_failedFonts.Count < 64)
            {
                _failedFonts.Add(fontName);
            }

            _report("font " + fontName + " / " + stage, error);
            UseFallbackFont(fontName);
        }
    }

    private void UseFallbackFont(string failedFamily)
    {
        // Never carry over the previously selected pack's font on failure.
        CurrentFont = null;
        CurrentTmpFont = null;
        CurrentFamily = null;
        if (!failedFamily.Equals("Arial", StringComparison.OrdinalIgnoreCase))
        {
            PrepareFont("Arial");
        }
    }

    internal void PrepareMenuFont()
    {
        // All native names must remain readable before their language is selected.
        var families = _catalog
            .Languages.Select(language =>
                string.IsNullOrWhiteSpace(language.Font) ? "Arial" : language.Font
            )
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var activeFont = CurrentFont;
        var activeTmp = CurrentTmpFont;
        var activeFamily = CurrentFamily;
        Font pendingFont = null;
        try
        {
            foreach (var family in families)
            {
                PrepareFont(family);
            }

            var key = string.Join("|", families);
            if (MenuFont && key == _languageMenuFontKey)
            {
                return;
            }

            var menuFont = pendingFont = new Font(
                IL2CPP.il2cpp_object_new(Il2CppClassPointerStore<Font>.NativeClassPtr)
            );
            Font.Internal_CreateDynamicFont(menuFont, new Il2CppStringArray(families), 32);
            if (!menuFont)
            {
                return;
            }

            menuFont.hideFlags = HideFlags.HideAndDontSave;
            UObject.DontDestroyOnLoad(menuFont);
            if (MenuFont)
            {
                UObject.Destroy(MenuFont);
            }

            MenuFont = menuFont;
            _languageMenuFontKey = key;
            pendingFont = null;
        }
        catch (Exception error)
        {
            _report("language menu fonts", error);
        }
        finally
        {
            if (pendingFont)
            {
                UObject.Destroy(pendingFont);
            }

            CurrentFont = activeFont;
            CurrentTmpFont = activeTmp;
            CurrentFamily = activeFamily;
        }
    }

    internal void AddMenuFallbacks(TMP_Text text)
    {
        if (!text || !text.font)
        {
            return;
        }

        if (Owns(text.font))
        {
            return;
        }

        var fallbacks = text.font.fallbackFontAssetTable;
        if (fallbacks is null)
        {
            text.font.fallbackFontAssetTable = fallbacks =
                new Il2CppSystem.Collections.Generic.List<TMP_FontAsset>();
        }

        foreach (var pair in _fonts.Values)
        {
            if (pair.Tmp && pair.Tmp.Pointer != text.font.Pointer && !fallbacks.Contains(pair.Tmp))
            {
                fallbacks.Add(pair.Tmp);
            }
        }
    }
}
