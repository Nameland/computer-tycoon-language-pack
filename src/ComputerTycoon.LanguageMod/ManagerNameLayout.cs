using TMPro;
using UnityEngine;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private sealed class ManagerNameStyle
    {
        public TMP_Text Text;
        public bool AutoSize;
        public float FontSize, Min, Max;
        public TextAlignmentOptions Alignment;
        public RectTransform InputRect, Viewport;
        public Vector2 InputSize, ViewportSize, ViewportPosition;
        public bool Modified;
    }
    private static readonly Dictionary<int, ManagerNameStyle> ManagerNameStyles = new();
    private static float _nextManagerNameScan;

    // ManagerLevelingWindow.Name_if is intentionally excluded from translation:
    // it contains the user's editable manager name. Change only its presentation.
    private static void RefreshManagerNameLayouts()
    {
        foreach (var pair in ManagerNameStyles.ToArray())
        {
            var state = pair.Value;
            if (!state.Text)
            {
                ManagerNameStyles.Remove(pair.Key);
                continue;
            }
            if (UsesLocalizedLayout || !state.Modified)
                continue;
            state.Text.enableAutoSizing = state.AutoSize;
            state.Text.fontSizeMin = state.Min;
            state.Text.fontSizeMax = state.Max;
            state.Text.fontSize = state.FontSize;
            state.Text.alignment = state.Alignment;
            if (state.InputRect)
                state.InputRect.sizeDelta = state.InputSize;
            if (state.Viewport)
            {
                state.Viewport.sizeDelta = state.ViewportSize;
                state.Viewport.anchoredPosition = state.ViewportPosition;
            }
            state.Modified = false;
        }
        if (!UsesLocalizedLayout || Time.realtimeSinceStartup < _nextManagerNameScan)
            return;
        _nextManagerNameScan = Time.realtimeSinceStartup + 2f;
        foreach (var owner in Resources.FindObjectsOfTypeAll<CTGUI_ManagerLevelingWindow>())
        {
            if (!owner || !owner.gameObject.scene.IsValid() || !owner.gameObject.activeInHierarchy)
                continue;
            if (owner.Name_if && owner.Name_if.gameObject.activeInHierarchy)
                ApplyManagerNameStyle(owner.Name_if.textComponent);
            if (owner.Name_lbl && owner.Name_lbl.gameObject.activeInHierarchy)
                ApplyManagerNameStyle(owner.Name_lbl);
        }
    }
    private static void ApplyManagerNameStyle(TMP_Text text)
    {
        if (!text || !text.enabled || !text.gameObject.activeInHierarchy)
            return;
        var rect = text.rectTransform;
        var canvas = ParentComponent<Canvas>(text);
        if (!rect || !canvas || rect.rect.height <= 0f)
            return;
        Camera camera = null;
        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            // Nested Doozy canvases inherit the UI camera from an outer canvas.
            for (var node = text.transform; node; node = node.parent)
            {
                var ancestorCanvas = node.GetComponent<Canvas>();
                if (ancestorCanvas && ancestorCanvas.worldCamera)
                {
                    camera = ancestorCanvas.worldCamera;
                    break;
                }
            }
            if (!camera)
                return;
        }
        var p0 = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(Vector3.zero));
        var p1 = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(Vector3.up));
        var scale = Vector2.Distance(p0, p1);
        if (!float.IsFinite(scale) || scale < .01f)
            return;
        var id = text.GetInstanceID();
        if (!ManagerNameStyles.TryGetValue(id, out var state) || !state.Text || state.Text.Pointer != text.Pointer)
        {
            state = new ManagerNameStyle
            {
                Text = text,
                AutoSize = text.enableAutoSizing,
                FontSize = text.fontSize,
                Min = text.fontSizeMin,
                Max = text.fontSizeMax,
                Alignment = text.alignment
            };
            var input = ParentComponent<TMP_InputField>(text);
            if (input && input.textComponent && input.textComponent.Pointer == text.Pointer &&
                input.name == "Name_if" && input.textViewport)
            {
                state.InputRect = input.transform.Cast<RectTransform>();
                state.Viewport = input.textViewport;
                state.InputSize = state.InputRect.sizeDelta;
                state.ViewportSize = state.Viewport.sizeDelta;
                state.ViewportPosition = state.Viewport.anchoredPosition;
            }
            ManagerNameStyles[id] = state;
        }
        // 20 screen-pixel em is about 14px of actual Latin capital glyph height.
        // Native proportional scaling is retained below 1080p. Long names may
        // autosize to 17.5px em, and the input retains its native horizontal scroll.
        var referenceScale = Math.Min(1f, Screen.height / 1080f);
        var target = 20f * referenceScale / scale;
        var originalMax = state.AutoSize ? state.Max : state.FontSize;
        var maximum = Math.Max(originalMax, target);
        var minimum = Math.Min(maximum, 17.5f * referenceScale / scale);
        state.Modified = true;
        if (state.InputRect && state.Viewport)
        {
            // The authored viewport was only 13 screen pixels high. A readable
            // 20px em needs room for ascenders and descenders, not just its font size.
            var height = 34f * referenceScale / scale;
            state.InputRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            state.Viewport.sizeDelta = new Vector2(state.ViewportSize.x, -6f * referenceScale / scale);
            state.Viewport.anchoredPosition = new Vector2(state.ViewportPosition.x, 0f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
        }
        if (Math.Abs(text.fontSizeMax - maximum) > .01f)
            text.fontSizeMax = maximum;
        if (Math.Abs(text.fontSizeMin - minimum) > .01f)
            text.fontSizeMin = minimum;
        if (!text.enableAutoSizing)
            text.enableAutoSizing = true;
    }
}
