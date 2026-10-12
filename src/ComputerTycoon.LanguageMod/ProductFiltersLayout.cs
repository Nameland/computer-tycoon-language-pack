using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private static readonly string[] ComputerFilterRows = {
        "ComputerList_All_Status_Filter_Tgl", "DesignToggle", "PrototypeToggle", "OnMarketToggle", "ArchivedToggle",
        "ComputerList_All_Design_Filter_Tgl", "CPU_Proto_Architecture_HC_tgl", "CPU_Proto_Architecture_PC_tgl",
        "CPU_Proto_Architecture_Laptop_tgl", "CPU_Proto_Architecture_Tablet_tgl"
    };
    private static readonly string[] HardwareFilterRows = {
        "Active_tgl", "Archived_tgl (1)", "Fill_All_Hardware_Tgl", "Toggle", "Toggle (1)", "Toggle (2)",
        "Toggle (3)", "Toggle (4)", "Toggle (5)", "Toggle (6)", "Toggle (7)", "Toggle (8)",
        "Fill_All_Design_Tgl", "CPU_Proto_Architecture_HC_tgl", "CPU_Proto_Architecture_PC_tgl",
        "CPU_Proto_Architecture_Laptop_tgl", "CPU_Proto_Architecture_Tablet_tgl"
    };
    private sealed class ProductFilterState
    {
        internal CountryFilterRect Panel, Frame;
        internal CountryFilterLabel Heading;
        internal List<CountryFilterLabel> Rows = new();
        internal bool Hardware, Modified;
    }
    private static readonly Dictionary<int, ProductFilterState> ProductFilters = new();

    private static void ApplyProductFiltersLayout(Component component)
    {
        var node = component.transform;
        while (node && node.name != "ComputerList" && node.name != "PrototypesList")
            node = node.parent;
        if (!node || node.parent?.name != "UI Screens")
            return;
        var panel = node.Find("Panel/Panel")?.TryCast<RectTransform>();
        if (!panel || !component.transform.IsChildOf(panel))
            return;
        var id = panel.GetInstanceID();
        if (ProductFilters.TryGetValue(id, out var stale) && (!stale.Panel.Rect || stale.Panel.Rect.Pointer != panel.Pointer))
            ProductFilters.Remove(id);
        if (!ProductFilters.TryGetValue(id, out var state))
        {
            if (!UsesLocalizedLayout)
                return;
            var frame = node.Find("Background")?.TryCast<RectTransform>();
            var hardware = node.name == "PrototypesList";
            if (!frame || frame.childCount != 0 || Math.Abs(panel.rect.width - (hardware ? 236f : 217.94f)) > 2f)
                return;
            state = new ProductFilterState { Panel = new(panel), Frame = new(frame), Hardware = hardware };
            foreach (var name in hardware ? HardwareFilterRows : ComputerFilterRows)
            {
                var toggle = panel.Find(name)?.TryCast<RectTransform>();
                var text = toggle?.Find("Label")?.GetComponent(Il2CppType.Of<TMP_Text>())?.TryCast<TMP_Text>();
                var check = toggle?.Find("Background")?.TryCast<RectTransform>();
                if (!toggle || !text || !check || !Texts.TryGetValue(text.GetInstanceID(), out var record))
                    return;
                state.Rows.Add(new CountryFilterLabel
                {
                    Text = text,
                    Record = record,
                    Rect = new(text.rectTransform),
                    Toggle = new(toggle),
                    Check = new(check),
                    Alignment = text.alignment,
                    Margin = text.margin
                });
            }
            if (!hardware)
            {
                var title = panel.Find("Filter_lbl")?.GetComponent(Il2CppType.Of<TMP_Text>())?.TryCast<TMP_Text>();
                if (!title || !Texts.TryGetValue(title.GetInstanceID(), out var record))
                    return;
                state.Heading = new CountryFilterLabel
                {
                    Text = title,
                    Record = record,
                    Rect = new(title.rectTransform),
                    Alignment = title.alignment,
                    Margin = title.margin
                };
            }
            ProductFilters[id] = state;
        }
        UpdateProductFilters(state);
    }

    private static void RefreshProductFiltersLayouts()
    {
        foreach (var pair in ProductFilters.ToArray())
        {
            if (!pair.Value.Panel.Rect || !pair.Value.Frame.Rect)
            {
                ProductFilters.Remove(pair.Key);
                continue;
            }
            try
            {
                UpdateProductFilters(pair.Value);
            }
            catch (Exception error) { Report("product filters layout", error); }
        }
    }

    private static void UpdateProductFilters(ProductFilterState state)
    {
        if (!UsesLocalizedLayout)
        {
            if (!state.Modified)
                return;
            state.Panel.Restore();
            state.Frame.Restore();
            foreach (var row in state.Heading is null ? state.Rows : state.Rows.Append(state.Heading))
            {
                row.Rect.Restore();
                row.Toggle?.Restore();
                row.Check?.Restore();
                if (!row.Text)
                    continue;
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
                    ApplyShortLabelFit(text, record, record.Rendered);
            }
            state.Modified = false;
            return;
        }
        var panel = state.Panel.Rect;
        if (!panel.gameObject.activeInHierarchy)
            return;
        var camera = ResearchDetailsCamera(panel);
        var zero = RectTransformUtility.WorldToScreenPoint(camera, panel.TransformPoint(Vector3.zero));
        var unit = RectTransformUtility.WorldToScreenPoint(camera, panel.TransformPoint(Vector3.up));
        var projection = Vector2.Distance(zero, unit);
        if (!float.IsFinite(projection) || projection < .01f)
            return;
        var px = (Screen.height / 1080f) / projection;
        // The native 17 hardware rows occupy 317 screen pixels. Reuse their
        // unused upper margin and group gaps; neither the table nor action buttons move.
        // 17px em gives about 12.4..16.3px translated ink, instead of 7.6..9.8px.
        var extra = Math.Max(0, 196f * px - state.Panel.Width);
        state.Panel.ExpandRight(extra);
        state.Frame.ExpandRight(extra); // Independent childless Image; preserve the enclosing blue frame.
        var y = state.Hardware ? 7f : 29f;
        if (state.Heading is not null)
            PlaceCountryFilterLabel(state.Heading, 8f * px, -13f * px, 180f * px, 22f * px, 18f * px);
        for (var index = 0; index < state.Rows.Count; index++)
        {
            if (state.Hardware ? index is 2 or 12 : index == 5)
                y += 4f;
            var row = state.Rows[index];
            Put(row.Toggle.Rect, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(196f * px, 17.5f * px), new Vector2(0, -y * px), Vector3.one);
            Put(row.Check.Rect, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f),
                new Vector2(12f * px, 12f * px), new Vector2(8f * px, 0), Vector3.one);
            PlaceCountryFilterLabel(row, 28f * px, -8.75f * px, 162f * px, 17.5f * px, 17f * px);
            y += 17.5f;
        }
        state.Modified = true;
    }
}
