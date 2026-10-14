using TMPro;
using UnityEngine;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private static readonly Dictionary<int, TrackedText> ManagerRowTexts = new();

    // Run AFTER ApplyOfficeActionLayout. Keep its proven row geometry unchanged;
    // short context-specific action captions fit the existing 90 px text area.
    private static void ApplyManagerRowLayout(Component component, TrackedText record, string value)
    {
        var text = component.TryCast<TMP_Text>();
        if (!text || record.OriginalFontSize <= 0)
        {
            return;
        }

        var owner = ParentComponent<CTGUI_Manager_MiniWindow>(component);
        if (!owner || !ParentComponent<CTGUI_ManagerPoolWindow>(component))
        {
            return;
        }

        var parent = component.transform.parent;
        var action =
            parent
            && owner.activityButton
            && parent.Pointer == owner.activityButton.transform.Pointer
            && component.name == "Text";
        var skills =
            parent
            && owner.skillsButton
            && parent.Pointer == owner.skillsButton.transform.Pointer
            && component.name == "Text";
        var activity =
            component.name == "activity_lbl" && parent && parent.Pointer == owner.transform.Pointer;
        var progress =
            component.name == "Text"
            && parent
            && parent.name == "progress_bar"
            && parent.parent
            && parent.parent.Pointer == owner.transform.Pointer;
        if (!action && !skills && !activity && !progress)
        {
            return;
        }

        ManagerRowTexts[component.GetInstanceID()] = record;
        if (!UsesLocalizedLayout)
        {
            text.enableAutoSizing = record.OriginalAutoSize;
            text.fontSizeMin = record.OriginalSizeMin;
            text.fontSizeMax = record.OriginalSizeMax;
            text.fontSize = record.OriginalFontSize;
            text.enableWordWrapping = record.OriginalWordWrap;
            text.overflowMode = record.OriginalOverflow;
            return; // OfficeActionLayout restores all row geometry from its original snapshots.
        }

        if (
            !component.gameObject.activeInHierarchy
            || !OfficeActions.TryGetValue(owner.GetInstanceID(), out var state)
            || !state.Modified
        )
        {
            return;
        }

        var rect = text.rectTransform;
        Camera camera = null;
        for (var tr = component.transform; tr; tr = tr.parent)
        {
            var canvas = tr.GetComponent<Canvas>();
            if (!canvas)
            {
                continue;
            }

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                camera = null;
                break;
            }

            if (canvas.worldCamera)
            {
                camera = canvas.worldCamera;
                break;
            }
        }

        if (!camera)
        {
            return; // These confirmed world-space rows use the native UI Camera.
        }

        var p0 = camera.WorldToScreenPoint(rect.TransformPoint(new Vector3(0, rect.rect.yMin, 0)));
        var p1 = camera.WorldToScreenPoint(rect.TransformPoint(new Vector3(0, rect.rect.yMax, 0)));
        var scale = Math.Abs(p1.y - p0.y) / Math.Max(.001f, rect.rect.height);
        if (!float.IsFinite(scale) || scale < .01f || p0.z <= camera.nearClipPlane)
        {
            return;
        }

        // 14 screen pixels at 1080p; preserve native proportional UI scaling below it.
        var targetPixels = 14f * Math.Min(1f, Screen.height / 1080f);
        var font = targetPixels / scale;
        text.enableAutoSizing = false;
        text.fontSize = font;
        text.enableWordWrapping = activity;
        text.overflowMode = activity ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
    }

    private static void RefreshManagerRowLayouts()
    {
        RefreshTrackedStates(
            ManagerRowTexts,
            static record => record.Owner,
            static record => ApplyManagerRowLayout(record.Owner, record, record.Rendered),
            nameof(RefreshManagerRowLayouts)
        );
    }
}
