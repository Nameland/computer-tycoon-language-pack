using TMPro;
using UnityEngine;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private enum SiteLabelKind
    {
        None,
        NextLevel,
        MaintenanceTitle,
        FinanceColumn,
        Requirements,
        MaximumLevel,
    }

    private sealed class SiteLabelState
    {
        public TMP_Text Text;
        public TrackedText Record;
        public RectTransform Rect;
        public Vector2 Size;
        public Vector2 Position;
        public float Width;
        public float Height;
        public SiteLabelKind Kind;
        public bool Modified;
    }

    private static readonly Dictionary<int, SiteLabelState> SiteLabels = new();

    private static void ApplySiteLabelLayout(Component component, TrackedText record)
    {
        var text = component.TryCast<TMP_Text>();
        if (!text || !text.gameObject.scene.IsValid())
        {
            return;
        }

        var kind = SiteLabelKind.None;
        if (HasContextPath(component, "PlaceDetails", "Next_level_lbl"))
        {
            kind = SiteLabelKind.NextLevel;
        }
        else if (
            HasContextPath(component, "DepartmentFinanceWindow", "Title")
            && record.Source == "Maintenance Cost Modifiers"
        )
        {
            kind = SiteLabelKind.MaintenanceTitle;
        }
        else if (
            HasContextPath(component, "DepartmentFinanceWindow", "Panel (4)", "Text")
            || HasContextPath(component, "DepartmentFinanceWindow", "Panel (5)", "Text")
            || HasContextPath(component, "DepartmentFinanceWindow", "Panel (6)", "Text")
            || HasContextPath(component, "DepartmentFinanceWindow", "Panel (9)", "Text")
        )
        {
            kind = SiteLabelKind.FinanceColumn;
        }
        else if (HasContextPath(component, "PlaceDetails", "req_value"))
        {
            kind = SiteLabelKind.Requirements;
        }
        else if (HasContextPath(component, "PlaceDetails", "maxProd_value"))
        {
            kind = SiteLabelKind.MaximumLevel;
        }

        if (kind == SiteLabelKind.None)
        {
            return;
        }

        var id = component.GetInstanceID();
        if (
            !SiteLabels.TryGetValue(id, out var state)
            || !state.Text
            || state.Text.Pointer != text.Pointer
        )
        {
            if (!UsesLocalizedLayout)
            {
                return;
            }

            var rect = text.rectTransform;
            state = new SiteLabelState
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
            SiteLabels[id] = state;
        }

        UpdateSiteLabel(state);
    }

    private static void RefreshSiteLabelLayouts()
    {
        RefreshTrackedStates(
            SiteLabels,
            static state => state.Text && state.Rect,
            UpdateSiteLabel,
            nameof(RefreshSiteLabelLayouts)
        );
    }

    private static void UpdateSiteLabel(SiteLabelState state)
    {
        if (
            !UsesLocalizedLayout
            || (
                state.Kind == SiteLabelKind.MaximumLevel
                && state.Record.Source != "5 is the max. level!"
            )
        )
        {
            RestoreSiteLabel(state);
            return;
        }

        if (!state.Text.isActiveAndEnabled || !state.Text.gameObject.activeInHierarchy)
        {
            return;
        }

        var camera = ResearchDetailsCamera(state.Rect);
        var origin = RectTransformUtility.WorldToScreenPoint(
            camera,
            state.Rect.TransformPoint(Vector3.zero)
        );
        var point = RectTransformUtility.WorldToScreenPoint(
            camera,
            state.Rect.TransformPoint(Vector3.up)
        );
        var scale = Vector2.Distance(origin, point);
        if (!float.IsFinite(scale) || scale < .1f)
        {
            return;
        }

        var reference = Math.Min(1f, Screen.height / 1080f);
        var width = state.Width;
        var height = state.Height;
        var delta = Vector2.zero;
        switch (state.Kind)
        {
            case SiteLabelKind.NextLevel:
                width = Math.Max(width, 160f * reference / scale);
                break;
            // Title now spans the four finance columns; raise it above their two-line captions.
            case SiteLabelKind.MaintenanceTitle:
                width = Math.Max(width, 350f * reference / scale);
                delta.y = 16f * reference / scale;
                break;
            case SiteLabelKind.FinanceColumn:
                width = Math.Max(width, 89f * reference / scale);
                height = Math.Max(height, 34f * reference / scale);
                delta.y = (height - state.Height) * state.Rect.pivot.y;
                break; // Preserve bottom; values below do not move.
            case SiteLabelKind.Requirements:
                width = Math.Max(width, 150f * reference / scale);
                height = Math.Max(height, 32f * reference / scale);
                delta.x = -(width - state.Width) * (1 - state.Rect.pivot.x);
                break; // Retain right edge, eleven pixels after 'Req.'.
            case SiteLabelKind.MaximumLevel:
                break; // Compact context caption fits the native value cell at 14 px.
        }

        state.Modified = true;
        SetCourtSize(
            state.Rect,
            state.Size + new Vector2(width - state.Width, height - state.Height)
        );
        SetCourtPosition(state.Rect, state.Position + delta);
        var font = 14f * reference / scale;
        if (state.Text.enableAutoSizing)
        {
            state.Text.enableAutoSizing = false;
        }

        if (Math.Abs(state.Text.fontSize - font) > .01f)
        {
            state.Text.fontSize = font;
        }

        var wrap = state.Kind is SiteLabelKind.FinanceColumn or SiteLabelKind.Requirements;
        if (state.Text.enableWordWrapping != wrap)
        {
            state.Text.enableWordWrapping = wrap;
        }

        if (state.Text.overflowMode != TextOverflowModes.Overflow)
        {
            state.Text.overflowMode = TextOverflowModes.Overflow;
        }
    }

    private static void RestoreSiteLabel(SiteLabelState state)
    {
        if (!state.Modified)
        {
            return;
        }

        SetCourtSize(state.Rect, state.Size);
        SetCourtPosition(state.Rect, state.Position);
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
