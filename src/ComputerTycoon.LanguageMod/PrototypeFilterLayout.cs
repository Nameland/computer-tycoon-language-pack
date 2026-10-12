using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private sealed class PrototypeFilterState
    {
        public TrackedText Record;
        public RectTransform Rect;
        public Vector2 Size, Position;
        public float Width, Height, TargetWidth;
        public bool Modified;
    }
    private static readonly Dictionary<int, PrototypeFilterState> PrototypeFilters = new();
    private static readonly HashSet<string> PrototypeFilterNames = new(StringComparer.Ordinal) {
        "Toggle", "Toggle (1)", "Toggle (2)", "Toggle (3)", "Toggle (4)",
        "Toggle (5)", "Toggle (6)", "Toggle (7)", "Toggle (8)", "Active_tgl", "Archived_tgl (1)"
    };

    // Hook in ApplyFont after the generic short-label policy.
    private static void ApplyPrototypeFilterLayout(Component component, TrackedText record, string value)
    {
        var text = component.TryCast<TMP_Text>();
        if (text is null || record.OriginalFontSize <= 0 || component.name != "Label")
            return;
        var toggle = component.transform.parent?.TryCast<RectTransform>();
        if (!toggle || !PrototypeFilterNames.Contains(toggle.name) ||
            !HasContextPath(component, "PrototypesList", "Panel", "Panel", toggle.name, "Label"))
            return;
        var id = component.GetInstanceID();
        if (PrototypeFilters.TryGetValue(id, out var state) &&
            (!state.Record.Owner || state.Record.Owner.Pointer != component.Pointer))
        {
            PrototypeFilters.Remove(id);
            state = null;
        }
        if (state is null)
        {
            if (!UsesLocalizedLayout)
                return;
            var rect = text.rectTransform;
            var panel = toggle.parent?.TryCast<RectTransform>();
            if (!panel || Math.Abs(panel.rect.width - 236f) > 2f ||
                Math.Abs(rect.localScale.x - .5f) > .01f || Math.Abs(rect.rect.height - 40f) > 2f ||
                toggle.anchorMin.x != 0f || toggle.anchorMax.x != 0f)
                return;
            var anchor = rect.anchorMin.x + (rect.anchorMax.x - rect.anchorMin.x) * rect.pivot.x;
            var left = toggle.anchoredPosition.x +
                ((anchor - toggle.pivot.x) * toggle.rect.width + rect.anchoredPosition.x -
                 rect.rect.width * rect.pivot.x * rect.localScale.x) * toggle.localScale.x;
            var cap = (panel.rect.width - 4f - left) / (rect.localScale.x * toggle.localScale.x);
            state = new PrototypeFilterState
            {
                Record = record,
                Rect = rect,
                Size = rect.sizeDelta,
                Position = rect.anchoredPosition,
                Width = rect.rect.width,
                Height = rect.rect.height,
                TargetWidth = Math.Max(rect.rect.width, Math.Min(390f, cap))
            };
            PrototypeFilters[id] = state;
        }
        if (!UsesLocalizedLayout)
        {
            if (!state.Modified)
                return;
            state.Rect.sizeDelta = state.Size;
            state.Rect.anchoredPosition = state.Position;
            text.enableWordWrapping = record.OriginalWordWrap;
            text.overflowMode = record.OriginalOverflow;
            text.fontSizeMin = record.OriginalSizeMin;
            text.fontSizeMax = record.OriginalSizeMax;
            text.enableAutoSizing = record.OriginalAutoSize;
            text.fontSize = record.OriginalFontSize;
            state.Modified = false;
            record.FitChanged = false;
            if (Catalog.ActiveLanguageId != "en")
                ApplyShortLabelFit(component, record, value);
            return;
        }
        var growth = state.TargetWidth - state.Width;
        state.Rect.sizeDelta = new Vector2(state.Size.x + growth, state.Size.y + 56f - state.Height);
        state.Rect.anchoredPosition = new Vector2(state.Position.x + growth * state.Rect.pivot.x * state.Rect.localScale.x, state.Position.y);
        text.enableWordWrapping = false;
        // Ellipsis can suppress the complete first line when fallback font ascenders
        // exceed the box. Keep a single visible line; width stays capped to the panel.
        text.overflowMode = TextOverflowModes.Overflow;
        text.enableAutoSizing = false;
        // 36 * 0.5 local scale * ~0.65 screen scale = ~11.7 px at 1080p.
        // Glyph height is below the 24-unit row step; the taller box only gives
        // TMP ascent/descent room and does not enlarge the rendered glyphs.
        text.fontSize = 36f;
        state.Modified = true;
    }
}
