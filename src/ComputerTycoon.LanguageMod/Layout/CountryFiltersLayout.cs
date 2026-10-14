using UnityEngine;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private static bool IsCountryFilterText(Component component)
    {
        var node = component.transform;
        while (node && node.name != "CountryList")
        {
            node = node.parent;
        }

        if (!node || node.parent?.name != "UI Screens")
        {
            return false;
        }

        var panel = node.Find("Panel/Panel");
        return panel && component.transform.IsChildOf(panel);
    }
}
