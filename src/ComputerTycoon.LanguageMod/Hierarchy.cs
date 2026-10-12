using UnityEngine;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private static string Hierarchy(Component component)
    {
        var names = new List<string>();
        for (var node = component.transform; node && names.Count < 24; node = node.parent)
            names.Add(node.name);
        names.Reverse();
        return string.Join("/", names);
    }
}
