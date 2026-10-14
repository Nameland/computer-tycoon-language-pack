using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private static void ApplyFont(Component component, TrackedText record, string value)
    {
        ApplyShortLabelFit(component, record, value);
        ApplyScreenLayout(component, record, value);
        ApplyOfficeActionLayout(component, record, value);
        ApplyManagerRowLayout(component, record, value);
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
            {
                return;
            }

            if (
                !Fonts.CurrentTmpFont
                || !tmp.font
                || tmp.font.Pointer == Fonts.CurrentTmpFont.Pointer
            )
            {
                return;
            }

            // Owned fonts are assigned directly by the artwork/loading handlers.
            // Linking them to each other would create cycles after language changes.
            if (Fonts.Owns(tmp.font))
            {
                return;
            }

            var fallbacks = tmp.font.fallbackFontAssetTable;
            if (fallbacks is null)
            {
                tmp.font.fallbackFontAssetTable = fallbacks =
                    new Il2CppSystem.Collections.Generic.List<TMP_FontAsset>();
            }

            if (!fallbacks.Contains(Fonts.CurrentTmpFont))
            {
                fallbacks.Add(Fonts.CurrentTmpFont);
            }
        }
        else if (component.TryCast<Text>() is { } legacy)
        {
            var desired =
                Catalog.ActiveLanguageId != "en"
                && value.Any(character => character > 127)
                && Fonts.CurrentFont
                    ? Fonts.CurrentFont
                    : record.OriginalFont;
            if (desired && (!legacy.font || legacy.font.Pointer != desired.Pointer))
            {
                legacy.font = desired;
            }
        }
    }

    private static void ApplyShortLabelFit(Component component, TrackedText record, string value)
    {
        if (record.OriginalFontSize <= 0)
        {
            return; // Menu font-only call, without a tracked layout.
        }

        var tmp = component.TryCast<TMP_Text>();
        var legacy = tmp is null ? component.TryCast<Text>() : null;
        var rect = tmp is not null ? tmp.rectTransform : legacy?.rectTransform;
        var shouldFit =
            !IsCountryFilterText(component)
            && Catalog.ActiveLanguageId != "en"
            && value != record.Source
            && record.Source.Length <= 100
            && !record.Source.Contains('\n')
            && !record.Source.Contains("<br", StringComparison.OrdinalIgnoreCase)
            && rect
            && rect.rect.height > 0
            && rect.rect.height <= record.OriginalFontSize * 2.2f;
        if (shouldFit)
        {
            if (record.FitChanged && !record.FitNeedsRetry)
            {
                return;
            }

            // Record the partial change before native setters. Retry until all
            // settings succeed, or restore the original settings on English.
            record.FitChanged = true;
            record.FitNeedsRetry = true;
            if (tmp is not null)
            {
                tmp.fontSizeMax = record.OriginalAutoSize
                    ? record.OriginalSizeMax
                    : record.OriginalFontSize;
                tmp.fontSizeMin = record.OriginalAutoSize
                    ? record.OriginalSizeMin
                    : record.OriginalFontSize * .45f;
                tmp.enableAutoSizing = true;
                // Preserve multiline layouts in research nodes and compatibility lists.
                // Auto-sizing alone must never turn a wrapped label into an overflowing line.
                tmp.enableWordWrapping = record.OriginalWordWrap;
                tmp.overflowMode = record.OriginalAutoSize
                    ? record.OriginalOverflow
                    : TextOverflowModes.Ellipsis;
            }
            else if (legacy is not null)
            {
                legacy.resizeTextMaxSize = record.OriginalAutoSize
                    ? (int)record.OriginalSizeMax
                    : (int)record.OriginalFontSize;
                legacy.resizeTextMinSize = record.OriginalAutoSize
                    ? (int)record.OriginalSizeMin
                    : Math.Max(1, (int)Math.Ceiling(record.OriginalFontSize * .45f));
                legacy.resizeTextForBestFit = true;
            }

            record.FitNeedsRetry = false;
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
            record.FitNeedsRetry = false;
        }
    }
}
