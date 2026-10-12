using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ComputerTycoon.Localization;

namespace ComputerTycoon.LanguageMod;

[BepInPlugin(Id, "Computer Tycoon Language Packs", Version)]
public sealed class Plugin : BasePlugin
{
    public const string Id = "computer.tycoon.languagepacks";
    public const string Version = "0.1.0";
    internal static Plugin Instance { get; private set; } = null!;
    private Harmony _harmony;
    internal ConfigEntry<string> SelectedLanguage { get; private set; } = null!;
    internal string PluginDirectory { get; private set; } = null!;

    public override void Load()
    {
        Instance = this;
        SelectedLanguage = Config.Bind("General", "Language", "en", "Local language pack ID; en restores the original English text.");
        PluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var directory = Path.Combine(PluginDirectory, "Languages");
        var catalog = new LanguageCatalog(directory);
        Runtime.Initialize(catalog, Log);
        foreach (var warning in catalog.Warnings)
            Log.LogWarning(warning);
        _harmony = new Harmony(Id);
        Patch(typeof(Input), "GetMouseButton", nameof(Hooks.MouseButtonPrefix), new[] { typeof(int) });
        Patch(typeof(Input), "GetMouseButtonDown", nameof(Hooks.MouseButtonPrefix), new[] { typeof(int) });
        Patch(typeof(Input), "GetMouseButtonUp", nameof(Hooks.MouseButtonPrefix), new[] { typeof(int) });
        // Native callers can inline Input queries. Stop the actual interaction handlers too.
        Patch(typeof(EventSystem), "Update", nameof(Hooks.ModalInteractionPrefix));
        Patch(typeof(StandaloneInputModule), "Process", nameof(Hooks.ModalInteractionPrefix));
        Patch(typeof(Button), "Press", nameof(Hooks.ModalInteractionPrefix));
        Patch(typeof(Dropdown), "OnPointerClick", nameof(Hooks.ModalInteractionPrefix));
        Patch(typeof(TMP_Dropdown), "OnPointerClick", nameof(Hooks.ModalInteractionPrefix));
        foreach (var method in new[] { "OnPointerDown", "OnPointerUp", "OnPointerClick", "OnBeginDrag", "OnDrag", "OnEndDrag" })
            Patch(typeof(EventTrigger), method, nameof(Hooks.ModalInteractionPrefix));
        Patch(typeof(CTGUI_ResearchNodeControl), "onNodeClick", nameof(Hooks.ModalInteractionPrefix));
        Patch(typeof(CTGUIResearchControl), "techOnClick", nameof(Hooks.ModalInteractionPrefix));
        Patch(typeof(CT_GUI_ResearchTreeDrag), "OnMouseDrag", nameof(Hooks.ModalInteractionPrefix));
        Patch(typeof(WorldMapStrategyKit.WMSK), "PerformUserInteraction", nameof(Hooks.ModalInteractionPrefix));
        Patch(typeof(WorldMapStrategyKit.WMSK), "DoOnMouseClick", nameof(Hooks.ModalInteractionPrefix));
        Patch(typeof(WorldMapStrategyKit.WMSK), "DoOnMouseRelease", nameof(Hooks.ModalInteractionPrefix));
        Patch(typeof(VfLabelManager), "OnGUI", nameof(Hooks.ModalInteractionPrefix));
        Patch(typeof(VfLabelManager), "OnGUI", nameof(Hooks.TooltipPrefix));
        Patch(typeof(VfLabelManager), "UpdateStyle", nameof(Hooks.TooltipStylePostfix), postfix: true);
        Patch(typeof(VfCursorTarget), "LateShow", nameof(Hooks.ModalInteractionPrefix));
        Patch(typeof(TMP_Text), "set_text", nameof(Hooks.TextPrefix));
        Patch(typeof(WorldMapStrategyKit.WMSK), "DrawTextMeshProLabels", nameof(Hooks.CountryCaptionsPrefix));
        Patch(typeof(WorldMapStrategyKit.WMSK), "DrawTextMeshProLabels", nameof(Hooks.CountryCaptionsFinalizer), finalizer: true);
        Patch(typeof(Text), "set_text", nameof(Hooks.LegacyTextPrefix));
        Patch(typeof(TextMeshProUGUI), "OnEnable", nameof(Hooks.TextEnabledPostfix), postfix: true);
        Patch(typeof(TextMeshPro), "OnEnable", nameof(Hooks.TextEnabledPostfix), postfix: true);
        Patch(typeof(Text), "OnEnable", nameof(Hooks.TextEnabledPostfix), postfix: true);
        // Native formatted, StringBuilder and SetCharArray paths are handled by the scan
        // after formatting, so a format template is never stored as the visible original.
        Patch(typeof(TMP_Text), "SetText", nameof(Hooks.SetTextPrefix), new[] { typeof(string), typeof(bool) });
        Patch(typeof(TMP_Text), "SetText", nameof(Hooks.SetTextPrefix), new[] { typeof(string) });
        Patch(typeof(I2.Loc.LocalizationManager), "set_CurrentLanguage", nameof(Hooks.EnglishPrefix));
        Patch(typeof(I2.Loc.LocalizationManager), "set_CurrentLanguageCode", nameof(Hooks.EnglishCodePrefix));
        Patch(typeof(I2.Loc.LocalizationManager), "SetLanguageAndCode", nameof(Hooks.EnglishPairPrefix));
        Patch(typeof(I2.Loc.SetLanguageDropdown), "OnValueChanged", nameof(Hooks.LanguageValuePrefix));
        // Replace I2 initialization: otherwise it can select index 0 while rebuilding its
        // English-only list and overwrite a previously saved language selection.
        Patch(typeof(I2.Loc.SetLanguageDropdown), "Start", nameof(Hooks.LanguageStartPrefix));
        Patch(typeof(Dropdown), "Show", nameof(Hooks.LanguageMenuOpenPrefix), Type.EmptyTypes);
        Patch(typeof(TMP_Dropdown), "Show", nameof(Hooks.LanguageMenuOpenPrefix), Type.EmptyTypes);
        AddComponent<LanguageBehaviour>();
        Log.LogInfo($"Language packs ready: {Runtime.Catalog.Languages.Count} packs in {directory}. F8 opens the language chooser.");
    }

    private void Patch(Type type, string method, string hook, Type[] arguments = null, bool postfix = false, bool finalizer = false)
    {
        try
        {
            var target = arguments is null ? AccessTools.Method(type, method) : AccessTools.Method(type, method, arguments);
            if (target is null)
                throw new MissingMethodException(type.FullName, method);
            var patch = new HarmonyMethod(typeof(Hooks), hook);
            _harmony!.Patch(target, prefix: postfix || finalizer ? null : patch,
                postfix: postfix ? patch : null, finalizer: finalizer ? patch : null);
        }
        catch (Exception error)
        {
            Log.LogWarning($"Could not hook {type.Name}.{method}; periodic UI refresh remains available: {error.Message}");
        }
    }
}

internal static class Hooks
{
    public static void CountryCaptionsPrefix(WorldMapStrategyKit.WMSK __instance) => Runtime.PrepareCountryCaptions(__instance);
    public static void CountryCaptionsFinalizer() => Runtime.FinishCountryCaptions();
    public static void LanguageMenuOpenPrefix(Component __instance) => Runtime.PrepareLanguageMenu(__instance);
    public static bool ModalInteractionPrefix() => !Runtime.BlockGameMouse;
    public static void TooltipPrefix(VfLabelManager __instance) => Runtime.PrepareTooltip(__instance);
    public static void TooltipStylePostfix(VfLabelManager __instance) => Runtime.ApplyTooltipFont(__instance);
    public static bool MouseButtonPrefix(ref bool __result)
    {
        if (!Runtime.BlockGameMouse)
            return true;
        __result = false;
        return false;
    }
    public static void TextEnabledPostfix(Component __instance) => Runtime.TextEnabled(__instance);
    public static void TextPrefix(TMP_Text __instance, ref string __0) => Runtime.Intercept(__instance, ref __0);
    public static void SetTextPrefix(TMP_Text __instance, ref string __0) => Runtime.Intercept(__instance, ref __0);
    public static void LegacyTextPrefix(Text __instance, ref string __0) => Runtime.Intercept(__instance, ref __0);
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

public sealed class LanguageBehaviour : MonoBehaviour
{
    public LanguageBehaviour(IntPtr pointer) : base(pointer) { }
    public void Update() => Runtime.Update();
    public void OnGUI() => Runtime.DrawGui();
}
