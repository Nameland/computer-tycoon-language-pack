using UnityEngine;

namespace ComputerTycoon.LanguageMod;

internal static partial class Runtime
{
    private static float _mouseBlockedUntil = float.NegativeInfinity;
    internal static bool BlockGameMouse => _showWindow || Time.unscaledTime < _mouseBlockedUntil;

    private static void GuardWindowMouse(bool visible)
    {
        // Consume the release as well: closing the chooser must not finish a click in
        // the research tree underneath it. IMGUI reads its own Event queue, not these APIs.
        if (_showWindow && !visible)
        {
            _mouseBlockedUntil = Time.unscaledTime + .2f;
        }
    }
}
