using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private sealed class EventLabelState
    {
        public Component Owner;
        public TrackedText Record;
        public RectTransform Rect;
        public Vector2 Size;
        public float Width;
        public bool Modified;
        public HorizontalWrapMode Horizontal;
        public VerticalWrapMode Vertical;
        public TextAnchor Alignment;
    }

    private static readonly Dictionary<int, EventLabelState> EventLabels = new();

    private static bool IsManagerReportLength(Component component)
    {
        var report = ParentComponent<CTGUI_ManagerTaskReport>(component);
        // level2: static caption TMP97825 is ManagerTaskReport/Lenght. The
        // misleading Lenght_lbl field points to TMP99700/value_lbl (the days value).
        return report
            && component.name == "Lenght"
            && component.transform.parent
            && component.transform.parent.Pointer == report.transform.Pointer;
    }

    private static bool TryTranslateEventLabel(Component component, string source, out string value)
    {
        value = null;
        if (source != "Length" || !IsManagerReportLength(component))
        {
            return false;
        }

        value = TranslateContextOrSource("Manager report: ", source);
        return true;
    }

    private static bool IsPossessionCategoryTab(Component component) =>
        component.name == "Text"
        && component.transform.parent
        && component.transform.parent.name.StartsWith(
            "CEOStuffManager_GeneratedTabButton_",
            StringComparison.Ordinal
        )
        && component.transform.parent.parent
        && component.transform.parent.parent.name == "Header"
        && ParentComponent<CTGUI_CEOStuffManagerControl>(component);

    private static void ApplyEventPresentationLayout(
        Component component,
        TrackedText record,
        string value
    )
    {
        if (!UsesLocalizedLayout || value == record.Source)
        {
            return;
        }

        var legacy = component.TryCast<Text>();
        var isTab = legacy && IsPossessionCategoryTab(component);
        var isLength =
            component.TryCast<TMP_Text>() is not null
            && record.Source == "Length"
            && IsManagerReportLength(component);
        if (!isTab && !isLength)
        {
            return;
        }

        var rect = component.transform.TryCast<RectTransform>();
        if (!rect)
        {
            return;
        }

        var id = component.GetInstanceID();
        if (
            !EventLabels.TryGetValue(id, out var state)
            || !state.Owner
            || state.Owner.Pointer != component.Pointer
        )
        {
            EventLabels[id] = state = new EventLabelState
            {
                Owner = component,
                Record = record,
                Rect = rect,
                Size = rect.sizeDelta,
                Width = rect.rect.width,
                Horizontal = legacy ? legacy.horizontalOverflow : default,
                Vertical = legacy ? legacy.verticalOverflow : default,
                Alignment = legacy ? legacy.alignment : default,
            };
        }

        state.Modified = true;
        if (isTab)
        {
            // At 1920x1080, native 40 * projection 0.308 gives ~9px glyphs.
            // 52 yields ~12px. The observed gaps between all eight caption rectangles
            // exceed the added width, so keep the native buttons and row placement.
            rect.sizeDelta = new Vector2(state.Size.x + state.Width * .4f, state.Size.y);
            legacy.resizeTextForBestFit = false;
            legacy.fontSize = Math.Max(52, (int)record.OriginalFontSize);
            legacy.horizontalOverflow = HorizontalWrapMode.Overflow;
            legacy.verticalOverflow = VerticalWrapMode.Overflow;
            legacy.alignment = TextAnchor.MiddleCenter;
        }
        else
        {
            var tmp = component.TryCast<TMP_Text>();
            tmp.enableAutoSizing = false;
            tmp.fontSize = Math.Max(22f, record.OriginalFontSize);
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
        }
    }

    private static void RefreshEventPresentationLayouts()
    {
        foreach (var pair in EventLabels.ToArray())
        {
            try
            {
                var state = pair.Value;
                if (!state.Owner || !state.Rect)
                {
                    EventLabels.Remove(pair.Key);
                    continue;
                }

                if (UsesLocalizedLayout && state.Record.Rendered != state.Record.Source)
                {
                    ApplyEventPresentationLayout(state.Owner, state.Record, state.Record.Rendered);
                    continue;
                }

                if (!state.Modified)
                {
                    continue;
                }

                state.Rect.sizeDelta = state.Size;
                if (state.Owner.TryCast<Text>() is { } legacy)
                {
                    legacy.fontSize = (int)state.Record.OriginalFontSize;
                    legacy.resizeTextForBestFit = state.Record.OriginalAutoSize;
                    legacy.resizeTextMinSize = (int)state.Record.OriginalSizeMin;
                    legacy.resizeTextMaxSize = (int)state.Record.OriginalSizeMax;
                    legacy.horizontalOverflow = state.Horizontal;
                    legacy.verticalOverflow = state.Vertical;
                    legacy.alignment = state.Alignment;
                }
                else if (state.Owner.TryCast<TMP_Text>() is { } tmp)
                {
                    tmp.fontSize = state.Record.OriginalFontSize;
                    tmp.enableAutoSizing = state.Record.OriginalAutoSize;
                    tmp.fontSizeMin = state.Record.OriginalSizeMin;
                    tmp.fontSizeMax = state.Record.OriginalSizeMax;
                    tmp.enableWordWrapping = state.Record.OriginalWordWrap;
                    tmp.overflowMode = state.Record.OriginalOverflow;
                }

                state.Modified = false;
                state.Record.FitChanged = false;
                if (Catalog.ActiveLanguageId != "en")
                {
                    ApplyShortLabelFit(state.Owner, state.Record, state.Record.Rendered);
                }
            }
            catch (Exception error)
            {
                Report("RefreshEventPresentationLayouts instance", error);
            }
        }
    }
}
