using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private static Il2CppArrayBase<Image> _discoveredImages;
    private static int _imageDiscoveryFrame = -1;
    private static readonly List<int> StaleIds = new();
    private static readonly Action[] LayoutRefreshers =
    {
        RefreshStatisticsLayouts,
        RefreshManagerTaskHeaders,
        RefreshProductFiltersLayouts,
        RefreshHardwareTechnologyLabelLayouts,
        RefreshCompactDesignLabels,
        RefreshScreenLayouts,
        RefreshResearchDetailsLayouts,
        RefreshManagerNameLayouts,
        RefreshManagerRowLayouts,
        RefreshEventPresentationLayouts,
        RefreshCourtNoteLayouts,
        RefreshSmallEventLabelLayouts,
        RefreshSiteLabelLayouts,
        RefreshSiteOutputPresentation,
        RefreshNotificationRows,
    };

    private static void RefreshPresentationLayouts()
    {
        foreach (var refresh in LayoutRefreshers)
        {
            RunRefresh(refresh);
        }
    }

    private static void RunRefresh(Action refresh)
    {
        try
        {
            refresh();
        }
        catch (Exception error)
        {
            Report(refresh.Method.Name, error);
        }
    }

    // Snapshot the collection because Unity callbacks may register more labels
    // while a layout is being applied. A stale native object must affect only
    // its own entry, including failures in the liveness check itself.
    private static void RefreshTrackedStates<T>(
        Dictionary<int, T> states,
        Func<T, bool> isAlive,
        Action<T> refresh,
        string context
    )
    {
        foreach (var pair in states.ToArray())
        {
            try
            {
                if (!isAlive(pair.Value))
                {
                    states.Remove(pair.Key);
                    continue;
                }

                refresh(pair.Value);
            }
            catch (Exception error)
            {
                Report(context, error);
            }
        }
    }

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
        RemoveDestroyed(NoSpyExplanations, state => !state.Record.Owner || !state.Rect);
        RemoveDestroyed(Tooltips, state => !state.Owner);
        MapCaptions.CleanupDestroyedMaps();
        NegotiationsLayouts.RemoveWhere(state => !state.Owner);
    }

    private static void RemoveDestroyed<T>(Dictionary<int, T> states, Func<T, bool> isDestroyed)
    {
        StaleIds.Clear();
        foreach (var entry in states)
        {
            if (isDestroyed(entry.Value))
            {
                StaleIds.Add(entry.Key);
            }
        }

        foreach (var id in StaleIds)
        {
            states.Remove(id);
        }

        StaleIds.Clear();
    }
}
