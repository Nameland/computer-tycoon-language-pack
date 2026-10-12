using BepInEx.Logging;
using ComputerTycoon.Localization;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UObject = UnityEngine.Object;

namespace ComputerTycoon.LanguageMod;

/// <summary>Presentation-only translations. No game models, saved data or input values are modified.</summary>
internal static partial class Runtime
{
    // Reuse the translated-text layout for each installed language pack.
    private static bool UsesLocalizedLayout => Catalog.ActiveLanguageId != "en";

    private sealed class TrackedText
    {
        public Component Owner = null!;
        public string Source = "";
        public string Rendered = "";
        public Font OriginalFont;
        public TMP_FontAsset OriginalTmpFont;
        public Material OriginalSharedMaterial;
        public bool? IsLoadingPresentation;
        public bool PresentationFontChanged;
        public float OriginalFontSize;
        public bool OriginalAutoSize;
        public float OriginalSizeMin;
        public float OriginalSizeMax;
        public bool OriginalWordWrap;
        public TextOverflowModes OriginalOverflow;
        public bool FitChanged;
        public int Version = -1;
    }

    private static readonly Dictionary<int, TrackedText> Texts = new();
    private static readonly List<KeyValuePair<int, TrackedText>> TextSnapshot = new();
    private static readonly HashSet<string> ReportedErrors = new();
    private static readonly HashSet<string> FailedFonts = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, (Font Font, TMP_FontAsset Tmp)> Fonts = new(StringComparer.OrdinalIgnoreCase);
    private static ManualLogSource _log = null!;
    private static Font _font;
    private static TMP_FontAsset _tmpFont;
    private static bool _writing;
    private static bool _started;
    private static bool _needsRefresh;
    private static float _nextScan;
    private static float _nextDiscovery;
    private static int _version;
    internal static LanguageCatalog Catalog { get; private set; } = null!;

    internal static void Initialize(LanguageCatalog catalog, ManualLogSource log)
    {
        Catalog = catalog;
        _log = log;
    }

    internal static void Update()
    {
        try
        {
            if (!_started)
            {
                _started = true;
                EnsureEnglish();
                SelectLanguage(Plugin.Instance.SelectedLanguage.Value);
            }
            // A stripped or disabled legacy input module must not stop text refreshes.
            try
            {
                if (Input.GetKeyDown(KeyCode.F8))
                    SetWindowVisible(!_showWindow);
            }
            catch (Exception error) { Report("F8 keyboard shortcut", error); }
            var now = Time.unscaledTime;
            if (now >= _nextDiscovery)
            {
                _nextDiscovery = now + 2f;
                EnsureEnglish();
                DiscoverLanguageMenus();
                DiscoverTexts();
                CleanupDestroyedObjects();
            }
            if (_needsRefresh || now >= _nextScan)
            {
                _nextScan = now + .25f;
                RefreshCountryCaptions();
                RefreshTexts(_needsRefresh);
                RefreshBakedTextLocalization();
                RefreshMapPreviewLocalization();
                _needsRefresh = false;
            }
        }
        catch (Exception error) { Report("update", error); }
    }

    private static void EnsureEnglish()
    {
        try
        {
            // Every package receives the same keys regardless of OS language or old I2 prefs.
            I2.Loc.LocalizationManager.EnableChangingCultureInfo(false);
            if (I2.Loc.LocalizationManager.CurrentLanguage != "English")
                I2.Loc.LocalizationManager.SetLanguageAndCode("English", "en", false, true);
        }
        catch (Exception error) { Report("I2 English source language", error); }
    }

    internal static void SelectIndex(int index)
    {
        if (index >= 0 && index < Catalog.Languages.Count)
            SelectLanguage(Catalog.Languages[index].Id);
    }

    internal static void SelectLanguage(string id)
    {
        try
        {
            if (!Catalog.Languages.Any(language => language.Id == id))
                id = "en";
            Catalog.SelectLanguage(id);
            Plugin.Instance.SelectedLanguage.Value = Catalog.ActiveLanguageId;
            Plugin.Instance.Config.Save();
            PrepareFont();
            PrepareLanguageMenuFonts();
            _version++;
            _needsRefresh = true;
            _needsMapCaptionRefresh = true;
            RefreshLanguageMenus();
            _log.LogInfo($"Selected language '{Catalog.ActiveLanguageId}'; refreshing {Texts.Count} text components from their original English values.");
        }
        catch (Exception error) { Report("select language " + id, error); }
    }

    private static void PrepareFont(string requestedFont = null)
    {
        var language = Catalog.Languages.FirstOrDefault(info => info.Id == Catalog.ActiveLanguageId);
        var fontName = requestedFont ?? (string.IsNullOrWhiteSpace(language?.Font) ? "Arial" : language.Font);
        if (FailedFonts.Contains(fontName))
            return;
        if (!Fonts.ContainsKey(fontName) && Fonts.Count >= 16)
            fontName = "Arial";
        Font createdFont = null;
        TMP_FontAsset createdTmp = null;
        var stage = "allocate Unity font";
        try
        {
            if (!Fonts.TryGetValue(fontName, out var pair))
            {
                // Both public factories are broken in this interop build: String is
                // unstrippable; String[] calls a constructor that was not generated.
                // Reproduce that constructor through the available native internal call.
                var font = createdFont = new Font(IL2CPP.il2cpp_object_new(Il2CppClassPointerStore<Font>.NativeClassPtr));
                stage = "initialize Unity font";
                Font.Internal_CreateDynamicFont(font, new Il2CppStringArray(new[] { fontName, "Arial", "Segoe UI" }), 32);
                if (!font)
                    throw new InvalidOperationException("Native font creation returned an invalid font.");
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
                    throw new InvalidOperationException("TMP could not create a font asset for " + fontName);
                stage = "initialize TMP atlas";
                tmp.name = "ComputerTycoon Language Packs - " + fontName;
                tmp.hideFlags = HideFlags.HideAndDontSave;
                tmp.isMultiAtlasTexturesEnabled = true;
                UObject.DontDestroyOnLoad(tmp);
                Fonts[fontName] = pair = (font, tmp);
                _log.LogInfo($"Prepared local '{fontName}' font fallback for translated UI.");
            }
            _font = pair.Font;
            _tmpFont = pair.Tmp;
        }
        catch (Exception error)
        {
            if (createdTmp)
                UObject.Destroy(createdTmp);
            if (createdFont)
                UObject.Destroy(createdFont);
            if (FailedFonts.Count < 64)
                FailedFonts.Add(fontName);
            Report("font " + fontName + " / " + stage, error);
        }
    }

    internal static void TextEnabled(Component component)
    {
        if (!_started || _writing)
            return;
        try
        {
            var tmp = component.TryCast<TMP_Text>();
            var legacy = tmp is null ? component.TryCast<Text>() : null;
            var current = tmp is not null ? tmp.text : legacy?.text;
            if (current is null)
                return;
            var translated = current;
            Intercept(component, ref translated);
            if (translated == current)
                return;
            _writing = true;
            try
            {
                if (tmp is not null)
                    tmp.text = translated;
                else if (legacy is not null)
                    legacy.text = translated;
            }
            finally { _writing = false; }
        }
        catch (Exception error) { Report("enable text", error); }
    }

    internal static void Intercept(Component component, ref string value)
    {
        if (_writing || !_started || value is null || _countryLabelDrawDepth > 0)
            return;
        try
        {
            if (!component)
                return;
            if (IsCountryMapText(component))
            {
                // WMSK must receive its final caption before it calculates character
                // spacing and mesh indexes. Never replace text inside its TMP setter.
                return;
            }
            if (!CanTranslate(component, value))
                return;
            var record = Observe(component, value);
            value = record.Rendered = TranslateForComponent(component, record.Source);
            ApplyFont(component, record, value);
            record.Version = _version;
        }
        catch (Exception error) { Report("text interception", error); }
    }

    private static TrackedText Observe(Component component, string current)
    {
        var id = component.GetInstanceID();
        if (!Texts.TryGetValue(id, out var record) || !record.Owner || record.Owner.Pointer != component.Pointer)
        {
            record = new TrackedText { Owner = component, Source = current, Rendered = current };
            if (component.TryCast<Text>() is { } legacy)
            {
                record.OriginalFont = legacy.font;
                record.OriginalFontSize = legacy.fontSize;
                record.OriginalAutoSize = legacy.resizeTextForBestFit;
                record.OriginalSizeMin = legacy.resizeTextMinSize;
                record.OriginalSizeMax = legacy.resizeTextMaxSize;
            }
            else if (component.TryCast<TMP_Text>() is { } tmp)
            {
                record.OriginalTmpFont = tmp.font;
                record.OriginalSharedMaterial = tmp.fontSharedMaterial;
                record.OriginalFontSize = tmp.fontSize;
                record.OriginalAutoSize = tmp.enableAutoSizing;
                record.OriginalSizeMin = tmp.fontSizeMin;
                record.OriginalSizeMax = tmp.fontSizeMax;
                record.OriginalWordWrap = tmp.enableWordWrapping;
                record.OriginalOverflow = tmp.overflowMode;
            }
            Texts[id] = record;
        }
        else if (current != record.Rendered)
        {
            // Reassigning our own rendered text must not erase the English original.
            record.Source = current;
        }
        return record;
    }

    private static bool CanTranslate(Component component, string candidate = null)
    {
        if (!component || !component.gameObject.scene.IsValid())
            return false;
        if (IsCountryMapText(component))
            return false;
        if (IsOriginalChangelog(component))
            return false;
        var tmpInput = ParentComponent<TMP_InputField>(component);
        if (tmpInput && tmpInput.textComponent && tmpInput.textComponent.Pointer == component.Pointer)
            return false;
        var legacyInput = ParentComponent<InputField>(component);
        if (legacyInput && legacyInput.textComponent && legacyInput.textComponent.Pointer == component.Pointer)
            return false;
        // Input placeholders and decorative labels are presentation, not the value.
        // The editable text above is protected even when its value equals a dictionary key.
        if (tmpInput || legacyInput)
            return !InsideLanguageMenu(component);
        if (IsMapCompanyPlaceholder(component, candidate))
            return !InsideLanguageMenu(component);
        var namePath = component.transform.name;
        for (var parent = component.transform.parent; parent; parent = parent.parent)
            namePath = parent.name + "/" + namePath;
        if (ModelNameSlots.IsProtected(namePath))
            return false;
        // These narrow, explicit name slots show player-assigned/model names. Do not
        // use a generic "Name" match: that would also suppress ordinary column labels.
        var node = component.transform;
        for (var depth = 0; depth < 2 && node; depth++, node = node.parent)
        {
            var slot = node.name.Replace("_", "").Replace("-", "").Replace(" ", "").ToLowerInvariant();
            if (slot.Contains("playername") || slot.Contains("companyname") || slot.Contains("productname") ||
                slot.Contains("computername") || slot.Contains("savename") || slot.Contains("prototypename") ||
                slot.Contains("managername") || slot.Contains("modelname"))
            {
                // A name-entry row also contains an ordinary heading beside the input.
                // Inspect the actual input component rather than excluding the whole row.
                if (HasNameEntryInput(node) || (node.parent && HasNameEntryInput(node.parent)))
                    return !InsideLanguageMenu(component);
                return false;
            }
        }
        // Placeholders remain translatable; editable text stays exactly as the user typed.
        return !InsideLanguageMenu(component);
    }

    private static bool IsMapCompanyPlaceholder(Component component, string candidate)
    {
        // Whitelist only this shipped placeholder in this exact map label. The
        // setter supplies its incoming value: reading component.text there would
        // still see the old placeholder while a real company name is being set.
        if (!HasContextPath(component, "MapControlWindow", "Panel", "OpponentCompanyNameSmall_lbl"))
            return false;
        if (candidate is null)
        {
            var tmp = component.TryCast<TMP_Text>();
            candidate = tmp is not null ? tmp.text : component.TryCast<Text>()?.text;
        }
        if (candidate is null)
            return false;
        // A currently displayed translation must resolve to its original source
        // so that switching back to English can restore the placeholder. Instance ids
        // alone are insufficient because Unity can reuse them for another object.
        if (Texts.TryGetValue(component.GetInstanceID(), out var record) && record.Owner &&
            record.Owner.Pointer == component.Pointer && candidate == record.Rendered)
            candidate = record.Source;
        return string.Equals(candidate, "Opponent's Company Name", StringComparison.Ordinal);
    }
    private static bool HasNameEntryInput(Component component) =>
        component.GetComponentInChildren(Il2CppType.Of<TMP_InputField>(), true) ||
        component.GetComponentInChildren(Il2CppType.Of<InputField>(), true);

    private static void DiscoverTexts()
    {
        foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>())
            SafelyDiscover(text, () => text.text);
        foreach (var text in Resources.FindObjectsOfTypeAll<Text>())
            SafelyDiscover(text, () => text.text);
    }

    private static void SafelyDiscover(Component component, Func<string> read)
    {
        try
        {
            if (!component)
                return;
            if (Texts.TryGetValue(component.GetInstanceID(), out var known) && known.Owner && known.Owner.Pointer == component.Pointer)
                return;
            var current = read() ?? "";
            if (CanTranslate(component, current))
                Observe(component, current);
        }
        catch (Exception error) { Report("discover text", error); }
    }

    private static void RefreshTexts(bool includeInactive)
    {
        // Unity setters may synchronously trigger other UI updates, hence a snapshot.
        TextSnapshot.Clear();
        TextSnapshot.AddRange(Texts);
        foreach (var entry in TextSnapshot)
        {
            var record = entry.Value;
            try
            {
                if (!record.Owner)
                {
                    Texts.Remove(entry.Key);
                    continue;
                }
                if (!includeInactive && !record.Owner.gameObject.activeInHierarchy)
                    continue;
                var tmp = record.Owner.TryCast<TMP_Text>();
                var legacy = tmp is null ? record.Owner.TryCast<Text>() : null;
                var current = tmp is not null ? tmp.text : legacy?.text;
                if (current is null)
                    continue;
                if (current == record.Rendered && record.Version == _version)
                    continue;
                if (!CanTranslate(record.Owner, current))
                    continue;
                if (current != record.Rendered)
                    record.Source = current;
                var translated = TranslateForComponent(record.Owner, record.Source);
                record.Rendered = translated;
                ApplyFont(record.Owner, record, translated);
                record.Version = _version;
                if (current == translated)
                    continue;
                _writing = true;
                try
                {
                    if (tmp is not null)
                        tmp.text = translated;
                    else if (legacy is not null)
                        legacy.text = translated;
                }
                finally { _writing = false; }
            }
            catch (Exception error) { Report("refresh text", error); }
        }
        TextSnapshot.Clear();
        RefreshScreenLayouts();
        RefreshResearchDetailsLayouts();
        RefreshManagerRowLayouts();
        RefreshEventPresentationLayouts();
        RefreshCourtNoteLayouts();
        RefreshSmallEventLabelLayouts();
        RefreshSiteLabelLayouts();
        RefreshSiteOutputPresentation();
        RefreshNotificationRows();
    }

    private static void ApplyFont(Component component, TrackedText record, string value)
    {
        ApplyShortLabelFit(component, record, value);
        ApplyScreenLayout(component, record, value);
        ApplyOfficeActionLayout(component, record, value);
        ApplyManagerRowLayout(component, record, value);
        ApplyPrototypeFilterLayout(component, record, value);
        ApplyNegotiationsLayout(component, record);
        ApplyResearchDetailsLayout(component, record);
        ApplyEventPresentationLayout(component, record, value);
        ApplyCourtNoteLayout(component, record);
        ApplySmallEventLabelLayout(component, record);
        ApplySiteLabelLayout(component, record);
        ApplyNotificationRowLayout(component);
        if (component.TryCast<TMP_Text>() is { } tmp)
        {
            if (ApplyLoadingPresentationFont(tmp, record))
                return;
            if (!_tmpFont || !tmp.font || tmp.font.Pointer == _tmpFont.Pointer)
                return;
            // Owned fonts are assigned directly by the artwork/loading handlers.
            // Linking them to each other would create cycles after language changes.
            if (Fonts.Values.Any(pair => pair.Tmp && pair.Tmp.Pointer == tmp.font.Pointer))
                return;
            var fallbacks = tmp.font.fallbackFontAssetTable;
            if (fallbacks is null)
                tmp.font.fallbackFontAssetTable = fallbacks = new Il2CppSystem.Collections.Generic.List<TMP_FontAsset>();
            if (!fallbacks.Contains(_tmpFont))
                fallbacks.Add(_tmpFont);
        }
        else if (component.TryCast<Text>() is { } legacy)
        {
            var desired = Catalog.ActiveLanguageId != "en" && value.Any(character => character > 127) && _font ? _font : record.OriginalFont;
            if (desired && (!legacy.font || legacy.font.Pointer != desired.Pointer))
                legacy.font = desired;
        }
    }

    private static void ApplyShortLabelFit(Component component, TrackedText record, string value)
    {
        if (record.OriginalFontSize <= 0)
            return; // Menu font-only call, without a tracked layout.
        var tmp = component.TryCast<TMP_Text>();
        var legacy = tmp is null ? component.TryCast<Text>() : null;
        var rect = tmp is not null ? tmp.rectTransform : legacy?.rectTransform;
        var shouldFit = !IsCountryFilterText(component) && Catalog.ActiveLanguageId != "en" && value != record.Source &&
            record.Source.Length <= 100 && !record.Source.Contains('\n') && !record.Source.Contains("<br", StringComparison.OrdinalIgnoreCase) &&
            rect && rect.rect.height > 0 && rect.rect.height <= record.OriginalFontSize * 2.2f;
        if (shouldFit)
        {
            if (record.FitChanged)
                return;
            if (tmp is not null)
            {
                tmp.fontSizeMax = record.OriginalAutoSize ? record.OriginalSizeMax : record.OriginalFontSize;
                tmp.fontSizeMin = record.OriginalAutoSize ? record.OriginalSizeMin : record.OriginalFontSize * .45f;
                tmp.enableAutoSizing = true;
                // Preserve multiline layouts in research nodes and compatibility lists.
                // Auto-sizing alone must never turn a wrapped label into an overflowing line.
                tmp.enableWordWrapping = record.OriginalWordWrap;
                tmp.overflowMode = record.OriginalAutoSize ? record.OriginalOverflow : TextOverflowModes.Ellipsis;
            }
            else if (legacy is not null)
            {
                legacy.resizeTextMaxSize = record.OriginalAutoSize ? (int)record.OriginalSizeMax : (int)record.OriginalFontSize;
                legacy.resizeTextMinSize = record.OriginalAutoSize ? (int)record.OriginalSizeMin : Math.Max(1, (int)Math.Ceiling(record.OriginalFontSize * .45f));
                legacy.resizeTextForBestFit = true;
            }
            record.FitChanged = true;
        }
        else if (record.FitChanged)
        {
            if (tmp is not null)
            {
                tmp.enableAutoSizing = record.OriginalAutoSize;
                tmp.fontSizeMin = record.OriginalSizeMin;
                tmp.fontSizeMax = record.OriginalSizeMax;
                tmp.fontSize = record.OriginalFontSize;
                tmp.enableWordWrapping = record.OriginalWordWrap;
                tmp.overflowMode = record.OriginalOverflow;
            }
            else if (legacy is not null)
            {
                legacy.resizeTextForBestFit = record.OriginalAutoSize;
                legacy.resizeTextMinSize = (int)record.OriginalSizeMin;
                legacy.resizeTextMaxSize = (int)record.OriginalSizeMax;
                legacy.fontSize = (int)record.OriginalFontSize;
            }
            record.FitChanged = false;
        }
    }

    internal static void Report(string context, Exception error)
    {
        var key = context + ":" + error.GetType().Name + ":" + error.Message;
        if (ReportedErrors.Count < 128 && ReportedErrors.Add(key))
            _log.LogWarning($"Language mod ({context}): {error}");
    }

    private static T ParentComponent<T>(Component component) where T : Component
    {
        // Unity's unstripped generic bool overload has invalid generic constraints.
        // The native non-generic method and our correctly constrained cast are safe.
        var found = component.GetComponentInParent(Il2CppType.Of<T>(), true);
        return found ? found.TryCast<T>() : null;
    }
}
