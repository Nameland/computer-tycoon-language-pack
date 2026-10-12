using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private sealed class LanguageMenu
    {
        public Component Component = null!;
        public int Version = -1;
    }

    private static readonly Dictionary<int, LanguageMenu> Menus = new();
    internal static bool UpdatingLanguageMenu
    {
        get; private set;
    }
    private static bool _showWindow;
    private static bool _settingsVisible;
    private static GUIStyle _buttonStyle;
    private static GUIStyle _labelStyle;
    private static int _page;
    private static EventSystem _suspendedEventSystem;
    private static Font _languageMenuFont;
    private static string _languageMenuFontKey;

    private static void PrepareLanguageMenuFonts()
    {
        // All native names must remain readable before their language is selected.
        var families = Catalog.Languages.Select(language => string.IsNullOrWhiteSpace(language.Font) ? "Arial" : language.Font)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var activeFont = _font;
        var activeTmp = _tmpFont;
        try
        {
            foreach (var family in families)
                PrepareFont(family);
            var key = string.Join("|", families);
            if (_languageMenuFont && key == _languageMenuFontKey)
                return;
            var menuFont = new Font(Il2CppInterop.Runtime.IL2CPP.il2cpp_object_new(
                Il2CppInterop.Runtime.Il2CppClassPointerStore<Font>.NativeClassPtr));
            Font.Internal_CreateDynamicFont(menuFont,
                new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray(families), 32);
            if (!menuFont)
                return;
            menuFont.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(menuFont);
            if (_languageMenuFont)
                UnityEngine.Object.Destroy(_languageMenuFont);
            _languageMenuFont = menuFont;
            _languageMenuFontKey = key;
        }
        catch (Exception error) { Report("language menu fonts", error); }
        finally { _font = activeFont; _tmpFont = activeTmp; }
    }

    private static void SetWindowVisible(bool visible)
    {
        GuardWindowMouse(visible);
        _showWindow = visible;
        // Prevent a click on the floating chooser from activating a game button behind it.
        if (visible)
        {
            var current = EventSystem.current;
            if (current && current.enabled)
            {
                _suspendedEventSystem = current;
                current.enabled = false;
            }
        }
        else if (_suspendedEventSystem)
        {
            _suspendedEventSystem.enabled = true;
            _suspendedEventSystem = null;
        }
    }

    internal static void BindLanguageObject(GameObject gameObject)
    {
        try
        {
            if (!gameObject || !gameObject.scene.IsValid())
                return;
            var tmp = gameObject.GetComponent<TMP_Dropdown>();
            var legacy = gameObject.GetComponent<Dropdown>();
            if (tmp)
                BindMenu(tmp);
            if (legacy)
                BindMenu(legacy);
        }
        catch (Exception error) { Report("bind language selector", error); }
    }

    internal static void PrepareLanguageMenu(Component component)
    {
        try
        {
            if (!component || !component.gameObject.scene.IsValid())
                return;
            // Reconcile immediately before Unity clones the options into its popup.
            // A periodic refresh alone is too late for an already opened native list.
            if (Menus.ContainsKey(component.GetInstanceID()) ||
                component.gameObject.GetComponent<I2.Loc.SetLanguageDropdown>())
                BindMenu(component);
            else
                DiscoverNamedMenu(component);
        }
        catch (Exception error) { Report("open language selector", error); }
    }
    private static void BindMenu(Component component)
    {
        var id = component.GetInstanceID();
        if (!Menus.TryGetValue(id, out var menu) || !menu.Component || menu.Component.Pointer != component.Pointer)
        {
            menu = new LanguageMenu { Component = component };
            Menus[id] = menu;
            _log.LogInfo($"Using the game's language dropdown: {component.gameObject.name} ({component.GetIl2CppType().FullName}).");
        }
        if (menu.Version != _version || !MenuMatches(menu))
            PopulateMenu(menu);
    }

    private static bool MenuMatches(LanguageMenu menu)
    {
        var selected = SelectedLanguageIndex();
        if (menu.Component.TryCast<TMP_Dropdown>() is { } tmp)
        {
            if (tmp.options.Count != Catalog.Languages.Count)
                return false;
            for (var i = 0; i < Catalog.Languages.Count; i++)
                if (tmp.options[i].text != LanguageLabel(i))
                    return false;
            // Native activation may overwrite only the caption or value while our
            // options remain correct. Reconcile the displayed selection as well.
            return tmp.value == selected && (!tmp.captionText || tmp.captionText.text == LanguageLabel(selected));
        }
        if (menu.Component.TryCast<Dropdown>() is { } legacy)
        {
            if (legacy.options.Count != Catalog.Languages.Count)
                return false;
            for (var i = 0; i < Catalog.Languages.Count; i++)
                if (legacy.options[i].text != LanguageLabel(i))
                    return false;
            return legacy.value == selected && (!legacy.captionText || legacy.captionText.text == LanguageLabel(selected));
        }
        return false;
    }

    private static int SelectedLanguageIndex()
    {
        for (var index = 0; index < Catalog.Languages.Count; index++)
            if (Catalog.Languages[index].Id == Catalog.ActiveLanguageId)
                return index;
        return 0;
    }

    private static string LanguageLabel(int index)
    {
        var language = Catalog.Languages[index];
        return string.IsNullOrEmpty(language.NativeName) ? language.Name : language.NativeName;
    }

    private static void PopulateMenu(LanguageMenu menu)
    {
        UpdatingLanguageMenu = true;
        try
        {
            var names = new Il2CppSystem.Collections.Generic.List<string>();
            var selected = 0;
            for (var index = 0; index < Catalog.Languages.Count; index++)
            {
                var language = Catalog.Languages[index];
                names.Add(string.IsNullOrEmpty(language.NativeName) ? language.Name : language.NativeName);
                if (language.Id == Catalog.ActiveLanguageId)
                    selected = index;
            }
            // Replace the event object, including persistent I2 callbacks. Merely calling
            // RemoveAllListeners would leave those callbacks and select an I2 language.
            if (menu.Component.TryCast<TMP_Dropdown>() is { } tmp)
            {
                tmp.onValueChanged = new TMP_Dropdown.DropdownEvent();
                tmp.ClearOptions();
                tmp.AddOptions(names);
                tmp.SetValueWithoutNotify(selected);
                tmp.RefreshShownValue();
                tmp.interactable = true;
                tmp.onValueChanged.AddListener((UnityAction<int>)(Action<int>)OnLanguageSelected);
                AddMenuFont(tmp.captionText);
                AddMenuFont(tmp.itemText);
            }
            else if (menu.Component.TryCast<Dropdown>() is { } legacy)
            {
                legacy.onValueChanged = new Dropdown.DropdownEvent();
                legacy.ClearOptions();
                legacy.AddOptions(names);
                legacy.SetValueWithoutNotify(selected);
                legacy.RefreshShownValue();
                legacy.interactable = true;
                legacy.onValueChanged.AddListener((UnityAction<int>)(Action<int>)OnLanguageSelected);
                if (_languageMenuFont)
                {
                    if (legacy.captionText)
                        legacy.captionText.font = _languageMenuFont;
                    if (legacy.itemText)
                        legacy.itemText.font = _languageMenuFont;
                }
            }
            menu.Version = _version;
            _log.LogInfo($"Language dropdown refreshed: {Catalog.Languages.Count} options; selected {Catalog.ActiveLanguageId}.");
        }
        catch (Exception error) { Report("populate language selector", error); }
        finally { UpdatingLanguageMenu = false; }
    }

    private static void AddMenuFont(TMP_Text text)
    {
        if (!text || !text.font)
            return;
        if (Fonts.Values.Any(pair => pair.Tmp && pair.Tmp.Pointer == text.font.Pointer))
            return;
        var fallbacks = text.font.fallbackFontAssetTable;
        if (fallbacks is null)
            text.font.fallbackFontAssetTable = fallbacks = new Il2CppSystem.Collections.Generic.List<TMP_FontAsset>();
        foreach (var pair in Fonts.Values)
            if (pair.Tmp && pair.Tmp.Pointer != text.font.Pointer && !fallbacks.Contains(pair.Tmp))
                fallbacks.Add(pair.Tmp);
    }

    private static void OnLanguageSelected(int index)
    {
        if (!UpdatingLanguageMenu)
            SelectIndex(index);
    }

    internal static void HandleNativeLanguageChange(GameObject gameObject, int index)
    {
        try
        {
            if (UpdatingLanguageMenu || !gameObject)
                return;
            var tmp = gameObject.GetComponent<TMP_Dropdown>();
            var legacy = gameObject.GetComponent<Dropdown>();
            if ((tmp && Menus.ContainsKey(tmp.GetInstanceID())) || (legacy && Menus.ContainsKey(legacy.GetInstanceID())))
                SelectIndex(index);
        }
        catch (Exception error) { Report("native language callback", error); }
    }

    private static void RefreshLanguageMenus()
    {
        foreach (var entry in Menus.ToArray())
        {
            if (!entry.Value.Component)
            {
                Menus.Remove(entry.Key);
                continue;
            }
            PopulateMenu(entry.Value);
        }
    }

    private static void DiscoverLanguageMenus()
    {
        foreach (var selector in Resources.FindObjectsOfTypeAll<I2.Loc.SetLanguageDropdown>())
        {
            try
            {
                if (selector && selector.gameObject.scene.IsValid())
                    BindLanguageObject(selector.gameObject);
            }
            catch (Exception error) { Report("find I2 selector", error); }
        }
        // Some game builds use TMP selectors without the original I2 component.
        foreach (var dropdown in Resources.FindObjectsOfTypeAll<TMP_Dropdown>())
            DiscoverNamedMenu(dropdown);
        foreach (var dropdown in Resources.FindObjectsOfTypeAll<Dropdown>())
            DiscoverNamedMenu(dropdown);
        _settingsVisible = false;
        foreach (var options in Resources.FindObjectsOfTypeAll<bl_AllOptions>())
        {
            try
            {
                if (!options || !options.gameObject.scene.IsValid() || !options.gameObject.activeInHierarchy)
                    continue;
                foreach (var panel in options.Panels)
                    if (panel && panel.activeInHierarchy)
                    {
                        _settingsVisible = true;
                        break;
                    }
            }
            catch (Exception error) { Report("find settings window", error); }
        }
        foreach (var entry in Menus.ToArray())
            if (!entry.Value.Component)
                Menus.Remove(entry.Key);
    }

    private static void DiscoverNamedMenu(Component dropdown)
    {
        try
        {
            if (!dropdown || !dropdown.gameObject.scene.IsValid())
                return;
            var node = dropdown.transform;
            // Check only the row and dropdown: an entire Settings/Language panel may
            // contain unrelated controls which must retain their original callbacks.
            for (var depth = 0; depth < 2 && node; depth++, node = node.parent)
            {
                if (node.name.Contains("language", StringComparison.OrdinalIgnoreCase))
                {
                    BindMenu(dropdown);
                    return;
                }
            }
        }
        catch (Exception error) { Report("find named language selector", error); }
    }

    private static bool InsideLanguageMenu(Component component)
    {
        var tmp = ParentComponent<TMP_Dropdown>(component);
        if (tmp && Menus.ContainsKey(tmp.GetInstanceID()))
        {
            if ((tmp.captionText && tmp.captionText.Pointer == component.Pointer) ||
                (tmp.itemText && tmp.itemText.Pointer == component.Pointer))
                return true;
            var text = component.TryCast<TMP_Text>();
            return text && IsLanguageLabel(text.text);
        }
        var legacy = ParentComponent<Dropdown>(component);
        if (legacy && Menus.ContainsKey(legacy.GetInstanceID()))
        {
            if ((legacy.captionText && legacy.captionText.Pointer == component.Pointer) ||
                (legacy.itemText && legacy.itemText.Pointer == component.Pointer))
                return true;
            var text = component.TryCast<Text>();
            return text && IsLanguageLabel(text.text);
        }
        return false;
    }

    private static bool IsLanguageLabel(string value) => Catalog.Languages.Any(language =>
        value == language.NativeName || value == language.Name);

    internal static void DrawGui()
    {
        if (!_started)
            return;
        try
        {
            _buttonStyle ??= new GUIStyle(GUI.skin.button) { fontSize = 18 };
            _labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            if (_font)
            {
                _buttonStyle.font = _font;
                _labelStyle.font = _font;
            }
            // The native dropdown is preferred; only expose this extra button when none
            // could be integrated. F8 remains available from every screen in both cases.
            if (_settingsVisible && Menus.Count == 0 && GUI.Button(new Rect(Math.Max(10, Screen.width - 270), 16, 250, 36), Catalog.Translate("Language") + " [F8]", _buttonStyle))
                SetWindowVisible(!_showWindow);
            if (!_showWindow)
                return;
            var width = Math.Min(480f, Screen.width - 32f);
            var x = (Screen.width - width) / 2f;
            var y = Math.Max(16f, (Screen.height - 400f) / 2f);
            var count = Catalog.Languages.Count;
            const int perPage = 6;
            var maxPage = Math.Max(0, (count - 1) / perPage);
            _page = Math.Clamp(_page, 0, maxPage);
            var savedColor = GUI.color;
            try
            {
                GUI.color = new Color(.09f, .10f, .12f, 1f);
                GUI.DrawTexture(new Rect(x, y, width, 394), Texture2D.whiteTexture);
            }
            finally { GUI.color = savedColor; }
            GUI.Label(new Rect(x + 18, y + 12, width - 90, 32), Catalog.Translate("Language"), _labelStyle);
            if (GUI.Button(new Rect(x + width - 52, y + 10, 38, 32), "×", _buttonStyle))
                SetWindowVisible(false);
            for (var row = 0; row < perPage; row++)
            {
                var index = _page * perPage + row;
                if (index >= count)
                    break;
                var language = Catalog.Languages[index];
                var title = (language.Id == Catalog.ActiveLanguageId ? "✓ " : "") + language.NativeName;
                _buttonStyle.font = _languageMenuFont ? _languageMenuFont : _font;
                if (GUI.Button(new Rect(x + 18, y + 52 + row * 43, width - 36, 37), title, _buttonStyle))
                    SelectIndex(index);
            }
            if (_font)
                _buttonStyle.font = _font;
            if (maxPage > 0)
            {
                if (GUI.Button(new Rect(x + 18, y + 314, 50, 30), "←", _buttonStyle))
                    _page = Math.Max(0, _page - 1);
                GUI.Label(new Rect(x + 80, y + 314, width - 160, 30), $"{_page + 1} / {maxPage + 1}", _labelStyle);
                if (GUI.Button(new Rect(x + width - 68, y + 314, 50, 30), "→", _buttonStyle))
                    _page = Math.Min(maxPage, _page + 1);
            }
            if (GUI.Button(new Rect(x + 18, y + 352, width - 36, 30), Catalog.Translate("Reload packs"), _buttonStyle))
            {
                Catalog.Reload();
                foreach (var warning in Catalog.Warnings)
                    _log.LogWarning(warning);
                SelectLanguage(Plugin.Instance.SelectedLanguage.Value);
            }
        }
        catch (Exception error)
        {
            SetWindowVisible(false);
            Report("language window", error);
        }
    }
}
