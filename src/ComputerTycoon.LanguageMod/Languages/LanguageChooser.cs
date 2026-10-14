using UnityEngine;
using UnityEngine.EventSystems;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private static bool _showWindow;
    private static GUIStyle _buttonStyle;
    private static GUIStyle _labelStyle;
    private static int _page;
    private static EventSystem _suspendedEventSystem;

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
        else
        {
            RestoreGameInput();
        }
    }

    // Keep the reference until restoration succeeds, so a transient native error
    // can be retried on the next update instead of leaving input disabled.
    private static void RestoreGameInput()
    {
        try
        {
            if (_suspendedEventSystem)
            {
                _suspendedEventSystem.enabled = true;
            }

            _suspendedEventSystem = null;
        }
        catch (Exception error)
        {
            Report("restore game input", error);
        }
    }

    internal static void CloseLanguageWindow()
    {
        _showWindow = false;
        _mouseBlockedUntil = float.NegativeInfinity;
        RestoreGameInput();
    }

    private static void ReloadLanguagePacks()
    {
        // Manual retry is the only place failed resource caches are cleared.
        // Never retry missing fonts or reread files on every frame.
        ReportedErrors.Clear();
        Fonts.RetryFailedResources();
        MapCaptions.RetryFailedResources();
        Catalog.Reload();
        foreach (var warning in Catalog.Warnings)
        {
            _log.LogWarning(warning);
        }

        SelectLanguage(Catalog.ActiveLanguageId);
    }

    internal static void DrawGui()
    {
        if (!_started || (!_showWindow && (!_settingsVisible || Menus.Count > 0)))
        {
            return;
        }

        try
        {
            _buttonStyle ??= new GUIStyle(GUI.skin.button) { fontSize = 18 };
            _labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
            _buttonStyle.font = Fonts.CurrentFont;
            _labelStyle.font = Fonts.CurrentFont;
            // The native dropdown is preferred; only expose this extra button when none
            // could be integrated. F8 remains available from every screen in both cases.
            if (
                _settingsVisible
                && Menus.Count == 0
                && GUI.Button(
                    new Rect(Math.Max(10, Screen.width - 270), 16, 250, 36),
                    Catalog.Translate("Language") + " [F8]",
                    _buttonStyle
                )
            )
            {
                SetWindowVisible(!_showWindow);
            }

            if (!_showWindow)
            {
                return;
            }

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
            finally
            {
                GUI.color = savedColor;
            }

            GUI.Label(
                new Rect(x + 18, y + 12, width - 90, 32),
                Catalog.Translate("Language"),
                _labelStyle
            );
            if (GUI.Button(new Rect(x + width - 52, y + 10, 38, 32), "×", _buttonStyle))
            {
                SetWindowVisible(false);
            }

            for (var row = 0; row < perPage; row++)
            {
                var index = _page * perPage + row;
                if (index >= count)
                {
                    break;
                }

                var language = Catalog.Languages[index];
                var title =
                    (language.Id == Catalog.ActiveLanguageId ? "✓ " : "") + language.NativeName;
                _buttonStyle.font = Fonts.MenuFont ? Fonts.MenuFont : Fonts.CurrentFont;
                if (
                    GUI.Button(
                        new Rect(x + 18, y + 52 + row * 43, width - 36, 37),
                        title,
                        _buttonStyle
                    )
                )
                {
                    SelectIndex(index);
                }
            }

            _buttonStyle.font = Fonts.CurrentFont;
            if (maxPage > 0)
            {
                if (GUI.Button(new Rect(x + 18, y + 314, 50, 30), "←", _buttonStyle))
                {
                    _page = Math.Max(0, _page - 1);
                }

                GUI.Label(
                    new Rect(x + 80, y + 314, width - 160, 30),
                    $"{_page + 1} / {maxPage + 1}",
                    _labelStyle
                );
                if (GUI.Button(new Rect(x + width - 68, y + 314, 50, 30), "→", _buttonStyle))
                {
                    _page = Math.Min(maxPage, _page + 1);
                }
            }

            if (
                GUI.Button(
                    new Rect(x + 18, y + 352, width - 36, 30),
                    Catalog.Translate("Reload packs"),
                    _buttonStyle
                )
            )
            {
                ReloadLanguagePacks();
            }
        }
        catch (Exception error)
        {
            SetWindowVisible(false);
            Report("language window", error);
        }
    }
}
