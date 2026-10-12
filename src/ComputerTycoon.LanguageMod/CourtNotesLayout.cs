using TMPro;
using UnityEngine;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private sealed class CourtNoteStyle
    {
        public TMP_Text Text;
        public RectTransform Rect;
        public Vector2 Size, Position;
        public Vector3 LocalPosition;
        public float FontSize, Min, Max;
        public bool AutoSize, Wrap;
        public TextOverflowModes Overflow;
        public TextAlignmentOptions Alignment;
    }
    private sealed class CourtNoteState
    {
        public CTGUI_LegalResultScreen Owner;
        public RectTransform Root, Background, Button;
        public Vector2 BackgroundSize, BackgroundPosition, ButtonPosition;
        public CourtNoteStyle Plaintiff, Defendant;
        public bool Modified;
    }
    private static readonly Dictionary<int, CourtNoteState> CourtNoteStates = new();

    private static void ApplyCourtNoteLayout(Component component, TrackedText record)
    {
        var text = component.TryCast<TMP_Text>();
        if (!text)
            return;
        var owner = ParentComponent<CTGUI_LegalResultScreen>(component);
        if (!owner || !owner.gameObject.scene.IsValid())
            return;
        if ((!owner.BribaryImprisonmentPlaintiff || owner.BribaryImprisonmentPlaintiff.Pointer != text.gameObject.Pointer) &&
            (!owner.BribaryImprisonmentDefendant || owner.BribaryImprisonmentDefendant.Pointer != text.gameObject.Pointer))
            return;
        var id = owner.GetInstanceID();
        if (!CourtNoteStates.TryGetValue(id, out var state) || !state.Owner || state.Owner.Pointer != owner.Pointer)
        {
            if (!UsesLocalizedLayout)
                return;
            var root = owner.transform.TryCast<RectTransform>();
            var background = root?.Find("Background")?.TryCast<RectTransform>();
            var button = root?.Find("Exit_btn")?.TryCast<RectTransform>();
            var plaintiff = owner.BribaryImprisonmentPlaintiff?.GetComponent<TMP_Text>();
            var defendant = owner.BribaryImprisonmentDefendant?.GetComponent<TMP_Text>();
            if (!root || !background || !button || !plaintiff || !defendant ||
                plaintiff.transform.parent.Pointer != root.Pointer || defendant.transform.parent.Pointer != root.Pointer)
                return;
            state = new CourtNoteState
            {
                Owner = owner,
                Root = root,
                Background = background,
                Button = button,
                BackgroundSize = background.sizeDelta,
                BackgroundPosition = background.anchoredPosition,
                ButtonPosition = button.anchoredPosition,
                Plaintiff = SnapshotCourtNote(plaintiff),
                Defendant = SnapshotCourtNote(defendant)
            };
            CourtNoteStates[id] = state;
        }
        UpdateCourtNotes(state);
    }
    private static CourtNoteStyle SnapshotCourtNote(TMP_Text text)
    {
        Texts.TryGetValue(text.GetInstanceID(), out var record);
        return new CourtNoteStyle
        {
            Text = text,
            Rect = text.rectTransform,
            Size = text.rectTransform.sizeDelta,
            Position = text.rectTransform.anchoredPosition,
            LocalPosition = text.rectTransform.localPosition,
            FontSize = record?.OriginalFontSize ?? text.fontSize,
            Min = record?.OriginalSizeMin ?? text.fontSizeMin,
            Max = record?.OriginalSizeMax ?? text.fontSizeMax,
            AutoSize = record?.OriginalAutoSize ?? text.enableAutoSizing,
            Wrap = record?.OriginalWordWrap ?? text.enableWordWrapping,
            Overflow = record?.OriginalOverflow ?? text.overflowMode,
            Alignment = text.alignment
        };
    }
    private static void RefreshCourtNoteLayouts()
    {
        foreach (var pair in CourtNoteStates.ToArray())
        {
            if (!pair.Value.Owner)
            {
                CourtNoteStates.Remove(pair.Key);
                continue;
            }
            UpdateCourtNotes(pair.Value);
        }
    }
    private static void UpdateCourtNotes(CourtNoteState state)
    {
        if (!UsesLocalizedLayout)
        {
            RestoreCourtNotes(state);
            return;
        }
        if (!state.Root || !state.Root.gameObject.activeInHierarchy || !state.Plaintiff.Text || !state.Defendant.Text)
            return;
        var plaintiff = state.Plaintiff.Text.gameObject.activeSelf;
        var defendant = state.Defendant.Text.gameObject.activeSelf;
        if (!plaintiff && !defendant)
        {
            RestoreCourtNotes(state);
            return;
        }
        var camera = ResearchDetailsCamera(state.Root);
        var origin = RectTransformUtility.WorldToScreenPoint(camera, state.Root.TransformPoint(Vector3.zero));
        var unit = RectTransformUtility.WorldToScreenPoint(camera, state.Root.TransformPoint(Vector3.up));
        var scale = Vector2.Distance(origin, unit);
        if (!float.IsFinite(scale) || scale < .1f)
            return;
        var reference = Math.Min(1f, Screen.height / 1080f);
        var dual = plaintiff && defendant;
        var extension = dual ? 88f * reference / scale : 0f;
        // Extend only the decorative background. Resizing the root would move its
        // numerous bottom/center-anchored result fields. The original root has no mask.
        SetCourtSize(state.Background, new Vector2(state.BackgroundSize.x,
            state.BackgroundSize.y + extension / state.Background.localScale.y));
        SetCourtPosition(state.Background, state.BackgroundPosition - new Vector2(0, extension * .5f));
        SetCourtPosition(state.Button, state.ButtonPosition - new Vector2(0, extension));
        state.Modified = true;
        if (dual)
        {
            if (!state.Owner.CauseOfAction_value || !state.Owner.DefendantStrategy_value || !state.Owner.IncidenceMultiplier_value)
                return;
            var left = CourtBoundsInRoot(state.Owner.CauseOfAction_value.rectTransform, state.Root).xMin;
            var right = CourtBoundsInRoot(state.Owner.DefendantStrategy_value.rectTransform, state.Root).xMax;
            var top = CourtBoundsInRoot(state.Owner.IncidenceMultiplier_value.rectTransform, state.Root).yMin - 12f * reference / scale;
            if (state.Owner.UnpaidAmount_value)
                top = Math.Min(top, CourtBoundsInRoot(state.Owner.UnpaidAmount_value.rectTransform, state.Root).yMin - 12f * reference / scale);
            var gap = 36f * reference / scale;
            var width = (right - left - gap) * .5f;
            var height = 70f * reference / scale;
            PlaceCourtNote(state.Plaintiff, left, top, width, height);
            PlaceCourtNote(state.Defendant, left + width + gap, top, width, height);
        }
        else
        {
            foreach (var note in new[] { state.Plaintiff, state.Defendant })
            {
                SetCourtPosition(note.Rect, note.Position);
                SetCourtSize(note.Rect, new Vector2(note.Size.x, 52f * reference / (scale * note.Rect.localScale.y)));
            }
        }
        ConfigureCourtNote(state.Plaintiff, scale, reference);
        ConfigureCourtNote(state.Defendant, scale, reference);
    }
    private static Rect CourtBoundsInRoot(RectTransform rect, RectTransform root)
    {
        var a = root.InverseTransformPoint(rect.TransformPoint(new Vector3(rect.rect.xMin, rect.rect.yMin, 0)));
        var b = root.InverseTransformPoint(rect.TransformPoint(new Vector3(rect.rect.xMax, rect.rect.yMax, 0)));
        return Rect.MinMaxRect(Math.Min(a.x, b.x), Math.Min(a.y, b.y), Math.Max(a.x, b.x), Math.Max(a.y, b.y));
    }
    private static void PlaceCourtNote(CourtNoteStyle note, float left, float top, float width, float height)
    {
        SetCourtSize(note.Rect, new Vector2(width / note.Rect.localScale.x, height / note.Rect.localScale.y));
        var position = new Vector3(left + width * note.Rect.pivot.x, top - height * (1f - note.Rect.pivot.y), note.LocalPosition.z);
        if ((note.Rect.localPosition - position).sqrMagnitude > .0001f)
            note.Rect.localPosition = position;
    }
    private static void ConfigureCourtNote(CourtNoteStyle note, float rootScale, float reference)
    {
        // Do not alter an unobserved sibling before Runtime records its native font.
        if (!Texts.TryGetValue(note.Text.GetInstanceID(), out var record) || !record.Owner ||
            record.Owner.Pointer != note.Text.Pointer)
            return;
        var minimum = 14f * reference / (rootScale * note.Rect.localScale.y);
        var maximum = 15.5f * reference / (rootScale * note.Rect.localScale.y);
        if (Math.Abs(note.Text.fontSizeMin - minimum) > .01f)
            note.Text.fontSizeMin = minimum;
        if (Math.Abs(note.Text.fontSizeMax - maximum) > .01f)
            note.Text.fontSizeMax = maximum;
        if (!note.Text.enableAutoSizing)
            note.Text.enableAutoSizing = true;
        if (!note.Text.enableWordWrapping)
            note.Text.enableWordWrapping = true;
        if (note.Text.alignment != TextAlignmentOptions.TopLeft)
            note.Text.alignment = TextAlignmentOptions.TopLeft;
        if (note.Text.overflowMode != TextOverflowModes.Overflow)
            note.Text.overflowMode = TextOverflowModes.Overflow;
    }
    private static void RestoreCourtNotes(CourtNoteState state)
    {
        if (!state.Modified)
            return;
        SetCourtSize(state.Background, state.BackgroundSize);
        SetCourtPosition(state.Background, state.BackgroundPosition);
        SetCourtPosition(state.Button, state.ButtonPosition);
        foreach (var note in new[] { state.Plaintiff, state.Defendant })
        {
            if (!note.Text || !note.Rect)
                continue;
            SetCourtSize(note.Rect, note.Size);
            SetCourtPosition(note.Rect, note.Position);
            note.Text.enableAutoSizing = note.AutoSize;
            note.Text.fontSizeMin = note.Min;
            note.Text.fontSizeMax = note.Max;
            note.Text.fontSize = note.FontSize;
            note.Text.enableWordWrapping = note.Wrap;
            note.Text.overflowMode = note.Overflow;
            note.Text.alignment = note.Alignment;
        }
        state.Modified = false;
    }
    private static void SetCourtSize(RectTransform rect, Vector2 size)
    {
        if (rect && (rect.sizeDelta - size).sqrMagnitude > .0001f)
            rect.sizeDelta = size;
    }
    private static void SetCourtPosition(RectTransform rect, Vector2 position)
    {
        if (rect && (rect.anchoredPosition - position).sqrMagnitude > .0001f)
            rect.anchoredPosition = position;
    }
}
