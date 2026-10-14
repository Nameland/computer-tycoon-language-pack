using System.Text.Json;
using ComputerTycoon.Localization;

internal sealed class LocalizationTests
{
    private readonly string dir;
    private LanguageCatalog catalog = null!;
    private int count;

    internal LocalizationTests(string directory) => dir = directory;

    internal int Run()
    {
        Pack("en", new());
        Pack(
            "fr",
            new()
            {
                ["New Game"] = "Nouvelle partie",
                ["Research"] = "Recherche",
                ["RP per day: 100"] = "RP par jour: 100",
                ["Money: $100"] = "Argent: $100",
                ["Alpha\nBeta"] = "Alpha-fr\nBeta-fr",
                ["Sales"] = "Ventes",
                [" has released a new computer."] = " a sorti un nouvel ordinateur.",
                ["Leave blank"] = "",
            }
        );
        File.WriteAllText(Path.Combine(dir, "broken.json"), "{broken");
        catalog = new LanguageCatalog(dir);
        TestTranslation();
        TestProtectedNames();
        TestReload();
        TestCaptureRules();
        TestLegalCaptions();
        TestLimits();
        return count;
    }

    private void Pack(string id, Dictionary<string, string> strings) =>
        File.WriteAllText(
            Path.Combine(dir, id + ".json"),
            JsonSerializer.Serialize(
                new
                {
                    schemaVersion = 1,
                    id,
                    name = id,
                    nativeName = id,
                    strings,
                }
            )
        );

    private void Eq(string expected, string actual)
    {
        count++;
        if (expected != actual)
        {
            throw new Exception($"Expected [{expected}], got [{actual}]");
        }
    }

    private void TestTranslation()
    {
        Eq("New Game", catalog.Translate("New Game"));
        catalog.SelectLanguage("fr");
        Eq("Nouvelle partie", catalog.Translate("New Game"));
        Eq("\tNouvelle partie \n", catalog.Translate("\tNew Game \n"));
        Eq("Alpha-fr\nBeta-fr", catalog.Translate("Alpha\r\nBeta"));
        Eq("Nouvelle partie", catalog.Translate("New  Game"));
        Eq("Nouvelle partie", catalog.Translate("New\tGame"));
        Eq("Nouvelle partie", catalog.Translate("New\u00a0Game"));
        Eq("Nouvelle partie", catalog.Translate("New\u202fGame"));
        Eq("RP par jour: 250", catalog.Translate("RP per day: 250"));
        Eq("Argent: $1,234", catalog.Translate("Money: $1,234"));
        Eq(
            "<color=#aabbcc>Recherche</color>",
            catalog.Translate("<color=#aabbcc>Research</color>")
        );
        Eq("Ventes: 42", catalog.Translate("Sales: 42"));
        Eq(
            "CustomBrand a sorti un nouvel ordinateur.",
            catalog.Translate("CustomBrand has released a new computer.")
        );
        Eq("Unknown user product", catalog.Translate("Unknown user product"));
    }

    private void TestProtectedNames()
    {
        // Both authored names and explicit clone names occur in notification rules.
        foreach (
            var name in new[]
            {
                "gameMessageNotificationText",
                "mainmenu_old_notificationRed",
                "mainmenu_old_notificationBlue",
            }
        )
        {
            Eq(
                "True",
                ComputerTycoon.LanguageMod.HierarchyNames.Matches(name + "(Clone)", name).ToString()
            );
            Eq(
                "True",
                ComputerTycoon
                    .LanguageMod.HierarchyNames.Matches(name + "(Clone)", name + "(Clone)")
                    .ToString()
            );
            Eq(
                "True",
                ComputerTycoon.LanguageMod.HierarchyNames.Matches(name, name + "(Clone)").ToString()
            );
            Eq(
                "False",
                ComputerTycoon.LanguageMod.HierarchyNames.Matches(name + " (1)", name).ToString()
            );
        }

        foreach (
            var protectedPath in new[]
            {
                "DoozyUI/UI Screens/ComputerList/Panel/Content/ComputerElementLine(Clone)/NameButton/Text",
                "DoozyUI/UI Screens/PrototypesList/Panel/Content/PrototypeElementLine(Clone)/NameButton/Text",
                "DoozyUI/UI Menus/HardwareCompareWindow/Panel/HardwareCompareModalWindow (1)/hardwarename",
                "DoozyUI/HCWindow/Alternative1_Name_lbl",
                "DoozyUI/HCWindow/Alternative2_Name_lbl",
                "DoozyUI/GameOverFuture/InfoPanel/BestSellerComputer_lbl/Computer_picture/Name",
                "DoozyUI/CEO_ReactionWindow/Opponent_id_data/TextBox (1)",
                "DoozyUI/CEO_ReactionWindow/Opponent_id_data/TextBox (2)",
                "DoozyUI/UI Menus/ManagerHire/Points_left_lbl (2)",
                "DoozyUI/UI Menus/ManagerLevelingWindow/Points_left_lbl (2)",
            }
        )
        {
            Eq(
                "True",
                ComputerTycoon.LanguageMod.ModelNameSlots.IsProtected(protectedPath).ToString()
            );
            foreach (var collision in new[] { "Virus", "Research", "Name" })
            {
                Eq(
                    collision,
                    ComputerTycoon.LanguageMod.ModelNameSlots.IsProtected(protectedPath)
                        ? collision
                        : catalog.Translate(collision)
                );
            }
        }

        foreach (
            var ordinaryPath in new[]
            {
                "Root/ComputerList/Panel/NameFilter_tgl/Label",
                "Root/GameDetailsWindow/Name_lbl",
                "Root/CEO_TradeWindow/UsPanelName_lbl",
                "Root/Settings/Name",
                "Root/CountryList/Content/CountryElementLine(Clone)/NameButton/Text",
            }
        )
        {
            Eq(
                "False",
                ComputerTycoon.LanguageMod.ModelNameSlots.IsProtected(ordinaryPath).ToString()
            );
        }
    }

    private void TestReload()
    {
        Eq("Leave blank", catalog.Translate("Leave blank"));
        Eq("AResearchCorp", catalog.Translate("AResearchCorp"));
        catalog.SelectLanguage("en");
        Eq("Research", catalog.Translate("Research"));
        Pack("de", new() { ["Research"] = "Forschung" });
        catalog.Reload();
        catalog.SelectLanguage("de");
        Eq("Forschung", catalog.Translate("Research"));
        if (catalog.Languages.Count != 3 || catalog.Warnings.Count != 1)
        {
            throw new Exception("Language discovery/error isolation failed");
        }

        // An editor can temporarily leave a pack invalid or exclusively locked.
        // Retain its last valid text and selection, then pick up repairs on reload.
        File.WriteAllText(Path.Combine(dir, "de.json"), "{unfinished");
        catalog.Reload();
        Eq("de", catalog.ActiveLanguageId);
        Eq("Forschung", catalog.Translate("Research"));
        Pack("fr", new() { ["Research"] = "Recherche corrigée" });
        catalog.Reload();
        catalog.SelectLanguage("fr");
        Eq("Recherche corrigée", catalog.Translate("Research"));
        catalog.SelectLanguage("de");
        Pack("de", new() { ["Research"] = "Überarbeitete Forschung" });
        using (
            var lockedPack = new FileStream(
                Path.Combine(dir, "de.json"),
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None
            )
        )
        {
            catalog.Reload();
            Eq("de", catalog.ActiveLanguageId);
            Eq("Forschung", catalog.Translate("Research"));
        }

        catalog.Reload();
        Eq("de", catalog.ActiveLanguageId);
        Eq("Überarbeitete Forschung", catalog.Translate("Research"));
        // Previously loaded files need not have an id that matches their basename.
        File.Move(Path.Combine(dir, "de.json"), Path.Combine(dir, "custom.json"));
        catalog.Reload();
        File.WriteAllText(Path.Combine(dir, "custom.json"), "{unfinished");
        catalog.Reload();
        Eq("de", catalog.ActiveLanguageId);
        Eq("Überarbeitete Forschung", catalog.Translate("Research"));
        File.Delete(Path.Combine(dir, "custom.json"));
        catalog.Reload();
        Eq("en", catalog.ActiveLanguageId);
    }

    private void TestCaptureRules()
    {
        // Isolated rule fixtures exercise capture translation independently of shipped text.
        var ruleDir = Path.Combine(dir, "capture-rules");
        Directory.CreateDirectory(ruleDir);
        File.WriteAllText(
            Path.Combine(ruleDir, "fr.json"),
            JsonSerializer.Serialize(
                new LanguagePack
                {
                    Id = "fr",
                    NativeName = "Français",
                    Strings = new()
                    {
                        ["Norway"] = "Norvège",
                        ["Dollarland"] = "Pays $1 ${country} {0}",
                    },
                    Rules = new()
                    {
                        new()
                        {
                            Pattern = @"^Country: (?<country>.+)$",
                            Replacement = "Pays: ${country}",
                            TranslateGroups = new() { "country" },
                        },
                        new()
                        {
                            Pattern = @"^Raw: (?<country>.+)$",
                            Replacement = "Sans traduction: ${country}",
                        },
                        new()
                        {
                            Pattern = @"^maybe(?<country>Norway)?$",
                            Replacement = "[${country}]",
                            TranslateGroups = new() { "country" },
                        },
                        new()
                        {
                            Pattern = @"^(?<whole>loop)$",
                            Replacement = "${whole}!",
                            TranslateGroups = new() { "whole" },
                        },
                        new()
                        {
                            Pattern = @"^bad-missing(?<known>.*)$",
                            Replacement = "${known}",
                            TranslateGroups = new() { "missing" },
                        },
                        new()
                        {
                            Pattern = @"^bad-null-list$",
                            Replacement = "invalid",
                            TranslateGroups = null!,
                        },
                        new()
                        {
                            Pattern = @"^bad-null-name(?<known>.*)$",
                            Replacement = "${known}",
                            TranslateGroups = new() { null! },
                        },
                        new()
                        {
                            Pattern = @"^bad-empty-name(?<known>.*)$",
                            Replacement = "${known}",
                            TranslateGroups = new() { "" },
                        },
                        new()
                        {
                            Pattern = @"^bad-zero$",
                            Replacement = "${0}",
                            TranslateGroups = new() { "0" },
                        },
                    },
                }
            )
        );
        var capture = new LanguageCatalog(ruleDir);
        capture.SelectLanguage("fr");
        Eq("5", capture.Warnings.Count.ToString());
        foreach (var warning in capture.Warnings)
        {
            count++;
            if (!warning.Contains("translateGroups", StringComparison.Ordinal))
            {
                throw new Exception("Missing capture validation diagnostic: " + warning);
            }
        }

        Eq("Pays: Norvège", capture.Translate("Country: Norway"));
        Eq("Pays: Pays $1 ${country} {0}", capture.Translate("Country: Dollarland"));
        Eq(
            "Pays: Unknown $1 ${country} {0}",
            capture.Translate("Country: Unknown $1 ${country} {0}")
        );
        Eq("Sans traduction: Norway", capture.Translate("Raw: Norway"));
        Eq("[]", capture.Translate("maybe")); // An unmatched optional group is valid, unlike an absent group name.
        Eq("[Norvège]", capture.Translate("maybeNorway"));
        Eq("loop!!!!!", capture.Translate("loop")); // Four delegated levels, then the raw-capture terminal application.
        foreach (
            var rejected in new[]
            {
                "bad-missingX",
                "bad-null-list",
                "bad-null-nameX",
                "bad-empty-nameX",
                "bad-zero",
            }
        )
        {
            Eq(rejected, capture.Translate(rejected));
        }

        capture.SelectLanguage("en");
        Eq("Country: Dollarland", capture.Translate("Country: Dollarland"));
        Eq("loop", capture.Translate("loop"));
    }

    private void TestLegalCaptions()
    {
        // The correction depends on field identity and cause, not one language.
        foreach (var language in new[] { "en", "uk", "de", "fr", "zh-CN" })
        {
            foreach (var causeField in new[] { false, true })
            {
                foreach (var defamation in new[] { false, true })
                {
                    Eq(
                        language != "en" && causeField && defamation
                            ? "Defamation Of CEO"
                            : "Breach Of Contract",
                        ComputerTycoon.LanguageMod.LegalCaptionPolicy.TranslationSource(
                            language,
                            "Breach Of Contract",
                            causeField,
                            defamation
                        )
                    );
                }
            }
        }
    }

    private void TestLimits()
    {
        // Exercise editable packs with an arbitrary id, invalid metadata, and long
        // dynamic text. These cases do not require the game or shipped translations.
        var limitsDir = Path.Combine(dir, "limits");
        Directory.CreateDirectory(limitsDir);
        File.WriteAllText(
            Path.Combine(limitsDir, "qaa.json"),
            JsonSerializer.Serialize(
                new LanguagePack
                {
                    Id = "qaa",
                    Name = "Test",
                    NativeName = "Test",
                    Strings = new() { ["Hello"] = "Translated", ["X"] = new string('Z', 20000) },
                    Rules = new()
                    {
                        new()
                        {
                            Pattern = @"^repeat:(?<value>.*)$",
                            Replacement = string.Concat(Enumerable.Repeat("${value}", 100)),
                        },
                        new()
                        {
                            Pattern = @"^group:(?<value>X)$",
                            Replacement = "${value}${value}",
                            TranslateGroups = new() { "value" },
                        },
                        new() { Pattern = @"^(a+)+$", Replacement = "Slow rule" },
                    },
                }
            )
        );
        File.WriteAllText(
            Path.Combine(limitsDir, "invalid.json"),
            "{\"schemaVersion\":99,\"id\":\"bad\",\"nativeName\":\"Bad\"}"
        );
        var limits = new LanguageCatalog(limitsDir);
        Eq("1", limits.Warnings.Count.ToString());
        limits.SelectLanguage("qaa");
        Eq("Translated", limits.Translate("Hello"));
        var oversized = new string('a', 40000);
        Eq(oversized, limits.Translate(oversized));
        var repeat = "repeat:" + new string('b', 2000);
        Eq(repeat, limits.Translate(repeat));
        Eq("group:X", limits.Translate("group:X"));
        Eq("X\nX", limits.Translate("X\nX"));
        Eq(new string('Z', 20000) + " (12 RP)", limits.TranslateTechnologyOffer("X (12 RP)"));
        var richOffer = "<b>X</b><i>X</i>";
        Eq(richOffer, limits.TranslateTechnologyOffer(richOffer));
        var manyOffers = string.Concat(Enumerable.Repeat("<b>Hello</b>", 300));
        Eq(manyOffers, limits.TranslateTechnologyOffer(manyOffers));
        Eq(
            "<color=green>Translated (12 RP)</color>",
            limits.TranslateTechnologyOffer("<color=green>Hello (12 RP)</color>")
        );
        var slowInput = new string('a', 20000) + "!";
        Eq(slowInput, limits.Translate(slowInput));
        Eq("2", limits.Warnings.Count.ToString());
        Eq(slowInput + "!", limits.Translate(slowInput + "!"));
        Eq("2", limits.Warnings.Count.ToString()); // The timed-out rule stays disabled.
        for (var index = 0; index < 16000; index++)
        {
            limits.Translate(new string('b', 300) + index);
        }

        var flags =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var characterCount = (int)
            typeof(LanguageCatalog).GetField("cachedCharacters", flags)!.GetValue(limits)!;
        var entries = (System.Collections.IDictionary)
            typeof(LanguageCatalog).GetField("cache", flags)!.GetValue(limits)!;
        Eq("True", (characterCount <= 2 * 1024 * 1024 && entries.Count <= 12000).ToString());
        Eq("Translated", limits.Translate("Hello"));
        limits.SelectLanguage("en");
        Eq("Hello", limits.Translate("Hello"));
    }
}
