using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private enum HardwareLabelKind
    {
        None,
        TechnologyName,
        ShowAll,
        AddAll,
    }

    private sealed class HardwareTechnologyLabelState
    {
        public TMP_Text Text;
        public TrackedText Record;
        public RectTransform Parent;
        public Vector2 Size;
        public Vector2 Position;
        public Vector2 ParentSize;
        public Vector2 ParentPosition;
        public TextAlignmentOptions Alignment;
        public HardwareLabelKind Kind;
        public bool Modified;
        public float Height = 116f;
    }

    private static readonly Dictionary<int, HardwareTechnologyLabelState> HardwareTechnologyLabels =
        new();

    private static void ApplyHardwareTechnologyLabelLayout(Component component, TrackedText record)
    {
        var text = component.TryCast<TMP_Text>();
        if (!text || !text.transform.parent)
        {
            return;
        }

        var path = Hierarchy(component);
        var kind = HardwareLabelKind.None;
        if (
            path.Contains("PrototypeWindow/Scroll View/Viewport/Content/", StringComparison.Ordinal)
            && text.transform.parent.name == "ProtoTechListButtonPrefab(Clone)"
            && text.name == "Text (TMP)"
            && text.rectTransform.rect.width > 250f
            && Math.Abs(text.rectTransform.localScale.x - 1f) < .01f
        )
        {
            kind = HardwareLabelKind.TechnologyName;
        }
        else if (
            path.EndsWith(
                "PrototypeWindow/Scroll View/Show_Outdated_Tech_tgl/Label",
                StringComparison.Ordinal
            )
        )
        {
            kind = HardwareLabelKind.ShowAll;
        }
        else if (IsCpuAddAllButton(component))
        {
            kind = HardwareLabelKind.AddAll;
        }

        if (kind == HardwareLabelKind.None)
        {
            return;
        }

        var rect = text.rectTransform;
        var parent = text.transform.parent.TryCast<RectTransform>();
        if (!parent)
        {
            return;
        }

        if (kind == HardwareLabelKind.TechnologyName)
        {
            var container = parent.parent;
            if (!container || !container.GetComponent(Il2CppType.Of<VerticalLayoutGroup>()))
            {
                return;
            }
        }

        var id = text.GetInstanceID();
        if (
            !HardwareTechnologyLabels.TryGetValue(id, out var state)
            || !state.Text
            || state.Text.Pointer != text.Pointer
        )
        {
            state = new HardwareTechnologyLabelState
            {
                Text = text,
                Record = record,
                Parent = parent,
                Size = rect.sizeDelta,
                Position = rect.anchoredPosition,
                ParentSize = parent.sizeDelta,
                ParentPosition = parent.anchoredPosition,
                Alignment = text.alignment,
                Kind = kind,
            };
            HardwareTechnologyLabels[id] = state;
        }

        UpdateHardwareTechnologyLabel(state);
    }

    private static bool UpdateHardwareTechnologyLabel(HardwareTechnologyLabelState state)
    {
        var text = state.Text;
        var parent = state.Parent;
        if (!text || !parent || state.Record is null)
        {
            return false;
        }

        var rect = text.rectTransform;
        if (!rect || !rect.parent || rect.parent.Pointer != parent.Pointer)
        {
            return false;
        }

        // Scene changes can detach a still-live row before destroying it.
        // Resolve its current container once; never rebuild a missing parent.
        RectTransform container = null;
        if (state.Kind == HardwareLabelKind.TechnologyName)
        {
            var currentParent = parent.parent;
            if (!currentParent)
            {
                return false;
            }

            container = currentParent.TryCast<RectTransform>();
            if (!container)
            {
                return false;
            }
        }

        if (!UsesLocalizedLayout)
        {
            if (!state.Modified)
            {
                return true;
            }

            rect.sizeDelta = state.Size;
            rect.anchoredPosition = state.Position;
            parent.sizeDelta = state.ParentSize;
            parent.anchoredPosition = state.ParentPosition;
            text.alignment = state.Alignment;
            text.enableAutoSizing = state.Record.OriginalAutoSize;
            text.fontSizeMin = state.Record.OriginalSizeMin;
            text.fontSizeMax = state.Record.OriginalSizeMax;
            text.fontSize = state.Record.OriginalFontSize;
            text.enableWordWrapping = state.Record.OriginalWordWrap;
            text.overflowMode = state.Record.OriginalOverflow;
            state.Modified = false;
            if (state.Kind == HardwareLabelKind.TechnologyName)
            {
                LayoutRebuilder.MarkLayoutForRebuild(container);
            }

            return true;
        }

        if (!text.gameObject.activeInHierarchy)
        {
            return true;
        }

        state.Modified = true;
        // Native editor projection at1080 is .588px/local unit. Arial30 gives
        // about12.8px capital ink; unlike generic autosize it cannot shrink away.
        var fontChanged = text.enableAutoSizing || Math.Abs(text.fontSize - 30f) > .01f;
        if (text.enableAutoSizing)
        {
            text.enableAutoSizing = false;
        }

        if (Math.Abs(text.fontSize - 30f) > .01f)
        {
            text.fontSize = 30f;
        }

        text.overflowMode = TextOverflowModes.Overflow;
        if (state.Kind == HardwareLabelKind.TechnologyName)
        {
            text.enableWordWrapping = true;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            // Read the actual existing renderer cache only. Start with three-line
            // room; after normal rendering, fit its line count. No preferred-size
            // getter or forced mesh generation touches TMP's live parser.
            var info = text.m_textInfo;
            if (
                !fontChanged
                && !text.havePropertiesChanged
                && info is not null
                && info.lineCount > 0
            )
            {
                state.Height = Math.Max(48f, info.lineCount * 36f + 8f);
            }

            var height = state.Height;
            if (Math.Abs(parent.rect.height - height) > .01f)
            {
                parent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                LayoutRebuilder.MarkLayoutForRebuild(container);
            }

            if (Math.Abs(rect.rect.height - (height - 8f)) > .01f)
            {
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height - 8f);
            }
        }
        else
        {
            text.enableWordWrapping = false;
            parent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 42f);
            if (state.Kind == HardwareLabelKind.ShowAll)
            {
                rect.sizeDelta = new Vector2(state.Size.x, -4f);
                rect.anchoredPosition = new Vector2(state.Position.x, 0f);
                text.alignment = TextAlignmentOptions.MidlineLeft;
            }
            else
            {
                parent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 100f);
                parent.anchoredPosition = new Vector2(
                    state.ParentPosition.x - (100f - state.ParentSize.x) * .5f,
                    state.ParentPosition.y
                );
                rect.sizeDelta = state.Size;
                text.alignment = TextAlignmentOptions.Midline;
            }
        }

        return true;
    }

    private static void RefreshHardwareTechnologyLabelLayouts()
    {
        foreach (var item in HardwareTechnologyLabels.ToArray())
        {
            try
            {
                if (!UpdateHardwareTechnologyLabel(item.Value))
                {
                    HardwareTechnologyLabels.Remove(item.Key);
                }
            }
            catch (Exception error)
            {
                // Text callbacks can register a healthy row again. Do not keep
                // retrying an invalid native object on every maintenance pass.
                HardwareTechnologyLabels.Remove(item.Key);
                Report("hardware technology labels", error);
            }
        }
    }
}
