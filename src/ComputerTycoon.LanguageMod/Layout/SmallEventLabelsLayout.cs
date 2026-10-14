using TMPro;
using UnityEngine;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private enum SmallEventLabelKind
    {
        None,
        FeaturePoints,
        PublishingRisk,
        NotificationHint,
    }

    private sealed class SmallEventLabelState
    {
        public TMP_Text Text;
        public TrackedText Record;
        public RectTransform Rect;
        public RectTransform Value;
        public RectTransform Background;
        public RectTransform Body;
        public Vector2 Size;
        public Vector2 Position;
        public Vector2 ValuePosition;
        public Vector2 BackgroundSize;
        public Vector2 BodyPosition;
        public float Width;
        public float Height;
        public SmallEventLabelKind Kind;
        public bool Modified;
    }

    private static readonly Dictionary<int, SmallEventLabelState> SmallEventLabels = new();

    private static void ApplySmallEventLabelLayout(Component component, TrackedText record)
    {
        var text = component.TryCast<TMP_Text>();
        if (!text || !text.gameObject.scene.IsValid())
        {
            return;
        }

        var kind = SmallEventLabelKind.None;
        var publishing = ParentComponent<CTGUI_GamePublishingControl>(component);
        if (
            publishing
            && component.transform.parent
            && component.transform.parent.Pointer == publishing.transform.Pointer
        )
        {
            if (component.name == "potential_feature")
            {
                kind = SmallEventLabelKind.FeaturePoints;
            }
            else if (component.name == "risk_freedom_lbl")
            {
                kind = SmallEventLabelKind.PublishingRisk;
            }
        }

        if (
            record.Source == "Click On It"
            && (
                HasContextPath(
                    component,
                    "mainmenu_old_notificationRed(Clone)",
                    "NotificationContainer",
                    "Background",
                    "Title (1)"
                )
                || HasContextPath(
                    component,
                    "mainmenu_old_notificationBlue(Clone)",
                    "NotificationContainer",
                    "Background",
                    "Title (1)"
                )
            )
        )
        {
            kind = SmallEventLabelKind.NotificationHint;
        }

        if (kind == SmallEventLabelKind.None)
        {
            return;
        }

        var id = component.GetInstanceID();
        if (
            !SmallEventLabels.TryGetValue(id, out var state)
            || !state.Text
            || state.Text.Pointer != text.Pointer
        )
        {
            if (!UsesLocalizedLayout)
            {
                return;
            }

            var rect = text.rectTransform;
            state = new SmallEventLabelState
            {
                Text = text,
                Record = record,
                Rect = rect,
                Size = rect.sizeDelta,
                Position = rect.anchoredPosition,
                Width = rect.rect.width,
                Height = rect.rect.height,
                Kind = kind,
            };
            if (kind == SmallEventLabelKind.FeaturePoints)
            {
                state.Value = rect.Find("value_lbl")?.TryCast<RectTransform>();
                if (
                    !state.Value
                    || state.Value.anchorMin.y != 1
                    || state.Value.anchorMax.y != 1
                    || rect.pivot.y != .5f
                )
                {
                    return;
                }

                state.ValuePosition = state.Value.anchoredPosition;
            }

            if (kind == SmallEventLabelKind.NotificationHint)
            {
                state.Background = rect.parent.TryCast<RectTransform>();
                state.Body = state.Background?.Find("Title")?.TryCast<RectTransform>();
                // Both authored titles use the background's bottom anchor. Growing
                // it down requires compensating only the main body, not moving it.
                if (
                    !state.Background
                    || !state.Body
                    || state.Background.pivot.y != 1
                    || state.Body.anchorMin.y != 0
                    || state.Body.anchorMax.y != 0
                    || rect.anchorMin.y != 0
                    || rect.anchorMax.y != 0
                )
                {
                    return;
                }

                state.BackgroundSize = state.Background.sizeDelta;
                state.BodyPosition = state.Body.anchoredPosition;
            }

            SmallEventLabels[id] = state;
        }

        UpdateSmallEventLabel(state);
    }

    private static void RefreshSmallEventLabelLayouts()
    {
        foreach (var pair in SmallEventLabels.ToArray())
        {
            try
            {
                if (!pair.Value.Text)
                {
                    SmallEventLabels.Remove(pair.Key);
                    continue;
                }

                UpdateSmallEventLabel(pair.Value);
            }
            catch (Exception error)
            {
                Report("RefreshSmallEventLabelLayouts instance", error);
            }
        }
    }

    private static void UpdateSmallEventLabel(SmallEventLabelState state)
    {
        if (!UsesLocalizedLayout)
        {
            RestoreSmallEventLabel(state);
            return;
        }

        if (!state.Text.isActiveAndEnabled || !state.Text.gameObject.activeInHierarchy)
        {
            return;
        }

        var camera = ResearchDetailsCamera(state.Rect);
        var a = RectTransformUtility.WorldToScreenPoint(
            camera,
            state.Rect.TransformPoint(Vector3.zero)
        );
        var b = RectTransformUtility.WorldToScreenPoint(
            camera,
            state.Rect.TransformPoint(Vector3.up)
        );
        var scale = Vector2.Distance(a, b);
        if (!float.IsFinite(scale) || scale < .1f)
        {
            return;
        }

        var reference = Math.Min(1f, Screen.height / 1080f);
        state.Modified = true;
        if (state.Kind == SmallEventLabelKind.FeaturePoints)
        {
            // 1080p: label row center558.75;36px leaves4px to the cost row
            // and16px to the final risk toggle. Preserve its right-hand value.
            var height = Math.Max(state.Height, 36f * reference / scale);
            SetCourtSize(
                state.Rect,
                new Vector2(state.Size.x, state.Size.y + height - state.Height)
            );
            SetCourtPosition(
                state.Value,
                state.ValuePosition - new Vector2(0, (height - state.Height) * .5f)
            );
        }
        else if (state.Kind == SmallEventLabelKind.PublishingRisk)
        {
            // Caption's native right edge1226; budget starts1281. A190px
            // caption retains its left edge and leaves10px before that column.
            var width = Math.Max(state.Width, 190f * reference / scale);
            SetCourtSize(state.Rect, new Vector2(state.Size.x + width - state.Width, state.Size.y));
            SetCourtPosition(
                state.Rect,
                state.Position + new Vector2((width - state.Width) * state.Rect.pivot.x, 0)
            );
        }
        else
        {
            // At1080 the long body ends934.7px and the old9.8px hint touches it.
            // Add18px below the original background. Main body stays put; the
            // 14px hint gets a20px row,145px width and about5px separation.
            var extension = 18f * reference / scale;
            var width = 145f * reference / scale;
            var height = 20f * reference / scale;
            SetCourtSize(
                state.Background,
                new Vector2(state.BackgroundSize.x, state.BackgroundSize.y + extension)
            );
            SetCourtPosition(state.Body, state.BodyPosition + new Vector2(0, extension));
            SetCourtSize(
                state.Rect,
                new Vector2(
                    state.Size.x + width - state.Width,
                    state.Size.y + height - state.Height
                )
            );
            SetCourtPosition(
                state.Rect,
                new Vector2(
                    state.Position.x - (width - state.Width) * (1 - state.Rect.pivot.x),
                    height * .5f + reference / scale
                )
            );
        }

        var font = 14f * reference / scale;
        if (state.Text.enableAutoSizing)
        {
            state.Text.enableAutoSizing = false;
        }

        if (Math.Abs(state.Text.fontSize - font) > .01f)
        {
            state.Text.fontSize = font;
        }

        var wrap = state.Kind == SmallEventLabelKind.FeaturePoints;
        if (state.Text.enableWordWrapping != wrap)
        {
            state.Text.enableWordWrapping = wrap;
        }

        if (state.Text.overflowMode != TextOverflowModes.Overflow)
        {
            state.Text.overflowMode = TextOverflowModes.Overflow;
        }
    }

    private static void RestoreSmallEventLabel(SmallEventLabelState state)
    {
        if (!state.Modified)
        {
            return;
        }

        SetCourtSize(state.Rect, state.Size);
        SetCourtPosition(state.Rect, state.Position);
        if (state.Value)
        {
            SetCourtPosition(state.Value, state.ValuePosition);
        }

        if (state.Background)
        {
            SetCourtSize(state.Background, state.BackgroundSize);
        }

        if (state.Body)
        {
            SetCourtPosition(state.Body, state.BodyPosition);
        }

        var record = state.Record;
        state.Text.enableAutoSizing = record.OriginalAutoSize;
        state.Text.fontSizeMin = record.OriginalSizeMin;
        state.Text.fontSizeMax = record.OriginalSizeMax;
        state.Text.fontSize = record.OriginalFontSize;
        state.Text.enableWordWrapping = record.OriginalWordWrap;
        state.Text.overflowMode = record.OriginalOverflow;
        state.Modified = false;
        record.FitChanged = false;
        if (Catalog.ActiveLanguageId != "en")
        {
            ApplyShortLabelFit(state.Text, record, record.Rendered);
        }
    }
}
