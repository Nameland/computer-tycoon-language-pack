using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private sealed class ScreenLayoutState
    {
        public TrackedText Record;
        public RectTransform Rect;
        public Vector2 OriginalSizeDelta;
        public Vector2 OriginalPosition;
        public float OriginalWidth;
        public string ValueSibling;
        public float WidthFactor;
        public bool ColumnLabel;
        public Vector4 OriginalMargin;
        public bool Modified;
    }

    private static readonly Dictionary<int, ScreenLayoutState> ScreenLayouts = new();

    // Call after ApplyShortLabelFit, so this narrow policy wins over generic fitting.
    // Original text/format comes from TrackedText, not a snapshot of generic translated-text fitting.
    private static void ApplyScreenLayout(Component component, TrackedText record, string value)
    {
        ApplyStatisticsLayout(component, record, value);
        ApplyManagerTaskHeaderLayout(component, record, value);

        ApplyProductFiltersLayout(component);
        ApplyHardwareTechnologyLabelLayout(component, record);
        ApplyCompactDesignLabel(component, record);
        var tmp = component.TryCast<TMP_Text>();
        if (tmp is null || record.OriginalFontSize <= 0)
            return;
        var id = component.GetInstanceID();
        if (ScreenLayouts.TryGetValue(id, out var state) &&
            (!state.Record.Owner || state.Record.Owner.Pointer != component.Pointer))
        {
            ScreenLayouts.Remove(id);
            state = null;
        }
        var apply = UsesLocalizedLayout && value != record.Source;
        if (state is null)
        {
            if (!apply || !TryScreenLayoutRule(component, out var sibling, out var factor))
                return;
            var rect = tmp.rectTransform;
            if (!rect || rect.rect.width <= 0)
                return;
            state = new ScreenLayoutState
            {
                Record = record,
                Rect = rect,
                OriginalSizeDelta = rect.sizeDelta,
                OriginalPosition = rect.anchoredPosition,
                OriginalWidth = rect.rect.width,
                ValueSibling = sibling,
                WidthFactor = factor,
                ColumnLabel = IsCompanySelectorTechLabel(component),
                OriginalMargin = tmp.margin
            };
            ScreenLayouts[id] = state;
        }
        if (!apply)
        {
            var wasModified = state.Modified;
            RestoreScreenLayout(tmp, state);
            if (wasModified && Catalog.ActiveLanguageId != "en")
            {
                // Switching between packs still receives generic fitting.
                record.FitChanged = false;
                ApplyShortLabelFit(component, record, value);
            }
            return;
        }

        state.Modified = true;
        var targetWidth = state.OriginalWidth;
        if (state.ValueSibling is not null)
            targetWidth = ScreenLabelWidth(state);
        var delta = targetWidth - state.OriginalWidth;
        state.Rect.sizeDelta = new Vector2(state.OriginalSizeDelta.x + delta, state.OriginalSizeDelta.y);
        // Expanding about the original pivot would move the left edge into another column.
        state.Rect.anchoredPosition = new Vector2(
            state.OriginalPosition.x + delta * state.Rect.pivot.x * state.Rect.localScale.x, state.OriginalPosition.y);
        if (state.ColumnLabel)
            tmp.margin = new Vector4(state.OriginalMargin.x + 6f, state.OriginalMargin.y,
                state.OriginalMargin.z + 6f, state.OriginalMargin.w);
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.fontSizeMax = record.OriginalAutoSize ? record.OriginalSizeMax : record.OriginalFontSize;
        tmp.fontSizeMin = Math.Min(tmp.fontSizeMax, Math.Max(14f, record.OriginalFontSize * .75f));
        tmp.enableAutoSizing = true;
    }

    private static float ScreenLabelWidth(ScreenLayoutState state)
    {
        var rect = state.Rect;
        if (!rect.parent)
            return state.OriginalWidth;
        var valueNode = rect.parent.Find(state.ValueSibling);
        if (!valueNode)
            return state.OriginalWidth;
        var valueText = valueNode.GetComponent(Il2CppType.Of<TMP_Text>())?.TryCast<TMP_Text>();
        // Only borrow unused space inside a right-aligned numeric field. A future
        // left/center-aligned field or a changed hierarchy keeps its original width.
        if (valueText is null || valueText.horizontalAlignment != HorizontalAlignmentOptions.Right)
            return state.OriginalWidth;
        var valueRect = valueText.rectTransform;
        if (!valueRect || rect.localScale.x <= 0f || valueRect.localScale.x <= 0f ||
            Quaternion.Angle(rect.localRotation, Quaternion.identity) > .01f ||
            Quaternion.Angle(valueRect.localRotation, Quaternion.identity) > .01f)
            return state.OriginalWidth;
        // These shipped labels and values have scale.x=0.8914286. Compare their
        // glyph edges in the common parent's coordinates, then convert the free
        // width back to the label's local units.
        var labelScale = rect.localScale.x;
        var valueScale = valueRect.localScale.x;
        // Read only the already-rendered mesh cache. TMP preferred-size getters
        // can alter live parsing/rendering state in this game's interop build.
        // Wait for the normal renderer after a value change instead of measuring it.
        if (valueText.havePropertiesChanged)
            return state.OriginalWidth;
        var info = valueText.m_textInfo;
        var characters = info?.characterInfo;
        if (characters is null || info.characterCount <= 0 || info.characterCount > characters.Length ||
            info.characterCount > 512)
            return state.OriginalWidth;
        var minX = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        for (var index = 0; index < info.characterCount; index++)
        {
            var character = characters[index];
            if (!character.isVisible)
                continue;
            minX = Math.Min(minX, character.bottomLeft.x);
            maxX = Math.Max(maxX, character.topRight.x);
        }
        if (!float.IsFinite(minX) || !float.IsFinite(maxX) || maxX <= minX)
            return state.OriginalWidth;
        var labelLeft = rect.localPosition.x + rect.rect.xMin * labelScale;
        var valueLeft = valueRect.localPosition.x + minX * valueScale;
        var available = (valueLeft - 12f - labelLeft) / labelScale;
        return Math.Max(state.OriginalWidth, Math.Min(state.OriginalWidth * state.WidthFactor, available));
    }

    private static void RestoreScreenLayout(TMP_Text tmp, ScreenLayoutState state)
    {
        if (!state.Modified)
            return;
        state.Rect.sizeDelta = state.OriginalSizeDelta;
        state.Rect.anchoredPosition = state.OriginalPosition;
        var record = state.Record;
        if (state.ColumnLabel)
            tmp.margin = state.OriginalMargin;
        tmp.enableWordWrapping = record.OriginalWordWrap;
        tmp.overflowMode = record.OriginalOverflow;
        tmp.fontSizeMin = record.OriginalSizeMin;
        tmp.fontSizeMax = record.OriginalSizeMax;
        tmp.enableAutoSizing = record.OriginalAutoSize;
        tmp.fontSize = record.OriginalFontSize;
        state.Modified = false;
    }

    // Call once at the end of RefreshTexts, including passes where unchanged labels
    // were skipped. Numeric values can grow without changing their label's text.
    private static void RefreshScreenLayouts()
    {
        RefreshStatisticsLayouts();
        RefreshManagerTaskHeaders();

        RefreshProductFiltersLayouts();
        RefreshHardwareTechnologyLabelLayouts();
        RefreshCompactDesignLabels();
        foreach (var item in ScreenLayouts.ToArray())
        {
            var state = item.Value;
            if (!state.Record.Owner || !state.Rect)
            {
                ScreenLayouts.Remove(item.Key);
                continue;
            }
            if (!state.Record.Owner.gameObject.activeInHierarchy && UsesLocalizedLayout)
                continue;
            try
            {
                ApplyScreenLayout(state.Record.Owner, state.Record, state.Record.Rendered);
            }
            catch (Exception error) { Report("screen layout", error); }
        }
    }

    private static bool TryScreenLayoutRule(Component component, out string sibling, out float factor)
    {
        sibling = null;
        factor = 1f;
        var name = component.name;
        // Market detail rows have no space for a second line. Keep these two
        // captions inside their original rectangles, without moving the window.
        if (HasContextPath(component, "CountryDetailsWindow", "upper_box", "Population_lbl (1)") ||
            HasContextPath(component, "CountryDetailsWindow", "GovFee_pl", "Text"))
            return true;
        // PC Marketing's lower-right labels. These exact names and both window
        // variants were verified in level2; unrelated Market Size labels are excluded.
        if (name is "Market_Size_lbl" or "Market_Size_lbl (2)" or "Market_Size_lbl (3)" or
            "Market_Size_lbl (6)" or "Market_Size_lbl (1)")
        {
            if (HasContextPath(component, "ProductMarketingWindow (1)", "Panel", name) ||
                HasContextPath(component, "ProductMarketingWindow", "Panel", name))
            {
                sibling = name.Replace("_lbl", "_value");
                factor = 1.8f;
                return true;
            }
        }
        // PC Release. Do not enlarge the value fields or the entire left column.
        if (name is "Market_Size_lbl (18)" or "Market_Size_lbl (1)" or "Market_Size_lbl (2)")
        {
            if (HasContextPath(component, "ProductFeedbackWindow", "Panel", name))
            {
                sibling = name == "Market_Size_lbl (18)" ? "LastDaysResellerCut_value (15)" :
                    name.Replace("_lbl", "_value");
                factor = 1.5f;
                return true;
            }
        }
        return IsCompanySelectorTechLabel(component);
    }

    private static bool IsCpuAddAllButton(Component component)
    {
        // Nine verified hardware editors share this exact action. Do not shorten
        // unrelated ADD ALL labels or user-entered names.
        foreach (var window in new[] { "CPUPrototypeWindow", "GPUPrototypeWindow", "MOBOPrototypeWindow",
            "MEMORYPrototypeWindow", "POWERPrototypeWindow", "STORAGEPrototypeWindow",
            "INPUTPrototypeWindow", "DISPLAYPrototypeWindow", "OSPrototypeWindow" })
            if (HasContextPath(component, window, "Scroll View", "AddAllTechToHardware_btn", "Text (TMP)"))
                return true;
        return false;
    }

    // Invoke inside TranslateForComponent's non-English branch. This optional key
    // keeps the shorter wording contextual and available to other language packs.
    private static bool TryTranslateScreenLabel(Component component, string source, out string value)
    {
        value = null;
        if (source == "Base" && HasContextPath(component, "ComputerList", "Panel", "price_tgl (1)", "Label"))
        {
            value = TranslateContextOrSource("Computer list column: ", source);
            return true;
        }
        // Under the Expenses heading, repeating "cost" makes small budget rows
        // shrink unnecessarily. These optional keys apply only to budget labels.
        if (ParentComponent<CTGUIStatisticsScreen>(component) && component.transform.parent &&
            component.transform.parent.name.StartsWith("BudgetElement", StringComparison.Ordinal) &&
            source is "Factory Cost" or "Logistic Cost" or "Marketing Cost" or "Office Cost" or "Recreation Cost" or "HC" or "PC")
        {
            value = TranslateContextOrSource("Budget row: ", source);
            return true;
        }
        if ((source is "Feature" or "Heat") &&
            HasContextPath(component, "ResearchDetailsViewDoozy", "ResearchDetailsView", component.name))
        {
            value = TranslateContextOrSource("Research detail: ", source);
            return true;
        }
        if (source != "ADD ALL" || !IsCpuAddAllButton(component))
            return false;
        value = TranslateContextOrSource("Hardware add-all button: ", source);
        return true;
    }
}
