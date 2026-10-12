using TMPro;
using UnityEngine;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private static readonly HashSet<TrackedText> NegotiationsLayouts = new();
    private static readonly HashSet<string> NegotiationButtons = new(StringComparer.Ordinal)
    {
        "Task_btn", "Task_btn (2)", "Task_btn (3)", "Task_btn (4)", "Task_btn (5)",
        "Task_btn (7)", "Task_btn (8)", "Task_btn (9)", "Task_btn (10)",
        "EndNoSpyAgreement_btn", "ActivePayments_btn", "CancelActivePayments_btn",
        "EndFriendshlip_btn", "RivalryReconciliation_btn", "EndResearchAgreement_btn"
    };

    private static void ApplyNegotiationsLayout(Component component, TrackedText record)
    {
        ApplyNoSpyExplanationLayout(component, record);
        var text = component.TryCast<TMP_Text>();
        if (text is null || record.OriginalFontSize <= 0)
            return;
        var endpoint = HasContextPath(component, "CEO_StatusWindow", "RelationShipStatus_lbl (1)") ||
            HasContextPath(component, "CEO_StatusWindow", "RelationShipStatus_lbl (2)");
        var parent = component.transform.parent;
        var button = parent && NegotiationButtons.Contains(parent.name) &&
            HasContextPath(component, "CEO_NegotiationWindow", "ManagerPoolWindow_Content", parent.name, "Text");
        if (!endpoint && !button)
            return;
        if (!UsesLocalizedLayout)
        {
            if (!NegotiationsLayouts.Remove(record))
                return;
            text.enableWordWrapping = record.OriginalWordWrap;
            text.overflowMode = record.OriginalOverflow;
            text.fontSizeMin = record.OriginalSizeMin;
            text.fontSizeMax = record.OriginalSizeMax;
            text.enableAutoSizing = record.OriginalAutoSize;
            text.fontSize = record.OriginalFontSize;
            // Use ordinary fitting when this layout policy does not apply.
            if (Catalog.ActiveLanguageId != "en")
            {
                record.FitChanged = false;
                ApplyShortLabelFit(component, record, record.Rendered);
            }
            return;
        }
        NegotiationsLayouts.RemoveWhere(item => !item.Owner);
        NegotiationsLayouts.Add(record);
        // Design-space sizes follow the game's CanvasScaler. At the inspected
        // scale of 0.72, 32 units gives a consistent 23 px button caption. Preserve
        // a small autosize range for longer translations and narrower layouts.
        var maximum = endpoint ? 26f : 32f;
        var minimum = endpoint ? 22f : 28f;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.fontSizeMax = maximum;
        text.fontSizeMin = minimum;
        text.fontSize = maximum;
        text.enableAutoSizing = true;
    }
}
