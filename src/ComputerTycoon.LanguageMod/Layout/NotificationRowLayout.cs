using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private sealed class NotificationRowState
    {
        internal TMP_Text Text;
        internal TrackedText Record;
        internal string LayoutText;
        internal int LayoutFrame;
        internal float LayoutWidth;
        internal float LayoutFont;
        internal RectTransform Rect;
        internal RectTransform Viewport;
        internal Vector2 OriginalSizeDelta;
        internal bool Modified;
    }

    private static readonly Dictionary<int, NotificationRowState> NotificationRows = new();

    private sealed class NotificationScrollState
    {
        internal ScrollRect Scroll;
        internal Vector2 Position;
        internal Vector2 Velocity;
        internal int Frame;
    }

    private static readonly Dictionary<int, NotificationScrollState> NotificationScrolls = new();

    private static void PreserveNotificationScroll(RectTransform viewport)
    {
        var scroll = viewport
            .parent?.GetComponent(Il2CppType.Of<ScrollRect>())
            ?.TryCast<ScrollRect>();
        if (!scroll)
        {
            return;
        }

        var id = scroll.GetInstanceID();
        if (NotificationScrolls.ContainsKey(id))
        {
            return;
        }

        NotificationScrolls[id] = new NotificationScrollState
        {
            Scroll = scroll,
            Position = scroll.normalizedPosition,
            Velocity = scroll.velocity,
            Frame = Time.frameCount,
        };
    }

    private static void ApplyNotificationRowLayout(Component component)
    {
        var text = component.TryCast<TMP_Text>();
        if (
            !text
            || !HasContextPath(
                component,
                "GameNotificationsWindowN",
                "Scroll View",
                "Viewport",
                "Content",
                "gameMessageNotificationText(Clone)"
            )
        )
        {
            return;
        }

        var id = text.GetInstanceID();
        if (
            !NotificationRows.TryGetValue(id, out var state)
            || !state.Text
            || state.Text.Pointer != text.Pointer
        )
        {
            if (!UsesLocalizedLayout)
            {
                return;
            }

            var rect = text.rectTransform;
            var viewport = rect.parent?.parent?.TryCast<RectTransform>();
            // Authored row is left-pivoted: increasing width then leaves its
            // position, left edge, height, row spacing and scroll state intact.
            if (
                !Texts.TryGetValue(id, out var record)
                || !viewport
                || viewport.name != "Viewport"
                || rect.pivot.x != 0
                || Quaternion.Angle(rect.localRotation, Quaternion.identity) > .01f
            )
            {
                return;
            }

            state = new NotificationRowState
            {
                Text = text,
                Record = record,
                Rect = rect,
                Viewport = viewport,
                OriginalSizeDelta = rect.sizeDelta,
            };
            NotificationRows[id] = state;
        }

        UpdateNotificationRow(state);
    }

    private static void RefreshNotificationRows()
    {
        foreach (var pair in NotificationScrolls.ToArray())
        {
            try
            {
                var saved = pair.Value;
                if (!saved.Scroll)
                {
                    NotificationScrolls.Remove(pair.Key);
                    continue;
                }

                if (Time.frameCount <= saved.Frame + 1)
                {
                    continue;
                }

                saved.Scroll.normalizedPosition = saved.Position;
                saved.Scroll.velocity = saved.Velocity;
                NotificationScrolls.Remove(pair.Key);
            }
            catch (Exception error)
            {
                Report("RefreshNotificationRows instance", error);
            }
        }

        foreach (var pair in NotificationRows.ToArray())
        {
            try
            {
                if (!pair.Value.Text || !pair.Value.Viewport)
                {
                    NotificationRows.Remove(pair.Key);
                    continue;
                }

                UpdateNotificationRow(pair.Value);
            }
            catch (Exception error)
            {
                Report("RefreshNotificationRows instance", error);
            }
        }
    }

    private static void UpdateNotificationRow(NotificationRowState state)
    {
        if (!UsesLocalizedLayout)
        {
            if (state.Modified)
            {
                PreserveNotificationScroll(state.Viewport);
                state.Rect.sizeDelta = state.OriginalSizeDelta;
                var record = state.Record;
                state.Text.enableAutoSizing = record.OriginalAutoSize;
                state.Text.fontSizeMin = record.OriginalSizeMin;
                state.Text.fontSizeMax = record.OriginalSizeMax;
                state.Text.fontSize = record.OriginalFontSize;
                state.Text.enableWordWrapping = record.OriginalWordWrap;
                state.Text.overflowMode = record.OriginalOverflow;
                state.Modified = false;
                state.LayoutText = null;
                record.FitChanged = false;
                if (Catalog.ActiveLanguageId != "en")
                {
                    ApplyShortLabelFit(state.Text, record, record.Rendered);
                }
            }

            return;
        }

        if (!state.Text.isActiveAndEnabled || !state.Rect.gameObject.activeInHierarchy)
        {
            return;
        }

        var rect = state.Rect;
        var viewport = state.Viewport;
        var camera = ResearchDetailsCamera(rect);
        var origin = RectTransformUtility.WorldToScreenPoint(
            camera,
            rect.TransformPoint(Vector3.zero)
        );
        var unit = RectTransformUtility.WorldToScreenPoint(
            camera,
            rect.TransformPoint(Vector3.right)
        );
        var scale = Vector2.Distance(origin, unit);
        if (!float.IsFinite(scale) || scale < .1f || unit.x <= origin.x)
        {
            return;
        }

        var right = RectTransformUtility.WorldToScreenPoint(
            camera,
            viewport.TransformPoint(new Vector3(viewport.rect.xMax, viewport.rect.center.y, 0))
        );
        var left = RectTransformUtility.WorldToScreenPoint(
            camera,
            rect.TransformPoint(new Vector3(rect.rect.xMin, rect.rect.center.y, 0))
        );
        var availableWidth = (right.x - left.x - 12f) / scale;
        var parent = rect.parent.TryCast<RectTransform>();
        if (!parent || !float.IsFinite(availableWidth) || availableWidth <= 0)
        {
            return;
        }

        var nativeWidth =
            parent.rect.width * (rect.anchorMax.x - rect.anchorMin.x) + state.OriginalSizeDelta.x;
        var desired = state.OriginalSizeDelta.x + Math.Max(0, availableWidth - nativeWidth);
        var targetFont = 14f / scale;
        var changed =
            state.LayoutText != state.Text.text
            || Math.Abs(state.LayoutWidth - desired) > .01f
            || Math.Abs(state.LayoutFont - targetFont) > .01f
            || state.Text.enableAutoSizing
            || !state.Text.enableWordWrapping
            || state.Text.overflowMode != TextOverflowModes.Overflow;
        state.Modified = true;
        if (changed)
        {
            PreserveNotificationScroll(viewport);
            state.Text.enableAutoSizing = false;
            state.Text.fontSize = targetFont;
            state.Text.enableWordWrapping = true;
            // Overflow permits TMP's ordinary render pass to lay out every line,
            // even before the row has its new height. Never compute a live mesh here.
            state.Text.overflowMode = TextOverflowModes.Overflow;
            rect.sizeDelta = new Vector2(desired, Math.Max(rect.sizeDelta.y, 20f / scale));
            state.LayoutText = state.Text.text;
            state.LayoutWidth = desired;
            state.LayoutFont = targetFont;
            state.LayoutFrame = Time.frameCount;
        }

        if (Time.frameCount <= state.LayoutFrame + 1 || state.Text.havePropertiesChanged)
        {
            return;
        }

        var info = state.Text.m_textInfo;
        if (
            info == null
            || info.lineInfo == null
            || info.lineCount <= 0
            || info.lineCount > info.lineInfo.Length
        )
        {
            return;
        }

        var top = float.NegativeInfinity;
        var bottom = float.PositiveInfinity;
        for (int i = 0; i < info.lineCount; i++)
        {
            var line = info.lineInfo[i];
            if (line.characterCount == 0)
            {
                continue;
            }

            top = Math.Max(top, line.ascender);
            bottom = Math.Min(bottom, line.descender);
        }

        var height = Math.Max(20f / scale, top - bottom + 6f / scale);
        if (!float.IsFinite(height) || height <= 0 || height > 10000)
        {
            return;
        }

        if (Math.Abs(rect.sizeDelta.y - height) > .05f)
        {
            PreserveNotificationScroll(viewport);
            rect.sizeDelta = new Vector2(desired, height);
            state.LayoutFrame = Time.frameCount;
        }
    }
}
