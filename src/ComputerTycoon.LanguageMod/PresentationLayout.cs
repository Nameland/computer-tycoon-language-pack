using TMPro;
using UnityEngine;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private static bool ApplyLoadingPresentationFont(TMP_Text text, TrackedText record)
    {
        // These are the two actual loading labels in each scene. In particular,
        // LoadGameMenu and the ordinary Load button must retain their usual font.
        record.IsLoadingPresentation ??= IsLoadingPresentation(text);
        if (record.IsLoadingPresentation != true)
            return false;
        // Keep the static BASIC banner's original pixel metrics and retro face.
        // Its embedded blank lines position the memory line above dynamic status.
        var staticBanner = record.Source.Contains("COMMANDOR", StringComparison.Ordinal);
        if (Catalog.ActiveLanguageId == "en" || staticBanner)
        {
            if (record.PresentationFontChanged)
            {
                // Assigning font can itself select a material, so restore the exact
                // original shared material afterwards (including any custom preset).
                text.font = record.OriginalTmpFont;
                text.fontSharedMaterial = record.OriginalSharedMaterial;
                record.PresentationFontChanged = false;
            }
            return !staticBanner;
        }
        // Reuse the established localization font for the whole string. Do not
        // create another OS face or manipulate any font engine state here.
        var desired = _tmpFont;
        if (!desired)
            return false; // Keep the normal translated fallback if unavailable.
        // Set this first so that a failing material assignment still permits EN restore.
        record.PresentationFontChanged = true;
        if (!text.font || text.font.Pointer != desired.Pointer)
            text.font = desired;
        var material = desired.material;
        if (material && (!text.fontSharedMaterial || text.fontSharedMaterial.Pointer != material.Pointer))
            text.fontSharedMaterial = material;
        return true;
    }

    private static bool IsLoadingPresentation(Component component)
    {
        var path = Hierarchy(component);
        return path is
            "DoozyUI/UI Container_expand/LoadingWindow/Image (1)/Text" or
            "DoozyUI/UI Container_expand/LoadingWindow/Image (1)/Text (1)" or
            "DoozyUI/UI FULL Screen /LoadingWindow/Image (1)/Text" or
            "DoozyUI/UI FULL Screen /LoadingWindow/Image (1)/Text (1)";
    }

}
