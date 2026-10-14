using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private sealed class TrackedText
    {
        public Component Owner = null!;
        public string Source = "";
        public string Rendered = "";
        public Font OriginalFont;
        public TMP_FontAsset OriginalTmpFont;
        public Material OriginalSharedMaterial;
        public bool? IsLoadingPresentation;
        public bool PresentationFontChanged;
        public float OriginalFontSize;
        public bool OriginalAutoSize;
        public float OriginalSizeMin;
        public float OriginalSizeMax;
        public bool OriginalWordWrap;
        public TextOverflowModes OriginalOverflow;
        public bool FitChanged;
        public bool FitNeedsRetry;
        public int Version = -1;
    }

    private static readonly Dictionary<int, TrackedText> Texts = new();
    private static readonly List<KeyValuePair<int, TrackedText>> TextSnapshot = new();

    internal static void TextEnabled(Component component)
    {
        if (!_started || _writing || MapCaptions.IsDrawing)
        {
            return;
        }

        try
        {
            var tmp = component.TryCast<TMP_Text>();
            var legacy = tmp is null ? component.TryCast<Text>() : null;
            var current = tmp is not null ? tmp.text : legacy?.text;
            if (current is null)
            {
                return;
            }

            if (!CanTranslate(component, current))
            {
                return;
            }

            var record = Observe(component, current);
            ApplyTranslation(record, current, tmp, legacy);
        }
        catch (Exception error)
        {
            Report("enable text", error);
        }
    }

    internal static void Intercept(Component component, ref string value)
    {
        if (_writing || !_started || value is null || MapCaptions.IsDrawing)
        {
            return;
        }

        try
        {
            if (!component)
            {
                return;
            }

            if (IsCountryMapText(component))
            {
                // WMSK must receive its final caption before it calculates character
                // spacing and mesh indexes. Never replace text inside its TMP setter.
                return;
            }

            if (!CanTranslate(component, value))
            {
                return;
            }

            var record = Observe(component, value);
            var translated = TranslateForComponent(component, record.Source);
            record.Version = -1;
            ApplyFont(component, record, translated);
            value = record.Rendered = translated;
            record.Version = _version;
        }
        catch (Exception error)
        {
            Report("text interception", error);
        }
    }

    private static TrackedText Observe(Component component, string current)
    {
        var id = component.GetInstanceID();
        if (
            !Texts.TryGetValue(id, out var record)
            || !record.Owner
            || record.Owner.Pointer != component.Pointer
        )
        {
            record = new TrackedText
            {
                Owner = component,
                Source = current,
                Rendered = current,
            };
            if (component.TryCast<Text>() is { } legacy)
            {
                record.OriginalFont = legacy.font;
                record.OriginalFontSize = legacy.fontSize;
                record.OriginalAutoSize = legacy.resizeTextForBestFit;
                record.OriginalSizeMin = legacy.resizeTextMinSize;
                record.OriginalSizeMax = legacy.resizeTextMaxSize;
            }
            else if (component.TryCast<TMP_Text>() is { } tmp)
            {
                record.OriginalTmpFont = tmp.font;
                record.OriginalSharedMaterial = tmp.fontSharedMaterial;
                record.OriginalFontSize = tmp.fontSize;
                record.OriginalAutoSize = tmp.enableAutoSizing;
                record.OriginalSizeMin = tmp.fontSizeMin;
                record.OriginalSizeMax = tmp.fontSizeMax;
                record.OriginalWordWrap = tmp.enableWordWrapping;
                record.OriginalOverflow = tmp.overflowMode;
            }

            Texts[id] = record;
        }
        else if (current != record.Rendered)
        {
            // Reassigning our own rendered text must not erase the English original.
            record.Source = current;
        }

        return record;
    }

    private static void DiscoverTexts()
    {
        foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            SafelyDiscover(text);
        }

        foreach (var text in Resources.FindObjectsOfTypeAll<Text>())
        {
            SafelyDiscover(text);
        }
    }

    private static void SafelyDiscover(Component component)
    {
        try
        {
            if (!component)
            {
                return;
            }

            if (
                Texts.TryGetValue(component.GetInstanceID(), out var known)
                && known.Owner
                && known.Owner.Pointer == component.Pointer
            )
            {
                return;
            }

            // Most objects are already tracked. Read text only for new ones,
            // without allocating a closure for every label on every discovery pass.
            var tmp = component.TryCast<TMP_Text>();
            var current = (tmp is not null ? tmp.text : component.TryCast<Text>()?.text) ?? "";
            if (CanTranslate(component, current))
            {
                Observe(component, current);
            }
        }
        catch (Exception error)
        {
            Report("discover text", error);
        }
    }

    private static void RefreshTexts(bool includeInactive)
    {
        // Unity setters may synchronously trigger other UI updates, hence a snapshot.
        TextSnapshot.Clear();
        TextSnapshot.AddRange(Texts);
        foreach (var entry in TextSnapshot)
        {
            var record = entry.Value;
            try
            {
                if (!record.Owner)
                {
                    Texts.Remove(entry.Key);
                    continue;
                }

                if (!includeInactive && !record.Owner.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var tmp = record.Owner.TryCast<TMP_Text>();
                var legacy = tmp is null ? record.Owner.TryCast<Text>() : null;
                var current = tmp is not null ? tmp.text : legacy?.text;
                if (current is null)
                {
                    continue;
                }

                if (current == record.Rendered && record.Version == _version)
                {
                    continue;
                }

                if (!CanTranslate(record.Owner, current))
                {
                    continue;
                }

                if (current != record.Rendered)
                {
                    record.Source = current;
                }

                ApplyTranslation(record, current, tmp, legacy);
            }
            catch (Exception error)
            {
                Report("refresh text", error);
            }
        }

        TextSnapshot.Clear();
    }

    private static void ApplyTranslation(
        TrackedText record,
        string current,
        TMP_Text tmp,
        Text legacy
    )
    {
        var translated = TranslateForComponent(record.Owner, record.Source);
        record.Version = -1;
        ApplyFont(record.Owner, record, translated);
        if (current != translated)
        {
            _writing = true;
            try
            {
                if (tmp is not null)
                {
                    tmp.text = translated;
                }
                else if (legacy is not null)
                {
                    legacy.text = translated;
                }
            }
            finally
            {
                _writing = false;
            }
        }

        // Commit only after Unity accepts the update. A failed setter must
        // not turn the previous translation into the next English source.
        record.Rendered = translated;
        record.Version = _version;
    }
}
