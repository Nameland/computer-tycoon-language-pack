using TMPro;
using UnityEngine;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private sealed class NoSpyExplanationState
    {
        internal TrackedText Record;
        internal RectTransform Rect;
        internal Vector2 Size;
        internal Vector2 Position;
        internal bool Modified;
    }

    private static readonly Dictionary<int, NoSpyExplanationState> NoSpyExplanations = new();

    private static void ApplyNoSpyExplanationLayout(Component component, TrackedText record)
    {
        var text = component.TryCast<TMP_Text>();
        if (
            !text
            || !HasContextPath(
                component,
                "CEO_NoSpyAgreementOfferWindow",
                "GameObject",
                "Reaction02_lbl (1)"
            )
        )
        {
            return;
        }

        var id = text.GetInstanceID();
        if (
            !NoSpyExplanations.TryGetValue(id, out var state)
            || !state.Record.Owner
            || state.Record.Owner.Pointer != component.Pointer
        )
        {
            if (!UsesLocalizedLayout)
            {
                return;
            }

            state = new NoSpyExplanationState
            {
                Record = record,
                Rect = text.rectTransform,
                Size = text.rectTransform.sizeDelta,
                Position = text.rectTransform.anchoredPosition,
            };
            NoSpyExplanations[id] = state;
        }

        if (!UsesLocalizedLayout)
        {
            if (!state.Modified)
            {
                return;
            }

            state.Rect.sizeDelta = state.Size;
            state.Rect.anchoredPosition = state.Position;
            text.enableAutoSizing = record.OriginalAutoSize;
            text.fontSize = record.OriginalFontSize;
            text.fontSizeMin = record.OriginalSizeMin;
            text.fontSizeMax = record.OriginalSizeMax;
            text.enableWordWrapping = record.OriginalWordWrap;
            text.overflowMode = record.OriginalOverflow;
            state.Modified = false;
            record.FitChanged = false;
            if (Catalog.ActiveLanguageId != "en")
            {
                ApplyShortLabelFit(component, record, record.Rendered);
            }

            return;
        }

        // Keep the top edge while widening the explanation inside its column.
        // These design-space dimensions leave clearance before the neighbouring
        // reaction column and the action button below.
        var width = 230f;
        var height = 124f;
        var grow = height - state.Size.y;
        state.Modified = true;
        state.Rect.sizeDelta = new Vector2(width, height);
        state.Rect.anchoredPosition = new Vector2(
            state.Position.x,
            state.Position.y - grow * (1f - state.Rect.pivot.y)
        );
        text.enableAutoSizing = false;
        text.fontSize = 25f;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
    }
}
