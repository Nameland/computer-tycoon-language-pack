using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private sealed class CountryFilterRect
    {
        internal RectTransform Rect;
        internal Vector2 Size, Position, Min, Max, Pivot;
        internal Vector3 Scale;
        internal float Width;
        internal CountryFilterRect(RectTransform rect)
        {
            Rect = rect;
            Size = rect.sizeDelta;
            Position = rect.anchoredPosition;
            Min = rect.anchorMin;
            Max = rect.anchorMax;
            Pivot = rect.pivot;
            Scale = rect.localScale;
            Width = rect.rect.width;
        }
        internal void Restore()
        {
            if (!Rect)
                return;
            Put(Rect, Min, Max, Pivot, Size, Position, Scale);
        }
        internal void ExpandRight(float delta)
        {
            Put(Rect, Min, Max, Pivot, new Vector2(Size.x + delta, Size.y),
                new Vector2(Position.x + delta * Pivot.x * Scale.x, Position.y), Scale);
        }
    }
    private sealed class CountryFilterLabel
    {
        internal TMP_Text Text;
        internal TrackedText Record;
        internal CountryFilterRect Rect, Toggle, Check;
        internal TextAlignmentOptions Alignment;
        internal Vector4 Margin;
    }
    private static bool IsCountryFilterText(Component component)
    {
        var node = component.transform;
        while (node && node.name != "CountryList")
            node = node.parent;
        if (!node || node.parent?.name != "UI Screens")
            return false;
        var panel = node.Find("Panel/Panel");
        return panel && component.transform.IsChildOf(panel);
    }
    private static void PlaceCountryFilterLabel(CountryFilterLabel row, float x, float y, float width, float height, float font)
    {
        Put(row.Rect.Rect, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, .5f),
            new Vector2(width, height), new Vector2(x, y), Vector3.one);
        var text = row.Text;
        if (text.enableAutoSizing)
            text.enableAutoSizing = false;
        if (Math.Abs(text.fontSize - font) > .01f)
            text.fontSize = font;
        if (text.enableWordWrapping)
            text.enableWordWrapping = false;
        if (text.overflowMode != TextOverflowModes.Overflow)
            text.overflowMode = TextOverflowModes.Overflow;
        if (text.alignment != TextAlignmentOptions.Left)
            text.alignment = TextAlignmentOptions.Left;
        if (text.margin != Vector4.zero)
            text.margin = Vector4.zero;
    }
    private static void Put(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot, Vector2 size, Vector2 position, Vector3 scale)
    {
        if (rect.anchorMin != min)
            rect.anchorMin = min;
        if (rect.anchorMax != max)
            rect.anchorMax = max;
        if (rect.pivot != pivot)
            rect.pivot = pivot;
        if (rect.sizeDelta != size)
            rect.sizeDelta = size;
        if (rect.anchoredPosition != position)
            rect.anchoredPosition = position;
        if (rect.localScale != scale)
            rect.localScale = scale;
    }
}
