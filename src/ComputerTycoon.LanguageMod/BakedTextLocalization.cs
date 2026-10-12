using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private sealed class BakedTextSpec
    {
        public string Screen, Sprite, Key, English;
        public float Width, Height;
        // Pixels in the exported original sprite, measured from its top-left corner.
        public Rect Patch;
        public Color Background, Foreground;
        public bool Body;
    }

    private sealed class BakedTextState
    {
        public Image Owner, Cover;
        public TextMeshProUGUI Text;
        public RectTransform Rect;
        public BakedTextSpec Spec;
        public Rect LastOwnerRect;
        public Vector2 LastPivot;
        public bool LastPreserveAspect;
        public bool Placed;
    }

    private static readonly BakedTextSpec[] BakedTextSpecs = {
        new() { Screen = "GameOverBankrupcy", Sprite = "Bankrupcy", Key = "Baked artwork: Game over", English = "GAME OVER",
            Width = 2048, Height = 1152, Patch = new Rect(1150, 157, 809, 145),
            Background = new Color(60f/255, 61f/255, 65f/255), Foreground = new Color(183f/255, 162f/255, 116f/255) },
        new() { Screen = "GameOverMonopoly", Sprite = "Monopoly", Key = "Baked artwork: Victory", English = "VICTORY!",
            Width = 2048, Height = 1152, Patch = new Rect(201, 100, 611, 124),
            Background = new Color(237f/255, 232f/255, 227f/255), Foreground = new Color(232f/255, 174f/255, 85f/255) },
        new() { Screen = "GameOverSingularity", Sprite = "Singularity", Key = "Baked artwork: Victory", English = "VICTORY!",
            Width = 2048, Height = 1152, Patch = new Rect(160, 851, 872, 147),
            Background = new Color(0, 11f/255, 25f/255), Foreground = new Color(208f/255, 119f/255, 48f/255) },
        new() { Screen = "Commodore", Sprite = "Commandor", Key = "Baked artwork: Developer message",
            English = "ERROR.\nHI THERE! THIS IS A POPULAR COMPUTER'S SCREEN FROM THE 80S. IF PEOPLE STOLE AS MANY COMPUTERS AS GAMES - WE WOULD STILL HAVE SYSTEMS LIKE THIS.\n\nIT WAS A LOT OF TIME AND MONEY TO CREATE THIS GAME! PLEASE CONSIDER BUYING IT IF YOU ENJOY IT. IT'S CHEAP. MY RENT ISN'T CHEAP UNFORTUNATELY :(\n\nHAVE A VERY NICE DAY! SOON YOU CAN CONTINUE PLAYING.\n\n@INDIEANDRAS - THE DEVELOPER",
            Width = 1920, Height = 1080, Patch = new Rect(104, 260, 1710, 711),
            Background = new Color(66f/255, 66f/255, 231f/255), Foreground = new Color(170f/255, 170f/255, 255f/255), Body = true }
    };
    private static readonly Dictionary<int, BakedTextState> BakedTexts = new();
    private static float _nextBakedTextDiscovery;

    // Call after RefreshTexts, or from the same 0.25-second update branch.
    // The original sprite is never edited, replaced or saved; EN hides these children.
    private static void RefreshBakedTextLocalization()
    {
        if (Catalog is null)
            return;
        if (Time.unscaledTime >= _nextBakedTextDiscovery)
        {
            _nextBakedTextDiscovery = Time.unscaledTime + 2f;
            foreach (var background in DiscoverImagesThisFrame())
            {
                try
                {
                    if (!background || !background.gameObject.scene.IsValid() || background.name != "Background" || !background.sprite)
                        continue;
                    var spec = BakedTextSpecs.FirstOrDefault(s => background.sprite.name == s.Sprite &&
                        HasContextPath(background, "DoozyUI", "UI FULL Screen ", s.Screen, "Background"));
                    if (spec is null)
                        continue;
                    var id = background.GetInstanceID();
                    if (BakedTexts.TryGetValue(id, out var current) && current.Owner && current.Owner.Pointer == background.Pointer)
                        continue;
                    if (!_tmpFont)
                        continue;
                    BakedTexts[id] = CreateBakedText(background, spec);
                    _log.LogInfo($"Prepared localized artwork text: {spec.Screen}");
                }
                catch (Exception error) { Report("discover baked artwork text", error); }
            }
        }
        foreach (var entry in BakedTexts.ToArray())
        {
            var state = entry.Value;
            try
            {
                if (!state.Owner || !state.Cover || !state.Text)
                {
                    BakedTexts.Remove(entry.Key);
                    continue;
                }
                var translated = Catalog.Translate(state.Spec.Key);
                var enabled = Catalog.ActiveLanguageId != "en" && translated != state.Spec.Key && translated != state.Spec.English && state.Owner.enabled && _tmpFont;
                if (state.Cover.gameObject.activeSelf != enabled)
                    state.Cover.gameObject.SetActive(enabled);
                if (!enabled)
                    continue;
                if (!state.Owner.gameObject.activeInHierarchy)
                    continue;
                PlaceBakedText(state);
                state.Cover.color = state.Spec.Background * state.Owner.color;
                state.Text.color = state.Spec.Foreground * state.Owner.color;
                if (!state.Text.font || state.Text.font.Pointer != _tmpFont.Pointer)
                    state.Text.font = _tmpFont;
                if (state.Text.text != translated)
                {
                    var wasWriting = _writing;
                    _writing = true;
                    try
                    {
                        state.Text.text = translated;
                    }
                    finally { _writing = wasWriting; }
                }
            }
            catch (Exception error) { Report("refresh baked artwork text", error); }
        }
    }

    private static RectTransform NewBakedRect(string name, Transform parent)
    {
        var gameObject = new GameObject(name, new Il2CppReferenceArray<Il2CppSystem.Type>(new[] { Il2CppType.Of<RectTransform>() }));
        gameObject.layer = parent.gameObject.layer;
        gameObject.transform.SetParent(parent, false);
        return gameObject.transform.TryCast<RectTransform>();
    }

    private static BakedTextState CreateBakedText(Image owner, BakedTextSpec spec)
    {
        var rect = NewBakedRect("LanguageMod.BakedText", owner.transform);
        rect.gameObject.SetActive(false);
        var cover = rect.gameObject.AddComponent(Il2CppType.Of<Image>()).TryCast<Image>();
        cover.raycastTarget = false;
        var textRect = NewBakedRect("LanguageMod.BakedCaption", rect);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textRect.gameObject.AddComponent(Il2CppType.Of<TextMeshProUGUI>()).TryCast<TextMeshProUGUI>();
        text.raycastTarget = false;
        text.font = _tmpFont;
        text.richText = false;
        text.enableWordWrapping = spec.Body;
        text.enableAutoSizing = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = spec.Body ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Center;
        text.fontStyle = spec.Body ? FontStyles.Normal : FontStyles.Bold;
        return new BakedTextState { Owner = owner, Cover = cover, Text = text, Rect = rect, Spec = spec };
    }

    private static void PlaceBakedText(BakedTextState state)
    {
        var ownerRect = state.Owner.rectTransform.rect;
        var pivot = state.Owner.rectTransform.pivot;
        var preserveAspect = state.Owner.preserveAspect;
        if (state.Placed && state.LastOwnerRect == ownerRect && state.LastPivot == pivot &&
            state.LastPreserveAspect == preserveAspect)
            return;
        state.LastOwnerRect = ownerRect;
        state.LastPivot = pivot;
        state.LastPreserveAspect = preserveAspect;
        state.Placed = true;
        var drawn = ownerRect;
        var spec = state.Spec;
        // Unity Image.preserveAspect letterboxes around the sprite using the owner's pivot.
        // Match that geometry so the cover cannot drift onto the frame on a wider display.
        if (state.Owner.preserveAspect && drawn.width > 0 && drawn.height > 0)
        {
            var aspect = spec.Width / spec.Height;
            if (aspect > drawn.width / drawn.height)
            {
                var height = drawn.width / aspect;
                drawn.y += (drawn.height - height) * state.Owner.rectTransform.pivot.y;
                drawn.height = height;
            }
            else
            {
                var width = drawn.height * aspect;
                drawn.x += (drawn.width - width) * state.Owner.rectTransform.pivot.x;
                drawn.width = width;
            }
        }
        var sx = drawn.width / spec.Width;
        var sy = drawn.height / spec.Height;
        state.Rect.anchorMin = Vector2.zero;
        state.Rect.anchorMax = Vector2.zero;
        state.Rect.pivot = Vector2.zero;
        state.Rect.anchoredPosition = new Vector2(drawn.x - ownerRect.x + spec.Patch.x * sx,
            drawn.y - ownerRect.y + (spec.Height - spec.Patch.yMax) * sy);
        state.Rect.sizeDelta = new Vector2(spec.Patch.width * sx, spec.Patch.height * sy);
        state.Text.margin = new Vector4(10 * sx, 5 * sy, 10 * sx, 5 * sy);
        state.Text.fontSizeMax = (spec.Body ? 34 : spec.Patch.height * .75f) * sy;
        state.Text.fontSizeMin = (spec.Body ? 20 : spec.Patch.height * .35f) * sy;
        state.Text.fontSize = state.Text.fontSizeMax;
    }
}
