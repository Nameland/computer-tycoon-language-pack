using System;

namespace ComputerTycoon.LanguageMod;

// Pure path policy shared with regression tests. These are confirmed model-name
// slots, not a blanket exclusion of labels containing the word "Name".
internal static class ModelNameSlots
{
    internal static bool IsProtected(string hierarchy)
    {
        var path = hierarchy.Replace("(Clone)", "", StringComparison.Ordinal);
        if (
            (
                path.Contains("/ComputerList/", StringComparison.Ordinal)
                && path.EndsWith("/ComputerElementLine/NameButton/Text", StringComparison.Ordinal)
            )
            || (
                path.Contains("/PrototypesList/", StringComparison.Ordinal)
                && path.EndsWith("/PrototypeElementLine/NameButton/Text", StringComparison.Ordinal)
            )
        )
        {
            return true;
        }

        if (
            path.Contains("/HardwareCompareWindow/", StringComparison.Ordinal)
            && path.EndsWith("/hardwarename", StringComparison.Ordinal)
        )
        {
            return true;
        }

        if (
            path.EndsWith("/Alternative1_Name_lbl", StringComparison.Ordinal)
            || path.EndsWith("/Alternative2_Name_lbl", StringComparison.Ordinal)
        )
        {
            return true;
        }

        if (
            path.Contains("/BestSellerComputer_lbl/", StringComparison.Ordinal)
            && path.EndsWith("/Computer_picture/Name", StringComparison.Ordinal)
        )
        {
            return true;
        }

        if (
            path.EndsWith("/Opponent_id_data/TextBox (1)", StringComparison.Ordinal)
            || path.EndsWith("/Opponent_id_data/TextBox (2)", StringComparison.Ordinal)
        )
        {
            return true;
        }

        // These misleading prefab names are the controller's Name_lbl field.
        if (
            path.EndsWith("/ManagerHire/Points_left_lbl (2)", StringComparison.Ordinal)
            || path.EndsWith("/ManagerLevelingWindow/Points_left_lbl (2)", StringComparison.Ordinal)
        )
        {
            return true;
        }

        return false;
    }
}
