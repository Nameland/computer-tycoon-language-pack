using ChartAndGraph.Legened;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private sealed class StatisticsRectState
    {
        public RectTransform Rect;
        public Vector2 Size;
        public Vector2 Position;
        public Vector2 Offset;
        public float ExtraWidth;
        public bool Modified;
    }

    private static readonly Dictionary<int, StatisticsRectState> StatisticsRects = new();

    private sealed class StatisticsFontState
    {
        public Text Text;
        public TrackedText Record;
        public TextAnchor Alignment;
        public bool Modified;
    }
    private static readonly Dictionary<int, StatisticsFontState> StatisticsFonts = new();

    private static void ApplyStatisticsLegendFont(Text text, TrackedText record)
    {
        var id = text.GetInstanceID();
        if (!StatisticsFonts.TryGetValue(id, out var state) || !state.Text || state.Text.Pointer != text.Pointer)
            StatisticsFonts[id] = state = new StatisticsFontState
            {
                Text = text,
                Record = record,
                Alignment = text.alignment
            };
        state.Modified = true;
        // The shipped prefab uses 20 local units (~10 physical pixels at 1080p).
        // Its 49-unit row leaves room for a readable 28-unit label. The widened
        // column accommodates translated labels without shrinking the font.
        text.resizeTextForBestFit = false;
        text.fontSize = Math.Max((int)record.OriginalFontSize, 28);
        text.alignment = TextAnchor.MiddleLeft;
    }

    // CTGUIStatisticsScreen owns this screen, but the game rewrites its root
    // GameObject name to a chart category (observed "Capacity"). The script and
    // exact tab/panel suffix remain stable; do not key behavior on the root name.
    private static bool HasStatisticsPath(Component component, params string[] names) =>
        HasContextPath(component, names) && ParentComponent<CTGUIStatisticsScreen>(component);

    private static bool IsStatisticsEconomyLegend(Component component)
    {
        var legend = ParentComponent<CanvasLegend>(component);
        if (!legend)
            return false;
        // Verified CanvasLegend owners in level2. Preferences and country/company
        // name legends are deliberately outside this list.
        return HasStatisticsPath(legend, "Tab8", "Panel_Customers",
                   "EconomyLevelOfYourFans", "MarketShareGlobalDetails") ||
               HasStatisticsPath(legend, "Tab5", "Panel_Customers",
                   "Economy_levels_by_your_sales", "MarketShareGlobalDetails") ||
               HasStatisticsPath(legend, "Tab5", "Panel_Customers",
                   "Economy_levels_by_global_sales (1)", "MarketShareGlobalDetails");
    }

    private static void ApplyStatisticsLayout(Component component, TrackedText record, string value)
    {
        if (!UsesLocalizedLayout || value == record.Source)
            return;
        if (component.TryCast<Text>() is not null && IsPreferenceSource(record.Source))
        {
            var legend = ParentComponent<CanvasLegend>(component);
            if (legend &&
                (HasStatisticsPath(legend, "Tab4", "Panel_Preferencies", "Global_pref", "globalMarketPrefDetails") ||
                 HasStatisticsPath(legend, "Tab4", "Panel_Preferencies", "yourmarket_pref", "yourMarketPrefDetails")))
            {
                var rect = legend.transform.TryCast<RectTransform>();
                // CanvasLegend instantiates resources.assets CanvasLegendItem 14388.
                // Its HorizontalLayoutGroup has childControlWidth=false: widen the
                // legacy Text itself as well as its column. Do this for every row
                // in these two legends so their labels remain aligned.
                ChangeStatisticsRect(rect, 160f, 0f);
                ChangeStatisticsRect(component.transform.TryCast<RectTransform>(), 160f, 0f);
                ApplyStatisticsLegendFont(component.TryCast<Text>(), record);
            }
        }
        if (HasStatisticsPath(component, "Tab6", "Panel_Singularity", "Singularity_explanation"))
        {
            var rect = component.transform.TryCast<RectTransform>();
            if (!rect || !rect.parent)
                return;
            var bar = rect.parent.Find("Singularity_bar")?.TryCast<RectTransform>();
            if (!bar)
                return;
            var id = rect.GetInstanceID();
            var down = 0f;
            if (StatisticsRects.TryGetValue(id, out var known) && known.Rect && known.Rect.Pointer == rect.Pointer)
                down = -known.Offset.y;
            else
            {
                // Original asset gap: 65 local units. Preserve height and leave at
                // least 12 units above the bar, even if a later game build changes it.
                var gap = rect.localPosition.y + rect.rect.yMin - (bar.localPosition.y + bar.rect.yMax);
                down = Math.Max(0f, Math.Min(52f, gap - 12f));
            }
            if (down > 0f)
                ChangeStatisticsRect(rect, 0f, down);
        }
    }

    private static void ChangeStatisticsRect(RectTransform rect, float extraWidth, float down)
    {
        if (!rect)
            return;
        var id = rect.GetInstanceID();
        if (!StatisticsRects.TryGetValue(id, out var state) || !state.Rect || state.Rect.Pointer != rect.Pointer)
        {
            state = new StatisticsRectState
            {
                Rect = rect,
                Size = rect.sizeDelta,
                Position = rect.anchoredPosition,
                ExtraWidth = extraWidth,
                Offset = new Vector2(extraWidth * rect.pivot.x, -down)
            };
            StatisticsRects[id] = state;
        }
        state.Modified = true;
        var size = new Vector2(state.Size.x + state.ExtraWidth, state.Size.y);
        var position = new Vector2(state.Position.x + state.Offset.x, state.Position.y + state.Offset.y);
        if (rect.sizeDelta != size)
            rect.sizeDelta = size;
        if (rect.anchoredPosition != position)
            rect.anchoredPosition = position;
    }

    private static bool IsPreferenceSource(string source) => source is
        "Performance" or "Features" or "Feature" or "Capacity" or "Quality" or "Prestige" or "Ease of Use" or "EaseOfUse" or "Ease-Of-Use";

    private static float _nextStatisticsRetry;

    // A legend can be translated while its clone is still outside the final
    // parent. Retry only these known static labels, at most once every 2 seconds.
    private static void RefreshStatisticsCandidates()
    {
        if (Time.unscaledTime < _nextStatisticsRetry)
            return;
        _nextStatisticsRetry = Time.unscaledTime + 2f;
        foreach (var record in Texts.Values.ToArray())
        {
            var source = record.Source;
            if (!IsPreferenceSource(source) && !EconomyLevels.Contains(source) && source != "FilthyRich" &&
                !source.StartsWith("The technological singularity is the hypothesis", StringComparison.Ordinal))
                continue;
            var owner = record.Owner;
            if (!owner || !owner.gameObject.activeInHierarchy)
                continue;
            try
            {
                // Retry only labels owned by the statistics screen.
                if (!ParentComponent<CTGUIStatisticsScreen>(owner))
                    continue;
                if (UsesLocalizedLayout)
                {
                    var translated = TranslateForComponent(owner, source);
                    if (translated != record.Rendered)
                    {
                        var legacy = owner.TryCast<Text>();
                        var tmp = legacy is null ? owner.TryCast<TMP_Text>() : null;
                        var wasWriting = _writing;
                        _writing = true;
                        try
                        {
                            if (legacy is not null)
                                legacy.text = translated;
                            else if (tmp is not null)
                                tmp.text = translated;
                            record.Rendered = translated;
                        }
                        finally { _writing = wasWriting; }
                    }
                    ApplyStatisticsLayout(owner, record, record.Rendered);
                }
            }
            catch (Exception error) { Report("statistics layout retry", error); }
        }
    }

    private static void RefreshStatisticsLayouts()
    {
        RefreshStatisticsCandidates();
        foreach (var item in StatisticsFonts.ToArray())
        {
            var state = item.Value;
            if (!state.Text)
            {
                StatisticsFonts.Remove(item.Key);
                continue;
            }
            if (UsesLocalizedLayout)
            {
                if (state.Modified && state.Text.gameObject.activeInHierarchy)
                    ApplyStatisticsLegendFont(state.Text, state.Record);
                continue;
            }
            if (!state.Modified)
                continue;
            state.Text.alignment = state.Alignment;
            state.Text.fontSize = (int)state.Record.OriginalFontSize;
            state.Text.resizeTextForBestFit = state.Record.OriginalAutoSize;
            state.Text.resizeTextMinSize = (int)state.Record.OriginalSizeMin;
            state.Text.resizeTextMaxSize = (int)state.Record.OriginalSizeMax;
            state.Modified = false;
        }
        foreach (var item in StatisticsRects.ToArray())
        {
            var state = item.Value;
            if (!state.Rect)
            {
                StatisticsRects.Remove(item.Key);
                continue;
            }
            if (UsesLocalizedLayout)
            {
                if (state.Modified && state.Rect.gameObject.activeInHierarchy)
                    ChangeStatisticsRect(state.Rect, state.ExtraWidth, -state.Offset.y);
                continue;
            }
            if (!state.Modified)
                continue;
            state.Rect.sizeDelta = state.Size;
            state.Rect.anchoredPosition = state.Position;
            state.Modified = false;
        }
    }
}
