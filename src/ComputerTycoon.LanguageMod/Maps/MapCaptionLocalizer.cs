using ComputerTycoon.Localization;
using TMPro;
using UnityEngine;
using WorldMapStrategyKit;

namespace ComputerTycoon.LanguageMod;

internal sealed class MapCaptionLocalizer
{
    private sealed class CountryCaption
    {
        internal string Original;
        internal string Rendered;
    }

    private sealed class MapState
    {
        internal WMSK Owner;
        internal UnityEngine.Object OriginalFont;
        internal UnityEngine.Object OriginalConfiguredFont;
        internal readonly Dictionary<IntPtr, CountryCaption> Countries = new();
    }

    private readonly Dictionary<int, MapState> _captions = new();
    private readonly Dictionary<IntPtr, TMP_FontAsset> _fonts = new();
    private readonly HashSet<IntPtr> _failedFonts = new();
    private bool _needsRefresh;
    private int _drawDepth;

    private readonly LanguageCatalog _catalog;
    private readonly FontProvider _fontProvider;
    private readonly Action<string, Exception> _report;
    private readonly List<int> _staleIds = new();

    internal MapCaptionLocalizer(
        LanguageCatalog catalog,
        FontProvider fonts,
        Action<string, Exception> report
    )
    {
        _catalog = catalog;
        _fontProvider = fonts;
        _report = report;
    }

    internal bool IsDrawing => _drawDepth > 0;

    internal void RequestRefresh() => _needsRefresh = true;

    internal void RetryFailedResources() => _failedFonts.Clear();

    internal void CleanupDestroyedMaps()
    {
        _staleIds.Clear();
        foreach (var entry in _captions)
        {
            if (!entry.Value.Owner)
            {
                _staleIds.Add(entry.Key);
            }
        }
        foreach (var id in _staleIds)
        {
            _captions.Remove(id);
        }
        _staleIds.Clear();
    }

    internal void PrepareCountryCaptions(WMSK map)
    {
        _drawDepth++;
        MapState state = null;
        TMP_FontAsset pendingFont = null;
        try
        {
            if (!map || map.countries is null || _catalog is null)
            {
                return;
            }

            var id = map.GetInstanceID();
            if (
                !_captions.TryGetValue(id, out state)
                || !state.Owner
                || state.Owner.Pointer != map.Pointer
            )
            {
                _captions[id] = state = new MapState
                {
                    Owner = map,
                    OriginalFont = map.labelsFontTMPro,
                    OriginalConfiguredFont = map._countryLabelsFontTMPro,
                };
            }

            var present = new HashSet<IntPtr>();
            var glyphs = new HashSet<char>();
            foreach (var country in map.countries)
            {
                if (country is null)
                {
                    continue;
                }

                present.Add(country.Pointer);
                if (!state.Countries.TryGetValue(country.Pointer, out var caption))
                {
                    caption = new CountryCaption { Original = country.customLabel };
                    state.Countries.Add(country.Pointer, caption);
                }
                else if (country.customLabel != caption.Rendered)
                {
                    // Decorators can replace a caption during historical border changes.
                    caption.Original = country.customLabel;
                }

                var source = string.IsNullOrEmpty(caption.Original)
                    ? country.name
                    : caption.Original;
                var translated = _catalog.Translate(source);
                caption.Rendered =
                    _catalog.ActiveLanguageId == "en" || translated == source
                        ? caption.Original
                        : translated;
                country.customLabel = caption.Rendered;
                // WMSK uses the country name for both null and empty custom labels.
                // Missing translations still need their original glyphs in mesh 0.
                var displayed = string.IsNullOrEmpty(caption.Rendered) ? source : caption.Rendered;
                foreach (var character in (displayed ?? "").ToUpperInvariant())
                {
                    if (!char.IsControl(character))
                    {
                        glyphs.Add(character);
                    }
                }
            }

            foreach (var key in state.Countries.Keys.Where(key => !present.Contains(key)).ToArray())
            {
                state.Countries.Remove(key);
            }

            if (_catalog.ActiveLanguageId == "en")
            {
                map.labelsFontTMPro = state.OriginalFont;
                map._countryLabelsFontTMPro = state.OriginalConfiguredFont;
            }
            else if (_fontProvider.CurrentFont)
            {
                if (!_fonts.TryGetValue(_fontProvider.CurrentFont.Pointer, out var font) || !font)
                {
                    if (_failedFonts.Contains(_fontProvider.CurrentFont.Pointer))
                    {
                        throw new InvalidOperationException("Map caption font is unavailable.");
                    }

                    // WMSK indexes mesh 0 directly. UI fallback fonts and additional
                    // atlases can split one country name into incompatible meshes.
                    // Key and family must describe the same resolved font, including
                    // a fallback selected after a missing font or a full font cache.
                    font = pendingFont = TMP_FontAsset.CreateFontAsset(
                        _fontProvider.CurrentFamily,
                        "Regular",
                        32
                    );
                    if (!font)
                    {
                        _failedFonts.Add(_fontProvider.CurrentFont.Pointer);
                        throw new InvalidOperationException("Could not create a map caption font.");
                    }

                    font.atlasWidth = 2048;
                    font.atlasHeight = 2048;
                    font.isMultiAtlasTexturesEnabled = false;
                    font.name = "Language Packs - map captions";
                    font.hideFlags = HideFlags.HideAndDontSave;
                    UnityEngine.Object.DontDestroyOnLoad(font);
                    _fonts[_fontProvider.CurrentFont.Pointer] = font;
                    pendingFont = null;
                }

                // TMP also returns false when every character is already present.
                // Check the primary atlas itself, without consulting fallback fonts.
                var missing = glyphs
                    .Where(character => !font.HasCharacter((int)character))
                    .ToArray();
                if (missing.Length > 0)
                {
                    font.TryAddCharacters(new string(missing), false);
                    missing = missing
                        .Where(character => !font.HasCharacter((int)character))
                        .ToArray();
                }

                if (missing.Length > 0)
                {
                    throw new InvalidOperationException(
                        "Missing map caption characters: "
                            + string.Join(
                                ", ",
                                missing.Select(character => "U+" + ((int)character).ToString("X4"))
                            )
                    );
                }

                map.labelsFontTMPro = font;
                map._countryLabelsFontTMPro = font;
            }
            else
            {
                throw new InvalidOperationException("Map caption font is not ready.");
            }
        }
        catch (Exception error)
        {
            if (pendingFont)
            {
                UnityEngine.Object.Destroy(pendingFont);
            }

            // A missing system font must leave the original map usable, not interrupt
            // game initialization with mixed-font meshes or missing character indexes.
            if (map && state is not null)
            {
                map.labelsFontTMPro = state.OriginalFont;
                map._countryLabelsFontTMPro = state.OriginalConfiguredFont;
                if (map.countries is not null)
                {
                    foreach (var country in map.countries)
                    {
                        if (
                            country is not null
                            && state.Countries.TryGetValue(country.Pointer, out var caption)
                        )
                        {
                            country.customLabel = caption.Rendered = caption.Original;
                        }
                    }
                }
            }

            _report("prepare country captions", error);
        }
    }

    internal void FinishCountryCaptions()
    {
        if (_drawDepth > 0)
        {
            _drawDepth--;
        }
    }

    internal void CountryRenamed(WMSK map, string previousName, string newName)
    {
        if (
            !map
            || !_captions.TryGetValue(map.GetInstanceID(), out var state)
            || !state.Owner
            || state.Owner.Pointer != map.Pointer
        )
        {
            return;
        }

        // CountryRename changes the name and decorator lookup, but does not request
        // a label redraw. Defer it until the historical event finishes updating.
        _needsRefresh = true;
        if (map.countries is null)
        {
            return;
        }

        foreach (var country in map.countries)
        {
            if (
                country is not null
                && country.name == newName
                && state.Countries.TryGetValue(country.Pointer, out var caption)
                && caption.Original == previousName
            )
            {
                // A custom caption that echoed the old name follows the rename.
                // Preserve unrelated custom captions supplied by map decorators.
                caption.Original = newName;
            }
        }
    }

    internal void RefreshIfRequested()
    {
        if (!_needsRefresh)
        {
            return;
        }

        _needsRefresh = false;
        foreach (var state in _captions.Values.ToArray())
        {
            if (!state.Owner)
            {
                continue;
            }

            try
            {
                state.Owner.DrawMapLabels();
            }
            catch (Exception error)
            {
                _report("refresh country captions", error);
            }
        }
    }
}
