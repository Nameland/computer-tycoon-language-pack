using TMPro;
using UnityEngine;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private sealed class ResearchDetailsState
    {
        public RectTransform Panel;
        public RectTransform TitleRect;
        public RectTransform DescriptionRect;
        public Vector2 PanelSize;
        public Vector2 TitleSize;
        public Vector2 DescriptionSize;
        public Vector2 DescriptionPosition;
        public float OriginalPanelWidth;
        public TMP_Text Title;
        public TMP_Text Description;
        public TrackedText TitleRecord;
        public TrackedText DescriptionRecord;
        public bool Modified;
    }

    private static readonly Dictionary<int, ResearchDetailsState> ResearchDetailsStates = new();

    // Narrowly applies to the actual floating research details panel. The left
    // statistics column stays anchored at x=0; both queue buttons already use a
    // centered x anchor and naturally follow the center of the widened right column.
    private static void ApplyResearchDetailsLayout(Component component, TrackedText record)
    {
        var tmp = component.TryCast<TMP_Text>();
        if (tmp is null)
        {
            return;
        }

        var title = HasContextPath(
            component,
            "ResearchDetailsViewDoozy",
            "ResearchDetailsView",
            "WindowTitle"
        );
        var description = HasContextPath(
            component,
            "ResearchDetailsViewDoozy",
            "ResearchDetailsView",
            "description_lbl"
        );
        if (!title && !description)
        {
            return;
        }

        var panel = component.transform.parent?.TryCast<RectTransform>();
        if (!panel)
        {
            return;
        }

        var id = panel.GetInstanceID();
        if (
            !ResearchDetailsStates.TryGetValue(id, out var state)
            || !state.Panel
            || state.Panel.Pointer != panel.Pointer
        )
        {
            if (!UsesLocalizedLayout)
            {
                return;
            }

            var titleRect = panel.Find("WindowTitle")?.TryCast<RectTransform>();
            var descriptionRect = panel.Find("description_lbl")?.TryCast<RectTransform>();
            if (!titleRect || !descriptionRect)
            {
                return;
            }

            // Shipped geometry: panel left/bottom anchored; title centered at top;
            // description anchored to the top-left. Do not guess for other layouts.
            if (
                panel.pivot.x != 0
                || titleRect.anchorMin.x != .5f
                || titleRect.anchorMax.x != .5f
                || descriptionRect.anchorMin.x != 0
                || descriptionRect.anchorMax.x != 0
            )
            {
                return;
            }

            state = new ResearchDetailsState
            {
                Panel = panel,
                PanelSize = panel.sizeDelta,
                OriginalPanelWidth = panel.rect.width,
                TitleRect = titleRect,
                TitleSize = titleRect.sizeDelta,
                DescriptionRect = descriptionRect,
                DescriptionSize = descriptionRect.sizeDelta,
                DescriptionPosition = descriptionRect.anchoredPosition,
                Title = titleRect.GetComponent<TMP_Text>(),
                Description = descriptionRect.GetComponent<TMP_Text>(),
            };
            ResearchDetailsStates[id] = state;
        }

        if (title)
        {
            state.TitleRecord = record;
        }

        if (description)
        {
            state.DescriptionRecord = record;
        }

        UpdateResearchDetailsLayout(state);
    }

    // Retry after native parenting/animation and react to resolution changes even
    // when the displayed technology text itself has not changed.
    private static void RefreshResearchDetailsLayouts()
    {
        foreach (var pair in ResearchDetailsStates.ToArray())
        {
            try
            {
                if (!pair.Value.Panel)
                {
                    ResearchDetailsStates.Remove(pair.Key);
                    continue;
                }

                UpdateResearchDetailsLayout(pair.Value);
            }
            catch (Exception error)
            {
                Report("RefreshResearchDetailsLayouts instance", error);
            }
        }
    }

    private static void UpdateResearchDetailsLayout(ResearchDetailsState state)
    {
        if (!UsesLocalizedLayout)
        {
            if (!state.Modified)
            {
                return;
            }

            SetResearchSize(state.Panel, state.PanelSize);
            SetResearchSize(state.TitleRect, state.TitleSize);
            SetResearchSize(state.DescriptionRect, state.DescriptionSize);
            if (state.DescriptionRect)
            {
                state.DescriptionRect.anchoredPosition = state.DescriptionPosition;
            }

            RestoreResearchText(state.Title, state.TitleRecord);
            RestoreResearchText(state.Description, state.DescriptionRecord);
            state.Modified = false;
            return;
        }

        if (!state.Panel.gameObject.activeInHierarchy || !state.Title || !state.Description)
        {
            return;
        }

        var camera = ResearchDetailsCamera(state.Panel);
        var a = RectTransformUtility.WorldToScreenPoint(
            camera,
            state.Panel.TransformPoint(Vector3.zero)
        );
        var b = RectTransformUtility.WorldToScreenPoint(
            camera,
            state.Panel.TransformPoint(Vector3.right)
        );
        var pixelScale = Vector2.Distance(a, b);
        // During Doozy's opening animation the scale can temporarily be zero.
        if (!float.IsFinite(pixelScale) || pixelScale < .1f)
        {
            return;
        }

        var targetPixels = Math.Min(500f * Screen.height / 1080f, Screen.width * .45f);
        var targetWidth = Math.Max(state.OriginalPanelWidth, targetPixels / pixelScale);
        var extra = targetWidth - state.OriginalPanelWidth;
        state.Modified = true;
        SetResearchSize(state.Panel, new Vector2(state.PanelSize.x + extra, state.PanelSize.y));
        SetResearchSize(state.TitleRect, new Vector2(state.TitleSize.x + extra, state.TitleSize.y));
        SetResearchSize(
            state.DescriptionRect,
            new Vector2(state.DescriptionSize.x + extra, state.DescriptionSize.y)
        );
        // Keep the description's original left edge: all extra space extends right.
        var position = new Vector2(
            state.DescriptionPosition.x + extra * state.DescriptionRect.pivot.x,
            state.DescriptionPosition.y
        );
        if ((state.DescriptionRect.anchoredPosition - position).sqrMagnitude > .0001f)
        {
            state.DescriptionRect.anchoredPosition = position;
        }

        ConfigureResearchText(state.Description, state.DescriptionRecord, pixelScale, true);
        ConfigureResearchText(state.Title, state.TitleRecord, pixelScale, false);
    }

    private static void ConfigureResearchText(
        TMP_Text text,
        TrackedText record,
        float pixelScale,
        bool description
    )
    {
        if (!text || record is null)
        {
            return;
        }

        var maximum = record.OriginalAutoSize ? record.OriginalSizeMax : record.OriginalFontSize;
        // Physical 14px em minimum, up to the original 40/35.8 unit design maximum.
        // At the measured 1080p scale this is 31.25 local units, not 14 local units.
        var minimum = Math.Min(maximum, 14f * Math.Min(1f, Screen.height / 1080f) / pixelScale);
        var changed =
            !text.enableAutoSizing
            || Math.Abs(text.fontSizeMin - minimum) > .01f
            || Math.Abs(text.fontSizeMax - maximum) > .01f;
        if (text.enableWordWrapping != description)
        {
            text.enableWordWrapping = description;
        }

        if (text.overflowMode != TextOverflowModes.Overflow)
        {
            text.overflowMode = TextOverflowModes.Overflow;
        }

        if (Math.Abs(text.fontSizeMin - minimum) > .01f)
        {
            text.fontSizeMin = minimum;
        }

        if (Math.Abs(text.fontSizeMax - maximum) > .01f)
        {
            text.fontSizeMax = maximum;
        }

        if (!text.enableAutoSizing)
        {
            text.enableAutoSizing = true;
        }

        if (changed)
        {
            text.fontSize = maximum;
        }
    }

    private static void RestoreResearchText(TMP_Text text, TrackedText record)
    {
        if (!text || record is null)
        {
            return;
        }

        text.enableAutoSizing = record.OriginalAutoSize;
        text.fontSizeMin = record.OriginalSizeMin;
        text.fontSizeMax = record.OriginalSizeMax;
        text.fontSize = record.OriginalFontSize;
        text.enableWordWrapping = record.OriginalWordWrap;
        text.overflowMode = record.OriginalOverflow;
        if (Catalog.ActiveLanguageId != "en")
        {
            record.FitChanged = false;
            ApplyShortLabelFit(text, record, record.Rendered);
        }
    }

    private static void SetResearchSize(RectTransform rect, Vector2 value)
    {
        if (rect && (rect.sizeDelta - value).sqrMagnitude > .0001f)
        {
            rect.sizeDelta = value;
        }
    }

    private static Camera ResearchDetailsCamera(Component component)
    {
        for (var t = component.transform; t; t = t.parent)
        {
            var canvas = t.GetComponent<Canvas>();
            if (!canvas)
            {
                continue;
            }

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            if (canvas.worldCamera)
            {
                return canvas.worldCamera;
            }
        }

        return Camera.main;
    }
}
