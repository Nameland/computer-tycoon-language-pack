using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WorldMapStrategyKit;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private static bool CanTranslate(Component component, string candidate = null)
    {
        if (!component || !component.gameObject.scene.IsValid())
        {
            return false;
        }

        if (IsCountryMapText(component))
        {
            return false;
        }

        if (IsOriginalChangelog(component))
        {
            return false;
        }

        var tmpInput = ParentComponent<TMP_InputField>(component);
        if (
            tmpInput
            && tmpInput.textComponent
            && tmpInput.textComponent.Pointer == component.Pointer
        )
        {
            return false;
        }

        var legacyInput = ParentComponent<InputField>(component);
        if (
            legacyInput
            && legacyInput.textComponent
            && legacyInput.textComponent.Pointer == component.Pointer
        )
        {
            return false;
        }

        // Input placeholders and decorative labels are presentation, not the value.
        // The editable text above is protected even when its value equals a dictionary key.
        if (tmpInput || legacyInput)
        {
            return !InsideLanguageMenu(component);
        }

        if (IsMapCompanyPlaceholder(component, candidate))
        {
            return !InsideLanguageMenu(component);
        }

        if (ModelNameSlots.IsProtected(Hierarchy(component)))
        {
            return false;
        }

        // These narrow, explicit name slots show player-assigned/model names. Do not
        // use a generic "Name" match: that would also suppress ordinary column labels.
        var node = component.transform;
        for (var depth = 0; depth < 2 && node; depth++, node = node.parent)
        {
            var slot = node
                .name.Replace("_", "")
                .Replace("-", "")
                .Replace(" ", "")
                .ToLowerInvariant();
            if (
                slot.Contains("playername")
                || slot.Contains("companyname")
                || slot.Contains("productname")
                || slot.Contains("computername")
                || slot.Contains("savename")
                || slot.Contains("prototypename")
                || slot.Contains("managername")
                || slot.Contains("modelname")
            )
            {
                // A name-entry row also contains an ordinary heading beside the input.
                // Inspect the actual input component rather than excluding the whole row.
                if (HasNameEntryInput(node) || (node.parent && HasNameEntryInput(node.parent)))
                {
                    return !InsideLanguageMenu(component);
                }

                return false;
            }
        }

        // Placeholders remain translatable; editable text stays exactly as the user typed.
        return !InsideLanguageMenu(component);
    }

    private static bool IsMapCompanyPlaceholder(Component component, string candidate)
    {
        // Whitelist only this shipped placeholder in this exact map label. The
        // setter supplies its incoming value: reading component.text there would
        // still see the old placeholder while a real company name is being set.
        if (!HasContextPath(component, "MapControlWindow", "Panel", "OpponentCompanyNameSmall_lbl"))
        {
            return false;
        }

        if (candidate is null)
        {
            var tmp = component.TryCast<TMP_Text>();
            candidate = tmp is not null ? tmp.text : component.TryCast<Text>()?.text;
        }

        if (candidate is null)
        {
            return false;
        }

        // A currently displayed translation must resolve to its original source
        // so that switching back to English can restore the placeholder. Instance ids
        // alone are insufficient because Unity can reuse them for another object.
        if (
            Texts.TryGetValue(component.GetInstanceID(), out var record)
            && record.Owner
            && record.Owner.Pointer == component.Pointer
            && candidate == record.Rendered
        )
        {
            candidate = record.Source;
        }

        return string.Equals(candidate, "Opponent's Company Name", StringComparison.Ordinal);
    }

    private static bool HasNameEntryInput(Component component) =>
        component.GetComponentInChildren(Il2CppType.Of<TMP_InputField>(), true)
        || component.GetComponentInChildren(Il2CppType.Of<InputField>(), true);

    private static T ParentComponent<T>(Component component)
        where T : Component
    {
        // Unity's unstripped generic bool overload has invalid generic constraints.
        // The native non-generic method and our correctly constrained cast are safe.
        var found = component.GetComponentInParent(Il2CppType.Of<T>(), true);
        return found ? found.TryCast<T>() : null;
    }

    private static bool IsCountryMapText(Component component)
    {
        if (component.TryCast<TextMeshPro>() is null)
        {
            return false;
        }

        var parent = component.transform.parent;
        return parent
            && parent.name == "TextRoot"
            && parent.parent
            && parent.parent.name == "OverlayLayer"
            && ParentComponent<WMSK>(component);
    }
}
