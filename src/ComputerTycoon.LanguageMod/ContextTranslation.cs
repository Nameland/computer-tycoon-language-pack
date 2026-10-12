using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    // Verified in Computer Tycoon settings; not Unity's six default preset names.
    private static readonly string[] QualityPresets = { "Fast", "Good", "Beautiful", "Full" };
    private static readonly string[] EconomyLevels = { "Poverty", "Poor", "Average", "Rich", "Filthy Rich" };
    private static readonly string[] EconomyToggles = { "Poverty_tgl", "Poor_tgl", "Average_tgl", "Rich_tgl", "Filthy_Rich_tgl" };

    private static string TranslateForComponent(Component component, string source)
    {
        if (Catalog.ActiveLanguageId != "en")
        {
            // Native logistics HUD's expansion country and signed LP delta.
            // The synthetic rule is reachable only here, never from model-name slots.
            if (source.Contains(" -> ", StringComparison.Ordinal) && HasContextPath(component, "MainLogisticBar", "Text"))
                return TranslateContextOrSource("Logistics expansion: ", source);
            // Native LegalResultScreen 436197..4361A1 maps both defCEO=3 and
            // breachOfContract=4 to "Breach Of Contract". Correct only this exact
            // model-backed cause field; retain record.Source for native EN restore.
            if (source == "Breach Of Contract")
            {
                var court = ParentComponent<CTGUI_LegalResultScreen>(component);
                var ownField = court && court.CauseOfAction_value && court.CauseOfAction_value.Pointer == component.Pointer;
                var defamation = court && court.localLegalAction != null &&
                    court.localLegalAction.CauseOfAction == CTGUI_LegalActionScreen.CTLegalActionClass.Legal_CauseOfAction.defCEO;
                var captionSource = LegalCaptionPolicy.TranslationSource(Catalog.ActiveLanguageId, source, ownField, defamation);
                if (captionSource != source)
                    return Catalog.Translate(captionSource);
            }
            if (TryTranslateEventLabel(component, source, out var eventLabel))
                return eventLabel;
            if (TryTranslateScreenLabel(component, source, out var screenLabel))
                return screenLabel;
            if (source is "Discontinue Task!" or "Cancel Task!")
            {
                var row = ParentComponent<CTGUI_Manager_MiniWindow>(component);
                if (row && row.activityButton && component.name == "Text" && component.transform.parent &&
                    component.transform.parent.Pointer == row.activityButton.transform.Pointer)
                    return TranslateContextOrSource("Manager task action: ", source);
            }
            // Numeric column headings in the active/passive tables, not prose labels.
            if ((source == "Skill Value" && (HasContextPath(component, "ManagerTaskActiveList", "Text (2)") || HasContextPath(component, "ManagerTaskPassiveList", "Text (2)"))) ||
                (source == "UpFront Cost" && HasContextPath(component, "ManagerTaskActiveList", "Text (3)")))
                return TranslateContextOrSource("Manager task column: ", source);
            if (source == "5 is the max. level!" && HasContextPath(component, "PlaceDetails", "maxProd_value"))
                return TranslateContextOrSource("Department next level: ", source);
            if (source == "MOBO/Circuits" && HasContextPath(component, "ChoosePrototypeTypeWindow", "Panel",
                "Image (1)", "GameObject", "New UIButton (1)", "Text"))
                return TranslateContextOrSource("Hardware category card: ", source);
            // Ex./In. toggle exclusion/inclusion in CT_GUI_Proto_TechButtonClick.refreshLabel.
            // These narrow controls use minus/plus rather than grammatical abbreviations.
            if (source is "Ex." or "In.")
            {
                var techButton = ParentComponent<CT_GUI_Proto_TechButtonClick>(component);
                if (techButton && techButton.exclude_lbl && techButton.exclude_lbl.Pointer == component.Pointer)
                    return TranslateContextOrSource("Hardware exclude button: ", source);
            }
            // refreshLabel writes Obs. only to new_lbl (native field offset 0x30).
            // Two prefab children share the same name, so use the owning field identity.
            if (source == "Obs.")
            {
                var techButton = ParentComponent<CT_GUI_Proto_TechButtonClick>(component);
                if (techButton && techButton.new_lbl && techButton.new_lbl.Pointer == component.Pointer)
                    return TranslateContextOrSource("Hardware obsolete badge: ", source);
            }
            // refreshControl writes Free to salary/hiring cost fields (0x90/0x98).
            // Compare component identity so idle-state labels keep their usual meaning.
            if (source == "Free")
            {
                var manager = ParentComponent<CTGUI_ManagerControl>(component);
                if (manager &&
                    ((manager.MonthlySalary_lbl && manager.MonthlySalary_lbl.Pointer == component.Pointer) ||
                     (manager.JoinsFor_lbl && manager.JoinsFor_lbl.Pointer == component.Pointer)))
                    return TranslateContextOrSource("Manager hiring cost: ", source);
            }
            if (source == "Outstanding Techs" && IsCompanySelectorTechLabel(component))
                return TranslateContextOrSource("Company selector: ", source);
            if (source == "for" && HasContextPath(component, "CEO_MoneySetter", "TextBox (2)"))
                return TranslateContextOrSource("Payment duration: ", source);
            // LoanWindow's Increase opens the one-time credit increase panel;
            // the misleading prefab button name is RejectLoan_btn (level2 98518).
            if (source == "Increase" && HasContextPath(component, "LoanWindow", "Panel", "LoanElement", "RejectLoan_btn", "Text"))
                return TranslateContextOrSource("Loan action: ", source);
            // Shared department current/next-level popup, level2 TMP 97836.
            // Its adjacent dailyCost_value already includes the /day suffix.
            if (source == "Daily Cost" && HasContextPath(component, "PlaceDetails", "Text"))
                return TranslateContextOrSource("Department level: ", source);
            if (QualityPresets.Contains(source))
            {
                var tmp = ParentComponent<TMP_Dropdown>(component);
                var legacy = tmp ? null : ParentComponent<Dropdown>(component);
                var matches = tmp ? tmp.options.Count == QualityPresets.Length :
                    legacy && legacy.options.Count == QualityPresets.Length;
                for (var index = 0; matches && index < QualityPresets.Length; index++)
                    matches = (tmp ? tmp.options[index].text : legacy.options[index].text) == QualityPresets[index];
                if (matches)
                    return TranslateContextOrSource("Graphics quality: ", source);
            }

            if (source == "Wealth" && HasContextPath(component, "CountryList", "Panel", "price_tgl (1)", "Label"))
                return TranslateContextOrSource("Market table: ", source);
            if ((source is "Unused" or "No Market & Site" or "Market & No Site" or "Market & Site") &&
                IsMarketTableRowLabel(component))
                return TranslateContextOrSource("Market status: ", source);

            // Country detail/government cards and the generated country table
            // can expose WealthE names. Normalize only within verified economy UI;
            // keep record.Source untouched so EN restores FilthyRich verbatim.
            if (source == "Unknown" && IsDynamicEconomyValue(component))
                return TranslateContextOrSource("Economy level: ", source);
            var economySource = source == "FilthyRich" ? "Filthy Rich" : source;
            var economyIndex = Array.IndexOf(EconomyLevels, economySource);
            if (economyIndex >= 0 && IsEconomyLabel(component, economyIndex))
                return TranslateContextOrSource("Economy level: ", economySource);

            if (source == "New" &&
                (HasContextPath(component, "ComputerList", "Panel", "NewPrototypeButton", "Text") ||
                 HasContextPath(component, "PrototypesList", "Panel", "NewPrototypeButton", "Text")))
                return TranslateContextOrSource("Create button: ", source);
        }
        return Catalog.Translate(source);
    }

    private static string TranslateContextOrSource(string prefix, string source)
    {
        // Context keys are optional. A missing key must not leak its synthetic prefix
        // or change the ordinary meaning of an unrelated label such as "Average".
        var key = prefix + source;
        var result = Catalog.Translate(key);
        return result != key && !result.StartsWith(prefix, StringComparison.Ordinal)
            ? result : Catalog.Translate(source);
    }

    private static bool IsEconomyLabel(Component component, int index)
    {
        if (IsStatisticsEconomyLegend(component))
            return true;
        if (IsDynamicEconomyValue(component))
            return true;
        // Exact ancestor/label sequences verified in 0.9.9.16 level2 and resources.assets.
        // The ordinal labels belong to the five economy columns, not arbitrary averages.
        if (HasContextPath(component, "ComputerList", "Panel", "PriceDetailWindow", $"Title ({index + 5})") ||
            HasContextPath(component, "ProductCustomerWindow", "Panel", $"Text ({index + 8})") ||
            HasContextPath(component, "ProductCustomerWindow (1)", "Panel", $"Text ({index + 8})"))
            return true;

        var discount = index == 0 ? "poverty_discount" : $"poverty_discount ({index})";
        if (HasContextPath(component, "MarketResearchWindow", "Panel", discount, "Text") ||
            HasContextPath(component, "MarketResearchWindow (1)", "Panel", discount, "Text") ||
            HasContextPath(component, "CountryList", "Panel", "Panel", EconomyToggles[index], "Label") ||
            HasContextPath(component, "CountryList", "Tab2", "Panel", "Panel", EconomyToggles[index], "Label"))
            return true;

        return HasContextPath(component, "GlobalMarketibilityReport", "Panel", $"Market_Size_lbl ({index + 6})") ||
            HasContextPath(component, "GlobalMarketibilityReport", "Panel", $"Market_Size_lbl ({index + 18})") ||
            HasContextPath(component, "GlobalMarketibilityReport", "Panel", "AlternativesPanel", "Text");
    }

    private static bool IsDynamicEconomyValue(Component component)
    {
        // 0.9.9.16 level2 TMP 98664 and 99612, plus the existing list prefabs.
        // Source-word checks alone would mistranslate Average in other contexts.
        return HasContextPath(component, "CountryDetailsWindow", "upper_box", "WealthValue_lbl") ||
               HasContextPath(component, "GovWindow", "Panel", "upper_box", "cWealth_lvalue") ||
               HasContextPath(component, "SiteManagementList_Element", "Wealth_val_lbl") ||
               (component.name == "Economy" && IsMarketTableRowLabel(component));
    }

    private static bool IsMarketTableRowLabel(Component component)
    {
        if (!component || !component.transform.parent)
            return false;
        // Observed generated table path; clone suffix is handled by HasContextPath.
        return HasContextPath(component.transform.parent, "CountryList", "Panel", "Scroll View", "Viewport",
            "Content", "CountryElementLine 1");
    }
    private static bool IsCompanySelectorTechLabel(Component component)
    {
        if (!component || component.name != "TechRatio_lbl" || !component.transform.parent)
            return false;
        var panel = component.transform.parent.name;
        // Twelve exact portrait panels in CompanyChooserWindow, reused for talks
        // and court selection. The separate CEO connections screen is excluded.
        var verified = panel == "Player_Selector_Panel";
        for (var index = 1; !verified && index <= 11; index++)
            verified = panel == $"Player_Selector_Panel ({index})";
        return verified && HasContextPath(component, "CompanyChooserWindow", "Panel (1)", panel, "TechRatio_lbl");
    }
    private static bool HasContextPath(Component component, params string[] names)
    {
        if (!component)
            return false;
        var node = component.transform;
        for (var index = names.Length - 1; index >= 0; index--)
        {
            if (!node)
                return false;
            var name = node.name;
            // Instantiated list rows retain their prefab name plus Unity's clone suffix.
            if (name.EndsWith("(Clone)", StringComparison.Ordinal))
                name = name[..^7];
            if (!string.Equals(name, names[index], StringComparison.Ordinal))
                return false;
            node = node.parent;
        }
        return true;
    }
}
