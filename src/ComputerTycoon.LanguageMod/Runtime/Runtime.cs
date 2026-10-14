using BepInEx.Logging;
using ComputerTycoon.Localization;
using UnityEngine;

namespace ComputerTycoon.LanguageMod;

/// <summary>Presentation-only translations. No game models, saved data or input values are modified.</summary>
internal static partial class Runtime
{
    // Reuse the translated-text layout for each installed language pack.
    private static bool UsesLocalizedLayout => Catalog.ActiveLanguageId != "en";

    private static readonly HashSet<string> ReportedErrors = new();
    private static ManualLogSource _log = null!;
    private static bool _writing;
    private static bool _started;
    private static bool _needsRefresh;
    private static float _nextScan;
    private static float _nextDiscovery;
    private static int _version;
    internal static LanguageCatalog Catalog { get; private set; } = null!;

    internal static FontProvider Fonts { get; private set; } = null!;
    internal static MapCaptionLocalizer MapCaptions { get; private set; } = null!;

    internal static void Initialize(LanguageCatalog catalog, ManualLogSource log)
    {
        Catalog = catalog;
        _log = log;
        Fonts = new FontProvider(catalog, log, Report);
        MapCaptions = new MapCaptionLocalizer(catalog, Fonts, Report);
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
                {
                    SetWindowVisible(!_showWindow);
                }
            }
            catch (Exception error)
            {
                Report("F8 keyboard shortcut", error);
            }

            var now = Time.unscaledTime;
            if (now >= _nextDiscovery)
            {
                _nextDiscovery = now + 2f;
                EnsureEnglish();
                if (!_showWindow)
                {
                    RestoreGameInput();
                }

                RunRefresh(DiscoverLanguageMenus);
                RunRefresh(DiscoverTexts);
                RunRefresh(CleanupDestroyedObjects);
            }

            if (_needsRefresh || now >= _nextScan)
            {
                _nextScan = now + .25f;
                var includeInactive = _needsRefresh;
                // Consume the request before running UI work. If a layout fails,
                // retries still respect the interval instead of running every frame.
                _needsRefresh = false;
                RunRefresh(MapCaptions.RefreshIfRequested);
                try
                {
                    RefreshTexts(includeInactive);
                }
                catch (Exception error)
                {
                    Report("refresh texts", error);
                }

                RefreshPresentationLayouts();
                RunRefresh(RefreshBakedTextLocalization);
                RunRefresh(RefreshMapPreviewLocalization);
            }
        }
        catch (Exception error)
        {
            Report("update", error);
        }
    }

    private static void EnsureEnglish()
    {
        try
        {
            // Every package receives the same keys regardless of OS language or old I2 prefs.
            I2.Loc.LocalizationManager.EnableChangingCultureInfo(false);
            if (I2.Loc.LocalizationManager.CurrentLanguage != "English")
            {
                I2.Loc.LocalizationManager.SetLanguageAndCode("English", "en", false, true);
            }
        }
        catch (Exception error)
        {
            Report("I2 English source language", error);
        }
    }

    internal static void SelectIndex(int index)
    {
        if (index >= 0 && index < Catalog.Languages.Count)
        {
            SelectLanguage(Catalog.Languages[index].Id);
        }
    }

    internal static void SelectLanguage(string id)
    {
        try
        {
            if (
                !Catalog.Languages.Any(language =>
                    string.Equals(language.Id, id, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                id = "en";
            }

            var previousWarnings = Catalog.Warnings.Count;
            Catalog.SelectLanguage(id);
            for (var index = previousWarnings; index < Catalog.Warnings.Count; index++)
            {
                _log.LogWarning(Catalog.Warnings[index]);
            }

            Fonts.PrepareFont();
            Fonts.PrepareMenuFont();
            _version++;
            _needsRefresh = true;
            MapCaptions.RequestRefresh();
            RunRefresh(RefreshLanguageMenus);
            try
            {
                // ConfigEntry may save immediately from its setter. A read-only
                // config must not prevent the selected pack from taking effect.
                Plugin.Instance.SelectedLanguage.Value = Catalog.ActiveLanguageId;
                if (!Plugin.Instance.Config.SaveOnConfigSet)
                {
                    Plugin.Instance.Config.Save();
                }
            }
            catch (Exception error)
            {
                Report("save selected language", error);
            }

            _log.LogInfo(
                $"Selected language '{Catalog.ActiveLanguageId}'; refreshing {Texts.Count} text components from their original English values."
            );
        }
        catch (Exception error)
        {
            Report("select language " + id, error);
        }
    }

    internal static void Report(string context, Exception error)
    {
        var key = context + ":" + error.GetType().Name + ":" + error.Message;
        if (ReportedErrors.Count < 128 && ReportedErrors.Add(key))
        {
            _log.LogWarning($"Language mod ({context}): {error}");
        }
    }
}
