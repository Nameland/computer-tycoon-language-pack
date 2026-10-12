using TMPro;
using UnityEngine;
using WorldMapStrategyKit;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private sealed class CountryCaption
    {
        internal string Original;
        internal string Rendered;
    }

    private sealed class MapCaptions
    {
        internal WMSK Owner;
        internal UnityEngine.Object OriginalFont;
        internal UnityEngine.Object OriginalConfiguredFont;
        internal readonly Dictionary<IntPtr, CountryCaption> Countries = new();
    }

    private static readonly Dictionary<int, MapCaptions> CountryCaptions = new();
    private static readonly Dictionary<IntPtr, TMP_FontAsset> CountryFonts = new();
    private static readonly HashSet<IntPtr> FailedCountryFonts = new();
    private static bool _needsMapCaptionRefresh;
    private static int _countryLabelDrawDepth;

    private static bool IsCountryMapText(Component component)
    {
        if (component.TryCast<TextMeshPro>() is null)
            return false;
        var parent = component.transform.parent;
        return parent && parent.name == "TextRoot" && parent.parent &&
            parent.parent.name == "OverlayLayer" && ParentComponent<WMSK>(component);
    }

    internal static void PrepareCountryCaptions(WMSK map)
    {
        _countryLabelDrawDepth++;
        MapCaptions state = null;
        try
        {
            if (!map || map.countries is null || Catalog is null)
                return;
            var id = map.GetInstanceID();
            if (!CountryCaptions.TryGetValue(id, out state) || !state.Owner || state.Owner.Pointer != map.Pointer)
                CountryCaptions[id] = state = new MapCaptions
                {
                    Owner = map,
                    OriginalFont = map.labelsFontTMPro,
                    OriginalConfiguredFont = map._countryLabelsFontTMPro
                };
            var present = new HashSet<IntPtr>();
            var glyphs = new HashSet<char>();
            foreach (var country in map.countries)
            {
                if (country is null)
                    continue;
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

                var source = string.IsNullOrEmpty(caption.Original) ? country.name : caption.Original;
                var translated = Catalog.Translate(source);
                caption.Rendered = Catalog.ActiveLanguageId == "en" || translated == source
                    ? caption.Original : translated;
                country.customLabel = caption.Rendered;
                foreach (var character in (caption.Rendered ?? country.name ?? "").ToUpperInvariant())
                    if (!char.IsControl(character))
                        glyphs.Add(character);
            }
            foreach (var key in state.Countries.Keys.Where(key => !present.Contains(key)).ToArray())
                state.Countries.Remove(key);

            if (Catalog.ActiveLanguageId == "en")
            {
                map.labelsFontTMPro = state.OriginalFont;
                map._countryLabelsFontTMPro = state.OriginalConfiguredFont;
            }
            else if (_font)
            {
                if (!CountryFonts.TryGetValue(_font.Pointer, out var font) || !font)
                {
                    if (FailedCountryFonts.Contains(_font.Pointer))
                        throw new InvalidOperationException("Map caption font is unavailable.");
                    // WMSK indexes mesh 0 directly. UI fallback fonts and additional
                    // atlases can split one country name into incompatible meshes.
                    var family = Catalog.Languages.First(language => language.Id == Catalog.ActiveLanguageId).Font;
                    font = TMP_FontAsset.CreateFontAsset(string.IsNullOrWhiteSpace(family) ? "Arial" : family, "Regular", 32);
                    if (!font)
                    {
                        FailedCountryFonts.Add(_font.Pointer);
                        throw new InvalidOperationException("Could not create a map caption font.");
                    }
                    font.atlasWidth = 2048;
                    font.atlasHeight = 2048;
                    font.isMultiAtlasTexturesEnabled = false;
                    font.name = "Language Packs - map captions";
                    font.hideFlags = HideFlags.HideAndDontSave;
                    UnityEngine.Object.DontDestroyOnLoad(font);
                    CountryFonts[_font.Pointer] = font;
                }
                // TMP also returns false when every character is already present.
                // Check the primary atlas itself, without consulting fallback fonts.
                font.TryAddCharacters(new string(glyphs.ToArray()), false);
                var missing = glyphs.Where(character => !font.HasCharacter((int)character)).ToArray();
                if (missing.Length > 0)
                    throw new InvalidOperationException("Missing map caption characters: " +
                        string.Join(", ", missing.Select(character => "U+" + ((int)character).ToString("X4"))));
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
            // A missing system font must leave the original map usable, not interrupt
            // game initialization with mixed-font meshes or missing character indexes.
            if (map && state is not null)
            {
                foreach (var country in map.countries)
                    if (country is not null && state.Countries.TryGetValue(country.Pointer, out var caption))
                        country.customLabel = caption.Rendered = caption.Original;
                map.labelsFontTMPro = state.OriginalFont;
                map._countryLabelsFontTMPro = state.OriginalConfiguredFont;
            }
            Report("prepare country captions", error);
        }
    }

    internal static void FinishCountryCaptions()
    {
        if (_countryLabelDrawDepth > 0)
            _countryLabelDrawDepth--;
    }

    private static void RefreshCountryCaptions()
    {
        if (!_needsMapCaptionRefresh)
            return;
        _needsMapCaptionRefresh = false;
        foreach (var state in CountryCaptions.Values.ToArray())
        {
            if (!state.Owner)
                continue;
            try { state.Owner.DrawMapLabels(); }
            catch (Exception error) { Report("refresh country captions", error); }
        }
    }
}
