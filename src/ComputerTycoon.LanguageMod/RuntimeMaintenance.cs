using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private static Il2CppArrayBase<Image> _discoveredImages;
    private static int _imageDiscoveryFrame = -1;
    private static readonly List<int> StaleIds = new();

    private static Il2CppArrayBase<Image> DiscoverImagesThisFrame()
    {
        if (_imageDiscoveryFrame != Time.frameCount)
        {
            _discoveredImages = Resources.FindObjectsOfTypeAll<Image>();
            _imageDiscoveryFrame = Time.frameCount;
        }
        return _discoveredImages;
    }

    // Most layout collections prune themselves during refresh. These collections
    // otherwise only see setter callbacks and can retain destroyed scene objects.
    private static void CleanupDestroyedObjects()
    {
        RemoveDestroyed(OfficeActions, state => !state.Owner);
        RemoveDestroyed(PrototypeFilters, state => !state.Record.Owner || !state.Rect);
        RemoveDestroyed(NoSpyExplanations, state => !state.Record.Owner || !state.Rect);
        RemoveDestroyed(Tooltips, state => !state.Owner);
        RemoveDestroyed(CountryCaptions, state => !state.Owner);
        NegotiationsLayouts.RemoveWhere(state => !state.Owner);
    }

    private static void RemoveDestroyed<T>(Dictionary<int, T> states, Func<T, bool> isDestroyed)
    {
        StaleIds.Clear();
        foreach (var entry in states)
            if (isDestroyed(entry.Value))
                StaleIds.Add(entry.Key);
        foreach (var id in StaleIds)
            states.Remove(id);
        StaleIds.Clear();
    }
}
