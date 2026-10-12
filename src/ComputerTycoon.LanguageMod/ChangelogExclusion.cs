using UnityEngine;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    // Keep release notes in the game's original language and layout, including
    // future cards. Exclude them before exact, rule or fragment translation.
    // The news window title and navigation buttons remain translatable.
    private static bool IsOriginalChangelog(Component component)
    {
        for (var card = component.transform; card; card = card.parent)
        {
            if (card.name != "release_notes" &&
                !card.name.StartsWith("release_notes (", StringComparison.Ordinal))
                continue;
            var content = card.parent;
            if (content && content.name == "Content" &&
                content.parent?.name == "Viewport" &&
                content.parent.parent?.name == "Scroll View" &&
                content.parent.parent.parent?.name == "NewsWIindow")
                return true;
        }
        return false;
    }
}
