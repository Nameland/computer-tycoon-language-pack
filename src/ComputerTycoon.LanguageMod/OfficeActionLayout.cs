using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private sealed class OfficeRectSnapshot
    {
        public RectTransform Rect;
        public Vector2 Size;
        public Vector2 Position;
        public float Width;
        public OfficeRectSnapshot(RectTransform rect)
        {
            Rect = rect;
            Size = rect.sizeDelta;
            Position = rect.anchoredPosition;
            Width = rect.rect.width;
        }
        public void Restore()
        {
            if (!Rect)
                return;
            Rect.sizeDelta = Size;
            Rect.anchoredPosition = Position;
        }
        public void WidthFromLeft(float width)
        {
            var delta = width - Width;
            Rect.sizeDelta = new Vector2(Size.x + delta, Size.y);
            Rect.anchoredPosition = new Vector2(Position.x + delta * Rect.pivot.x * Rect.localScale.x, Position.y);
        }
    }

    private sealed class OfficeActionState
    {
        public Component Owner;
        public OfficeRectSnapshot Action, Skills, ActionText, SkillsText, Activity, Progress;
        public List<OfficeRectSnapshot> ProgressChildren = new();
        public Dictionary<int, TrackedText> Labels = new();
        public bool Modified;
    }

    private static readonly Dictionary<int, OfficeActionState> OfficeActions = new();

    // Hook after ApplyShortLabelFit/ApplyScreenLayout in ApplyFont.
    // Confirmed live ManagerPoolWindow/.../ManagerMiniWindow/TryAgain_btn*/Text.
    // The nested world-space canvas has scale .00333396; its orthographic camera
    // projects 100 pixels per world unit at 1080p. This is a visible 50px button,
    // not an off-screen menu merely because its lossyScale is below one.
    private static void ApplyOfficeActionLayout(Component component, TrackedText record, string value)
    {
        var text = component.TryCast<TMP_Text>();
        if (text is null || record.OriginalFontSize <= 0)
            return;
        var owner = ParentComponent<CTGUI_Manager_MiniWindow>(component);
        if (!owner || !ParentComponent<CTGUI_ManagerPoolWindow>(component))
            return;
        if (!owner.activityButton || !owner.skillsButton || !component.transform.parent)
            return;
        var parent = component.transform.parent;
        if (parent.Pointer != owner.activityButton.transform.Pointer &&
            parent.Pointer != owner.skillsButton.transform.Pointer)
            return;
        if (component.name != "Text")
            return;

        var id = owner.GetInstanceID();
        if (OfficeActions.TryGetValue(id, out var state) &&
            (!state.Owner || state.Owner.Pointer != owner.Pointer))
        {
            OfficeActions.Remove(id);
            state = null;
        }
        if (state is null)
        {
            if (!UsesLocalizedLayout)
                return;
            var action = owner.activityButton.transform.TryCast<RectTransform>();
            var skills = owner.skillsButton.transform.TryCast<RectTransform>();
            var actionText = action ? action.Find("Text")?.TryCast<RectTransform>() : null;
            var skillsText = skills ? skills.Find("Text")?.TryCast<RectTransform>() : null;
            var activity = owner.transform.Find("activity_lbl")?.TryCast<RectTransform>();
            var progress = owner.transform.Find("progress_bar")?.TryCast<RectTransform>();
            if (!action || !skills || !actionText || !skillsText || !activity || !progress)
                return;
            // Restrict to the verified scene geometry, not differently laid-out resource variants.
            if (Math.Abs(action.rect.width - 150f) > 2f || Math.Abs(skills.rect.width - 150f) > 2f ||
                action.anchorMin.x != 0f || action.anchorMax.x != 0f ||
                activity.anchorMin.x != 0f || progress.anchorMin.x != 0f)
                return;
            state = new OfficeActionState
            {
                Owner = owner,
                Action = new(action),
                Skills = new(skills),
                ActionText = new(actionText),
                SkillsText = new(skillsText),
                Activity = new(activity),
                Progress = new(progress)
            };
            foreach (var childName in new[] { "generated_bar", "generated_fg_1", "Text" })
            {
                var child = progress.Find(childName)?.TryCast<RectTransform>();
                if (child)
                    state.ProgressChildren.Add(new(child));
            }
            OfficeActions[id] = state;
        }
        state.Labels[component.GetInstanceID()] = record;
        if (!UsesLocalizedLayout)
        {
            if (!state.Modified)
                return;
            foreach (var rect in new[] { state.Action, state.Skills, state.ActionText, state.SkillsText, state.Activity, state.Progress })
                rect.Restore();
            foreach (var rect in state.ProgressChildren)
                rect.Restore();
            foreach (var label in state.Labels.Values)
            {
                var tmp = label.Owner ? label.Owner.TryCast<TMP_Text>() : null;
                if (tmp is null)
                    continue;
                tmp.enableWordWrapping = label.OriginalWordWrap;
                tmp.overflowMode = label.OriginalOverflow;
                tmp.fontSizeMin = label.OriginalSizeMin;
                tmp.fontSizeMax = label.OriginalSizeMax;
                tmp.enableAutoSizing = label.OriginalAutoSize;
                tmp.fontSize = label.OriginalFontSize;
                label.FitChanged = false;
                if (Catalog.ActiveLanguageId != "en")
                    ApplyShortLabelFit(label.Owner, label, label.Rendered);
            }
            state.Modified = false;
            return;
        }

        const float growth = 150f;
        GrowOfficeButton(state.Action, state.ActionText, growth);
        GrowOfficeButton(state.Skills, state.SkillsText, growth);
        // Reserve the new button column. Keep the left edges and text font sizes of
        // status/progress intact; resize generated bar rects rather than scaling them.
        var buttonLeft = state.Action.Position.x -
            (state.Action.Width * state.Action.Rect.pivot.x + growth) * state.Action.Rect.localScale.x;
        var right = buttonLeft - 24f;
        var activityLeft = state.Activity.Position.x - state.Activity.Width * state.Activity.Rect.pivot.x * state.Activity.Rect.localScale.x;
        state.Activity.WidthFromLeft(Math.Min(state.Activity.Width, (right - activityLeft) / state.Activity.Rect.localScale.x));
        var progressLeft = state.Progress.Position.x - state.Progress.Width * state.Progress.Rect.pivot.x * state.Progress.Rect.localScale.x;
        var progressWidth = Math.Min(state.Progress.Width, (right - progressLeft) / state.Progress.Rect.localScale.x);
        state.Progress.WidthFromLeft(progressWidth);
        foreach (var child in state.ProgressChildren)
            child.Rect.sizeDelta = new Vector2(child.Size.x + progressWidth - state.Progress.Width, child.Size.y);
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.fontSizeMax = Math.Max(40f, record.OriginalAutoSize ? record.OriginalSizeMax : record.OriginalFontSize);
        text.fontSizeMin = 40f;
        text.enableAutoSizing = true;
        state.Modified = true;
    }

    private static void GrowOfficeButton(OfficeRectSnapshot button, OfficeRectSnapshot label, float growth)
    {
        button.Rect.sizeDelta = new Vector2(button.Size.x + growth, button.Size.y);
        button.Rect.anchoredPosition = new Vector2(button.Position.x - growth * (1f - button.Rect.pivot.x) * button.Rect.localScale.x, button.Position.y);
        var anchors = label.Rect.anchorMax.x - label.Rect.anchorMin.x;
        label.Rect.sizeDelta = new Vector2(label.Size.x + growth * (1f - anchors), label.Size.y);
        var anchorReference = label.Rect.anchorMin.x + anchors * label.Rect.pivot.x;
        label.Rect.anchoredPosition = new Vector2(label.Position.x + growth * (label.Rect.pivot.x - anchorReference), label.Position.y);
    }
}
