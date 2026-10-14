using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private sealed class FilterRectSnapshot
    {
        internal RectTransform Rect;
        internal Vector2 Size;
        internal Vector2 Position;
        internal Vector2 Min;
        internal Vector2 Max;
        internal Vector2 Pivot;
        internal Vector3 Scale;

        internal FilterRectSnapshot(RectTransform rect)
        {
            Rect = rect;
            Size = rect.sizeDelta;
            Position = rect.anchoredPosition;
            Min = rect.anchorMin;
            Max = rect.anchorMax;
            Pivot = rect.pivot;
            Scale = rect.localScale;
        }

        internal void Restore()
        {
            if (!Rect)
            {
                return;
            }

            SetFilterRect(Rect, Min, Max, Pivot, Size, Position, Scale);
        }
    }

    private sealed class FilterLabelState
    {
        internal TMP_Text Text;
        internal TrackedText Record;
        internal FilterRectSnapshot Rect;
        internal FilterRectSnapshot Toggle;
        internal FilterRectSnapshot Check;
        internal TextAlignmentOptions Alignment;
        internal Vector4 Margin;
    }

    private static void PlaceFilterLabel(
        FilterLabelState row,
        float x,
        float y,
        float width,
        float height,
        float font
    )
    {
        SetFilterRect(
            row.Rect.Rect,
            new Vector2(0, 1),
            new Vector2(0, 1),
            new Vector2(0, .5f),
            new Vector2(width, height),
            new Vector2(x, y),
            Vector3.one
        );
        var text = row.Text;
        // Fit each translation inside its own row instead of allowing long labels
        // to paint over the window edge. TMP reflows when the text/font changes.
        if (Math.Abs(text.fontSizeMax - font) > .01f)
        {
            text.fontSizeMax = font;
        }

        if (Math.Abs(text.fontSizeMin - font * .45f) > .01f)
        {
            text.fontSizeMin = font * .45f;
        }

        if (!text.enableAutoSizing)
        {
            text.enableAutoSizing = true;
        }

        if (text.enableWordWrapping)
        {
            text.enableWordWrapping = false;
        }

        if (text.overflowMode != TextOverflowModes.Ellipsis)
        {
            text.overflowMode = TextOverflowModes.Ellipsis;
        }

        if (text.alignment != TextAlignmentOptions.Left)
        {
            text.alignment = TextAlignmentOptions.Left;
        }

        if (text.margin != Vector4.zero)
        {
            text.margin = Vector4.zero;
        }
    }

    private static void SetFilterRect(
        RectTransform rect,
        Vector2 min,
        Vector2 max,
        Vector2 pivot,
        Vector2 size,
        Vector2 position,
        Vector3 scale
    )
    {
        if (rect.anchorMin != min)
        {
            rect.anchorMin = min;
        }

        if (rect.anchorMax != max)
        {
            rect.anchorMax = max;
        }

        if (rect.pivot != pivot)
        {
            rect.pivot = pivot;
        }

        if (rect.sizeDelta != size)
        {
            rect.sizeDelta = size;
        }

        if (rect.anchoredPosition != position)
        {
            rect.anchoredPosition = position;
        }

        if (rect.localScale != scale)
        {
            rect.localScale = scale;
        }
    }

    private static readonly string[] ComputerFilterRows =
    {
        "ComputerList_All_Status_Filter_Tgl",
        "DesignToggle",
        "PrototypeToggle",
        "OnMarketToggle",
        "ArchivedToggle",
        "ComputerList_All_Design_Filter_Tgl",
        "CPU_Proto_Architecture_HC_tgl",
        "CPU_Proto_Architecture_PC_tgl",
        "CPU_Proto_Architecture_Laptop_tgl",
        "CPU_Proto_Architecture_Tablet_tgl",
    };
    private static readonly string[] HardwareFilterRows =
    {
        "Active_tgl",
        "Archived_tgl (1)",
        "Fill_All_Hardware_Tgl",
        "Toggle",
        "Toggle (1)",
        "Toggle (2)",
        "Toggle (3)",
        "Toggle (4)",
        "Toggle (5)",
        "Toggle (6)",
        "Toggle (7)",
        "Toggle (8)",
        "Fill_All_Design_Tgl",
        "CPU_Proto_Architecture_HC_tgl",
        "CPU_Proto_Architecture_PC_tgl",
        "CPU_Proto_Architecture_Laptop_tgl",
        "CPU_Proto_Architecture_Tablet_tgl",
    };

    private sealed class ProductFilterState
    {
        internal FilterRectSnapshot Panel;
        internal FilterLabelState Heading;
        internal List<FilterLabelState> Rows = new();
        internal bool Hardware;
        internal bool Modified;
    }

    private static readonly Dictionary<int, ProductFilterState> ProductFilters = new();

    private static void ApplyProductFiltersLayout(Component component)
    {
        var node = component.transform;
        while (node && node.name != "ComputerList" && node.name != "PrototypesList")
        {
            node = node.parent;
        }

        if (!node || node.parent?.name != "UI Screens")
        {
            return;
        }

        var panel = node.Find("Panel/Panel")?.TryCast<RectTransform>();
        if (!panel || !component.transform.IsChildOf(panel))
        {
            return;
        }

        var id = panel.GetInstanceID();
        if (
            ProductFilters.TryGetValue(id, out var stale)
            && (!stale.Panel.Rect || stale.Panel.Rect.Pointer != panel.Pointer)
        )
        {
            ProductFilters.Remove(id);
        }

        if (!ProductFilters.TryGetValue(id, out var state))
        {
            if (!UsesLocalizedLayout)
            {
                return;
            }

            var hardware = node.name == "PrototypesList";
            if (Math.Abs(panel.rect.width - (hardware ? 236f : 217.94f)) > 2f)
            {
                return;
            }

            state = new ProductFilterState { Panel = new(panel), Hardware = hardware };
            foreach (var name in hardware ? HardwareFilterRows : ComputerFilterRows)
            {
                var toggle = panel.Find(name)?.TryCast<RectTransform>();
                var text = toggle
                    ?.Find("Label")
                    ?.GetComponent(Il2CppType.Of<TMP_Text>())
                    ?.TryCast<TMP_Text>();
                var check = toggle?.Find("Background")?.TryCast<RectTransform>();
                if (
                    !toggle
                    || !text
                    || !check
                    || !Texts.TryGetValue(text.GetInstanceID(), out var record)
                )
                {
                    return;
                }

                state.Rows.Add(
                    new FilterLabelState
                    {
                        Text = text,
                        Record = record,
                        Rect = new(text.rectTransform),
                        Toggle = new(toggle),
                        Check = new(check),
                        Alignment = text.alignment,
                        Margin = text.margin,
                    }
                );
            }

            if (!hardware)
            {
                var title = panel
                    .Find("Filter_lbl")
                    ?.GetComponent(Il2CppType.Of<TMP_Text>())
                    ?.TryCast<TMP_Text>();
                if (!title || !Texts.TryGetValue(title.GetInstanceID(), out var record))
                {
                    return;
                }

                state.Heading = new FilterLabelState
                {
                    Text = title,
                    Record = record,
                    Rect = new(title.rectTransform),
                    Alignment = title.alignment,
                    Margin = title.margin,
                };
            }

            ProductFilters[id] = state;
        }

        UpdateProductFilters(state);
    }

    private static void RefreshProductFiltersLayouts()
    {
        RefreshTrackedStates(
            ProductFilters,
            static state =>
                state.Panel.Rect
                && state.Rows.All(row =>
                    row.Text && row.Rect.Rect && row.Toggle.Rect && row.Check.Rect
                )
                && (state.Heading is null || state.Heading.Text),
            UpdateProductFilters,
            nameof(RefreshProductFiltersLayouts)
        );
    }

    private static void UpdateProductFilters(ProductFilterState state)
    {
        if (!UsesLocalizedLayout)
        {
            if (!state.Modified)
            {
                return;
            }

            foreach (
                var row in state.Heading is null ? state.Rows : state.Rows.Append(state.Heading)
            )
            {
                row.Rect.Restore();
                row.Toggle?.Restore();
                row.Check?.Restore();
                if (!row.Text)
                {
                    continue;
                }

                var record = row.Record;
                var text = row.Text;
                text.enableAutoSizing = record.OriginalAutoSize;
                text.fontSizeMin = record.OriginalSizeMin;
                text.fontSizeMax = record.OriginalSizeMax;
                text.fontSize = record.OriginalFontSize;
                text.enableWordWrapping = record.OriginalWordWrap;
                text.overflowMode = record.OriginalOverflow;
                text.alignment = row.Alignment;
                text.margin = row.Margin;
                record.FitChanged = false;
                if (Catalog.ActiveLanguageId != "en")
                {
                    ApplyShortLabelFit(text, record, record.Rendered);
                }
            }

            state.Modified = false;
            return;
        }

        var panel = state.Panel.Rect;
        if (!panel.gameObject.activeInHierarchy)
        {
            return;
        }

        // Work in panel-local units. Screen-pixel sizing cancelled the user's
        // window scale, so shrinking the window left its filters too large.
        // Leave a small bottom inset and keep both dimensions inside the native
        // panel. Its frame and neighbouring action buttons remain untouched.
        var requiredHeight = state.Hardware ? 312.5f : 208f;
        var unit = Math.Min(panel.rect.width / 196f, (panel.rect.height - 4f) / requiredHeight);
        if (!float.IsFinite(unit) || unit <= 0f)
        {
            return;
        }

        state.Modified = true;
        var y = state.Hardware ? 7f : 29f;
        if (state.Heading is not null)
        {
            PlaceFilterLabel(
                state.Heading,
                8f * unit,
                -13f * unit,
                180f * unit,
                22f * unit,
                18f * unit
            );
        }

        for (var index = 0; index < state.Rows.Count; index++)
        {
            if (state.Hardware ? index is 2 or 12 : index == 5)
            {
                y += 4f;
            }

            var row = state.Rows[index];
            SetFilterRect(
                row.Toggle.Rect,
                new Vector2(0, 1),
                new Vector2(0, 1),
                new Vector2(0, 1),
                new Vector2(196f * unit, 17.5f * unit),
                new Vector2(0, -y * unit),
                Vector3.one
            );
            SetFilterRect(
                row.Check.Rect,
                new Vector2(0, .5f),
                new Vector2(0, .5f),
                new Vector2(0, .5f),
                new Vector2(12f * unit, 12f * unit),
                new Vector2(8f * unit, 0),
                Vector3.one
            );
            PlaceFilterLabel(row, 28f * unit, -8.75f * unit, 162f * unit, 17.5f * unit, 17f * unit);
            y += 17.5f;
        }
    }
}
