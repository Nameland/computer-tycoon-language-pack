using TMPro;
using UnityEngine;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private sealed class CompactDesignLabelState
    {
        public TMP_Text Text;
        public TrackedText Record;
        public Vector2 Size;
        public Vector2 Position;
        public TextAlignmentOptions Alignment;
        public bool Mobo;
        public bool Modified;
    }

    private static readonly Dictionary<int, CompactDesignLabelState> CompactDesignLabels = new();

    private static void ApplyCompactDesignLabel(Component component, TrackedText record)
    {
        var mobo = HasContextPath(
            component,
            "MOBOPrototypeWindow",
            "Main_pl",
            "STORAGE_INFO_pl",
            "Cache_tpye_lbl (4)"
        );
        var price = HasContextPath(component, "ComputerList", "Panel", "price_tgl (1)", "Label");
        if (!mobo && !price)
        {
            return;
        }

        var text = component.TryCast<TMP_Text>();
        if (!text)
        {
            return;
        }

        var id = text.GetInstanceID();
        if (
            !CompactDesignLabels.TryGetValue(id, out var state)
            || !state.Text
            || state.Text.Pointer != text.Pointer
        )
        {
            state = new()
            {
                Text = text,
                Record = record,
                Size = text.rectTransform.sizeDelta,
                Position = text.rectTransform.anchoredPosition,
                Alignment = text.alignment,
                Mobo = mobo,
            };
            CompactDesignLabels[id] = state;
        }

        UpdateCompactDesignLabel(state);
    }

    private static void UpdateCompactDesignLabel(CompactDesignLabelState state)
    {
        var text = state.Text;
        var rect = text.rectTransform;
        if (!UsesLocalizedLayout)
        {
            if (!state.Modified)
            {
                return;
            }

            rect.sizeDelta = state.Size;
            rect.anchoredPosition = state.Position;
            text.enableAutoSizing = state.Record.OriginalAutoSize;
            text.fontSizeMin = state.Record.OriginalSizeMin;
            text.fontSizeMax = state.Record.OriginalSizeMax;
            text.fontSize = state.Record.OriginalFontSize;
            text.alignment = state.Alignment;
            text.enableWordWrapping = state.Record.OriginalWordWrap;
            text.overflowMode = state.Record.OriginalOverflow;
            state.Modified = false;
            return;
        }

        state.Modified = true;
        text.enableAutoSizing = false;
        text.fontSize = state.Mobo ? 34f : 30f;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        rect.sizeDelta = new Vector2(
            state.Size.x,
            state.Size.y + (state.Mobo ? 42f - 24.375114f : 36f - 30.011f)
        );
        // MOBO's native bottom-left pivot keeps the lower boundary clear of the
        // adjacent model-value row. The short Base header stays at its own center.
        text.alignment = state.Mobo
            ? TextAlignmentOptions.MidlineLeft
            : TextAlignmentOptions.Midline;
    }

    private static void RefreshCompactDesignLabels()
    {
        foreach (var item in CompactDesignLabels.ToArray())
        {
            if (!item.Value.Text)
            {
                CompactDesignLabels.Remove(item.Key);
                continue;
            }

            try
            {
                UpdateCompactDesignLabel(item.Value);
            }
            catch (Exception e)
            {
                Report("compact design label", e);
            }
        }
    }
}
