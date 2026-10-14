using UnityEngine;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private sealed class ManagerHeaderState
    {
        public RectTransform Rect;
        public Vector2 Position;
        public bool Modified;
    }

    private static readonly Dictionary<int, ManagerHeaderState> ManagerHeaders = new();

    private static void ApplyManagerTaskHeaderLayout(
        Component component,
        TrackedText record,
        string value
    )
    {
        if (!UsesLocalizedLayout || value == record.Source)
        {
            return;
        }

        var name = component.name;
        if (
            name is not ("Text" or "Text (1)" or "Text (2)" or "Text (3)" or "Text (4)")
            || !HasContextPath(component, "DoozyUI", "UI Menus", "ManagerTaskActiveList", name)
        )
        {
            return;
        }

        var rect = component.transform.TryCast<RectTransform>();
        if (!rect)
        {
            return;
        }

        var id = rect.GetInstanceID();
        if (
            !ManagerHeaders.TryGetValue(id, out var state)
            || !state.Rect
            || state.Rect.Pointer != rect.Pointer
        )
        {
            state = new ManagerHeaderState { Rect = rect, Position = rect.anchoredPosition };
            ManagerHeaders[id] = state;
        }

        // All five original headings are at y=-193, under the first task button.
        // Shift only this heading row to y=-150; sizes, fonts and task rows stay intact.
        state.Modified = true;
        rect.anchoredPosition = new Vector2(state.Position.x, state.Position.y + 43f);
    }

    private static void RefreshManagerTaskHeaders()
    {
        RefreshTrackedStates(
            ManagerHeaders,
            static state => state.Rect,
            static state =>
            {
                if (!UsesLocalizedLayout && state.Modified)
                {
                    state.Rect.anchoredPosition = state.Position;
                    state.Modified = false;
                }
            },
            nameof(RefreshManagerTaskHeaders)
        );
    }
}
