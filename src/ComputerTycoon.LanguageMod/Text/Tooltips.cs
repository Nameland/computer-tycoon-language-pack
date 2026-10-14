using UnityEngine;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private sealed class TooltipText
    {
        public VfLabelManager Owner;
        public string Source;
        public string Rendered;
        public GUIStyle Style;
        public Font OriginalFont;
    }

    private static readonly Dictionary<int, TooltipText> Tooltips = new();

    // VfCursorTarget forwards its serialized LabelText to an IMGUI label manager.
    // There is no TMP_Text/UI.Text component for DiscoverTexts to find here.
    internal static void PrepareTooltip(VfLabelManager manager)
    {
        if (!_started || BlockGameMouse || !manager)
        {
            return;
        }

        try
        {
            var current = manager.LabelText ?? "";
            var id = manager.GetInstanceID();
            if (
                !Tooltips.TryGetValue(id, out var text)
                || !text.Owner
                || text.Owner.Pointer != manager.Pointer
            )
            {
                text = new TooltipText
                {
                    Owner = manager,
                    Source = current,
                    Rendered = current,
                };
                Tooltips[id] = text;
                foreach (
                    var stale in Tooltips
                        .Where(pair => !pair.Value.Owner)
                        .Select(pair => pair.Key)
                        .ToArray()
                )
                {
                    Tooltips.Remove(stale);
                }
            }
            else if (NormalizeTooltipText(current) != NormalizeTooltipText(text.Rendered))
            {
                text.Source = current;
            }

            // Retaining Source independently makes English switching and pack reloads
            // reversible even when the mouse remains over the same tooltip target.
            var translated = Catalog.Translate(text.Source);
            if (NormalizeTooltipText(current) != NormalizeTooltipText(translated))
            {
                manager.LabelText = translated;
            }

            text.Rendered = translated;
            ApplyTooltipFont(manager);
        }
        catch (Exception error)
        {
            Report("translate IMGUI tooltip", error);
        }
    }

    // VfLabelManager.OnGUI rewrites literal <br> to LF in its own LabelText.
    // This is a native rendering normalization, not a new source from a target.
    // Keep the exact source and catalog output; compare only their native forms.
    private static string NormalizeTooltipText(string value) => (value ?? "").Replace("<br>", "\n");

    internal static void ApplyTooltipFont(VfLabelManager manager)
    {
        if (!_started || !manager)
        {
            return;
        }

        try
        {
            if (!Tooltips.TryGetValue(manager.GetInstanceID(), out var text))
            {
                return;
            }

            var style = manager._style;
            if (style is null)
            {
                return;
            }

            if (text.Style is null || text.Style.Pointer != style.Pointer)
            {
                text.Style = style;
                text.OriginalFont = style.font;
            }

            var desired =
                Catalog.ActiveLanguageId != "en"
                && text.Rendered.Any(character => character > 127)
                && Fonts.CurrentFont
                    ? Fonts.CurrentFont
                    : text.OriginalFont;
            var current = style.font;
            if (
                (desired && current && desired.Pointer == current.Pointer) || (!desired && !current)
            )
            {
                return;
            }

            style.font = desired;
            // The manager caches its measured rectangle. Font changes must trigger
            // measurement again, just as its native LabelText setter does.
            manager._textDirty = true;
        }
        catch (Exception error)
        {
            Report("prepare IMGUI tooltip font", error);
        }
    }
}
