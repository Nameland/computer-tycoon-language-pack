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
    private static bool _languageMenusNeedRefresh;
    internal static bool UpdatingLanguageMenu { get; private set; }

    private static bool _settingsVisible;

    internal static void BindLanguageObject(GameObject gameObject)
    {
        try
        {
            if (!gameObject || !gameObject.scene.IsValid())
            {
                return;
            }

            var tmp = gameObject.GetComponent<TMP_Dropdown>();
            var legacy = gameObject.GetComponent<Dropdown>();
            if (tmp)
            {
                BindMenu(tmp);
            }

            if (legacy)
            {
                BindMenu(legacy);
            }
        }
        catch (Exception error)
        {
            Report("bind language selector", error);
        }
    }

    internal static void PrepareLanguageMenu(Component component)
    {
        try
        {
            if (UpdatingLanguageMenu || !component || !component.gameObject.scene.IsValid())
            {
                return;
            }

            // Reconcile immediately before Unity clones the options into its popup.
            // A periodic refresh alone is too late for an already opened native list.
            if (
                Menus.ContainsKey(component.GetInstanceID())
                || component.gameObject.GetComponent<I2.Loc.SetLanguageDropdown>()
            )
            {
                BindMenu(component);
            }
            else
            {
                DiscoverNamedMenu(component);
            }
        }
        catch (Exception error)
        {
            Report("open language selector", error);
        }
    }

    internal static void LanguageControlEnabled(Selectable control)
    {
        // Both dropdown implementations inherit Selectable.OnEnable. Ignore buttons,
        // sliders and inputs; only language dropdowns are registered by PrepareLanguageMenu.
        var dropdown = (Component)control.TryCast<TMP_Dropdown>() ?? control.TryCast<Dropdown>();
        if (!dropdown)
        {
            return;
        }

        PrepareLanguageMenu(dropdown);
        if (Menus.ContainsKey(dropdown.GetInstanceID()))
        {
            QueueLanguageMenuRefresh();
        }
    }

    internal static void QueueLanguageMenuRefresh() => _languageMenusNeedRefresh = true;

    internal static void FinishLanguageMenuActivation()
    {
        if (!_languageMenusNeedRefresh || !_started || UpdatingLanguageMenu)
        {
            return;
        }

        _languageMenusNeedRefresh = false;
        // Settings and I2 can overwrite the caption after a child's OnEnable.
        // Reconcile once after activation completes, before this frame is rendered.
        // Do not scan the scene or rebuild matching menus on every frame.
        foreach (var menu in Menus.Values.ToArray())
        {
            try
            {
                if (menu.Component && menu.Component.gameObject.activeInHierarchy)
                {
                    BindMenu(menu.Component);
                }
            }
            catch (Exception error)
            {
                Report("activate language selector", error);
            }
        }
    }

    private static void BindMenu(Component component)
    {
        var id = component.GetInstanceID();
        if (
            !Menus.TryGetValue(id, out var menu)
            || !menu.Component
            || menu.Component.Pointer != component.Pointer
        )
        {
            menu = new LanguageMenu { Component = component };
            Menus[id] = menu;
            _log.LogInfo(
                $"Using the game's language dropdown: {component.gameObject.name} ({component.GetIl2CppType().FullName})."
            );
        }

        if (menu.Version != _version || !MenuMatches(menu))
        {
            PopulateMenu(menu);
        }
    }

    private static bool MenuMatches(LanguageMenu menu)
    {
        var selected = SelectedLanguageIndex();
        if (menu.Component.TryCast<TMP_Dropdown>() is { } tmp)
        {
            if (tmp.options.Count != Catalog.Languages.Count)
            {
                return false;
            }

            for (var i = 0; i < Catalog.Languages.Count; i++)
            {
                if (tmp.options[i].text != LanguageLabel(i))
                {
                    return false;
                }
            }

            // Native activation may overwrite only the caption or value while our
            // options remain correct. Reconcile the displayed selection as well.
            return tmp.value == selected
                && (!tmp.captionText || tmp.captionText.text == LanguageLabel(selected));
        }

        if (menu.Component.TryCast<Dropdown>() is { } legacy)
        {
            if (legacy.options.Count != Catalog.Languages.Count)
            {
                return false;
            }

            for (var i = 0; i < Catalog.Languages.Count; i++)
            {
                if (legacy.options[i].text != LanguageLabel(i))
                {
                    return false;
                }
            }

            return legacy.value == selected
                && (!legacy.captionText || legacy.captionText.text == LanguageLabel(selected));
        }

        return false;
    }

    private static int SelectedLanguageIndex()
    {
        for (var index = 0; index < Catalog.Languages.Count; index++)
        {
            if (Catalog.Languages[index].Id == Catalog.ActiveLanguageId)
            {
                return index;
            }
        }

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
                names.Add(
                    string.IsNullOrEmpty(language.NativeName) ? language.Name : language.NativeName
                );
                if (language.Id == Catalog.ActiveLanguageId)
                {
                    selected = index;
                }
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
                Fonts.AddMenuFallbacks(tmp.captionText);
                Fonts.AddMenuFallbacks(tmp.itemText);
            }
            else if (menu.Component.TryCast<Dropdown>() is { } legacy)
            {
                legacy.onValueChanged = new Dropdown.DropdownEvent();
                legacy.ClearOptions();
                legacy.AddOptions(names);
                legacy.SetValueWithoutNotify(selected);
                legacy.RefreshShownValue();
                legacy.interactable = true;
                legacy.onValueChanged.AddListener(
                    (UnityAction<int>)(Action<int>)OnLanguageSelected
                );
                if (Fonts.MenuFont)
                {
                    if (legacy.captionText)
                    {
                        legacy.captionText.font = Fonts.MenuFont;
                    }

                    if (legacy.itemText)
                    {
                        legacy.itemText.font = Fonts.MenuFont;
                    }
                }
            }

            menu.Version = _version;
            _log.LogInfo(
                $"Language dropdown refreshed: {Catalog.Languages.Count} options; selected {Catalog.ActiveLanguageId}."
            );
        }
        catch (Exception error)
        {
            Report("populate language selector", error);
        }
        finally
        {
            UpdatingLanguageMenu = false;
        }
    }

    private static void OnLanguageSelected(int index)
    {
        if (!UpdatingLanguageMenu)
        {
            SelectIndex(index);
        }
    }

    internal static void HandleNativeLanguageChange(GameObject gameObject, int index)
    {
        try
        {
            if (UpdatingLanguageMenu || !gameObject)
            {
                return;
            }

            var tmp = gameObject.GetComponent<TMP_Dropdown>();
            var legacy = gameObject.GetComponent<Dropdown>();
            if (
                (tmp && Menus.ContainsKey(tmp.GetInstanceID()))
                || (legacy && Menus.ContainsKey(legacy.GetInstanceID()))
            )
            {
                SelectIndex(index);
            }
        }
        catch (Exception error)
        {
            Report("native language callback", error);
        }
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
                {
                    BindLanguageObject(selector.gameObject);
                }
            }
            catch (Exception error)
            {
                Report("find I2 selector", error);
            }
        }

        // Some game builds use TMP selectors without the original I2 component.
        foreach (var dropdown in Resources.FindObjectsOfTypeAll<TMP_Dropdown>())
        {
            DiscoverNamedMenu(dropdown);
        }

        foreach (var dropdown in Resources.FindObjectsOfTypeAll<Dropdown>())
        {
            DiscoverNamedMenu(dropdown);
        }

        _settingsVisible = false;
        foreach (var options in Resources.FindObjectsOfTypeAll<bl_AllOptions>())
        {
            try
            {
                if (
                    !options
                    || !options.gameObject.scene.IsValid()
                    || !options.gameObject.activeInHierarchy
                )
                {
                    continue;
                }

                foreach (var panel in options.Panels)
                {
                    if (panel && panel.activeInHierarchy)
                    {
                        _settingsVisible = true;
                        break;
                    }
                }
            }
            catch (Exception error)
            {
                Report("find settings window", error);
            }
        }

        foreach (var entry in Menus.ToArray())
        {
            if (!entry.Value.Component)
            {
                Menus.Remove(entry.Key);
            }
        }
    }

    private static void DiscoverNamedMenu(Component dropdown)
    {
        try
        {
            if (!dropdown || !dropdown.gameObject.scene.IsValid())
            {
                return;
            }

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
        catch (Exception error)
        {
            Report("find named language selector", error);
        }
    }

    private static bool InsideLanguageMenu(Component component)
    {
        var tmp = ParentComponent<TMP_Dropdown>(component);
        if (tmp && Menus.ContainsKey(tmp.GetInstanceID()))
        {
            if (
                (tmp.captionText && tmp.captionText.Pointer == component.Pointer)
                || (tmp.itemText && tmp.itemText.Pointer == component.Pointer)
            )
            {
                return true;
            }

            var text = component.TryCast<TMP_Text>();
            return text && IsLanguageLabel(text.text);
        }

        var legacy = ParentComponent<Dropdown>(component);
        if (legacy && Menus.ContainsKey(legacy.GetInstanceID()))
        {
            if (
                (legacy.captionText && legacy.captionText.Pointer == component.Pointer)
                || (legacy.itemText && legacy.itemText.Pointer == component.Pointer)
            )
            {
                return true;
            }

            var text = component.TryCast<Text>();
            return text && IsLanguageLabel(text.text);
        }

        return false;
    }

    private static bool IsLanguageLabel(string value) =>
        Catalog.Languages.Any(language => value == language.NativeName || value == language.Name);
}
