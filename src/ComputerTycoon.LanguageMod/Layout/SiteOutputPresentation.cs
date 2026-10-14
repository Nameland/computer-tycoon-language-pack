using System.Globalization;
using UnityEngine;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private static CTGUISiteControl SiteOutputOwner;
    private static float _nextSiteOutputDiscovery;

    private static void RefreshSiteOutputPresentation()
    {
        if (!UsesLocalizedLayout)
        {
            return;
        }

        if (!SiteOutputOwner && Time.unscaledTime >= _nextSiteOutputDiscovery)
        {
            _nextSiteOutputDiscovery = Time.unscaledTime + 2f;
            SiteOutputOwner = Resources
                .FindObjectsOfTypeAll<CTGUISiteControl>()
                .FirstOrDefault(x => x && x.gameObject.scene.IsValid());
        }

        var owner = SiteOutputOwner;
        if (
            !owner
            || !owner.gameObject.activeInHierarchy
            || !owner.OutPut
            || !owner.OutPut.isActiveAndEnabled
        )
        {
            return;
        }

        var mode = owner.viewMode;
        if (
            mode != CTGUISiteControl.ViewModeE.Factory
            && mode != CTGUISiteControl.ViewModeE.Logistic
        )
        {
            return;
        }

        var gm = owner.GM;
        if (
            !gm
            || !gm.SPM
            || gm.Game?.Player?.Sites == null
            || gm.currentSiteIndex < 0
            || gm.currentSiteIndex >= gm.Game.Player.Sites.Count
        )
        {
            return;
        }

        var text = owner.OutPut;
        if (
            !Texts.TryGetValue(text.GetInstanceID(), out var record)
            || record.Owner.Pointer != text.Pointer
        )
        {
            return;
        }

        // LabelRefresh61B278..61B282 skips these two branches after selecting
        // their PP/LP suffix. Match the existing Laboratory call4A6990 exactly:
        // getDepartmentProd(mode,site,false), not a new production formula.
        var value = gm.SPM.getDepartmentProd(
            mode,
            gm.Game.Player.Sites[gm.currentSiteIndex],
            false
        );
        if (!float.IsFinite(value))
        {
            return;
        }

        var rendered =
            value.ToString(
                mode == CTGUISiteControl.ViewModeE.Factory ? "N0" : "N1",
                CultureInfo.InvariantCulture
            ) + (" " + Catalog.Translate(mode == CTGUISiteControl.ViewModeE.Factory ? "PP" : "LP"));
        if (text.text != record.Rendered)
        {
            record.Source = text.text;
        }

        if (text.text == rendered)
        {
            record.Rendered = rendered;
            return;
        }

        var wasWriting = _writing;
        _writing = true;
        try
        {
            text.text = rendered;
            record.Rendered = rendered;
        }
        finally
        {
            _writing = wasWriting;
        }
        // Source stays the exact native caption (including its stale value).
        // The regular language-switch refresh therefore restores native EN.
    }
}
