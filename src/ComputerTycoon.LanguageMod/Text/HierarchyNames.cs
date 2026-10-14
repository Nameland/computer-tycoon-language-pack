namespace ComputerTycoon.LanguageMod;

internal static class HierarchyNames
{
    // Rules can name either the authored prefab or its instantiated clone.
    internal static bool Matches(string actual, string expected) =>
        string.Equals(PrefabName(actual), PrefabName(expected), StringComparison.Ordinal);

    private static string PrefabName(string name) =>
        name.EndsWith("(Clone)", StringComparison.Ordinal) ? name[..^7] : name;
}
