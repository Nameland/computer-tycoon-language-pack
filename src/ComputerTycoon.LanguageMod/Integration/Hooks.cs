using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ComputerTycoon.LanguageMod;

internal static class Hooks
{
    public static bool ResearchSelectionPrefix(
        CTGUIResearchControl __instance,
        GameDataEditor.GDETechData __0
    )
    {
        try
        {
            return __0 is not null
                || __instance.ScreenType != CTGUIResearchControl.ScreenTypeE.ResearchTradeScreen
                || __instance.selectedResearch is not null;
        }
        catch (Exception error)
        {
            Runtime.Report("check trade technology selection", error);
            return true;
        }
    }

    public static void CountryCaptionsPrefix(WorldMapStrategyKit.WMSK __instance)
    {
        try
        {
            Runtime.MapCaptions.PrepareCountryCaptions(__instance);
        }
        catch (Exception error)
        {
            // Even a failed rollback must not escape into the game's draw loop.
            // The paired finalizer still releases the translation guard.
            Runtime.Report("prepare map captions hook", error);
        }
    }

    public static void CountryCaptionsFinalizer() => Runtime.MapCaptions.FinishCountryCaptions();

    public static void CountryRenamedPostfix(
        WorldMapStrategyKit.WMSK __instance,
        string __0,
        string __1,
        bool __result
    )
    {
        if (!__result)
        {
            return;
        }

        try
        {
            Runtime.MapCaptions.CountryRenamed(__instance, __0, __1);
        }
        catch (Exception error)
        {
            Runtime.Report("refresh renamed country caption", error);
        }
    }

    public static void LanguageMenuOpenPrefix(Component __instance) =>
        Runtime.PrepareLanguageMenu(__instance);

    public static void LanguageControlEnabledPostfix(Selectable __instance)
    {
        try
        {
            Runtime.LanguageControlEnabled(__instance);
        }
        catch (Exception error)
        {
            Runtime.Report("enable language selector", error);
        }
    }

    public static void SettingsUpdatedPostfix() => Runtime.QueueLanguageMenuRefresh();

    public static bool ModalInteractionPrefix() => !Runtime.BlockGameMouse;

    public static void TooltipPrefix(VfLabelManager __instance) =>
        Runtime.PrepareTooltip(__instance);

    public static void TooltipStylePostfix(VfLabelManager __instance) =>
        Runtime.ApplyTooltipFont(__instance);

    public static bool MouseButtonPrefix(ref bool __result)
    {
        if (!Runtime.BlockGameMouse)
        {
            return true;
        }

        __result = false;
        return false;
    }

    public static void TextEnabledPostfix(Component __instance) => Runtime.TextEnabled(__instance);

    public static void TextPrefix(TMP_Text __instance, ref string __0) =>
        Runtime.Intercept(__instance, ref __0);

    public static void SetTextPrefix(TMP_Text __instance, ref string __0) =>
        Runtime.Intercept(__instance, ref __0);

    public static void LegacyTextPrefix(Text __instance, ref string __0) =>
        Runtime.Intercept(__instance, ref __0);

    public static void EnglishPrefix(ref string __0) => __0 = "English";

    public static void EnglishCodePrefix(ref string __0) => __0 = "en";

    public static void EnglishPairPrefix(ref string __0, ref string __1)
    {
        __0 = "English";
        __1 = "en";
    }

    public static bool LanguageValuePrefix(I2.Loc.SetLanguageDropdown __instance, int __0)
    {
        Runtime.HandleNativeLanguageChange(__instance.gameObject, __0);
        return false;
    }

    public static bool LanguageStartPrefix(I2.Loc.SetLanguageDropdown __instance)
    {
        Runtime.BindLanguageObject(__instance.gameObject);
        return false;
    }
}
