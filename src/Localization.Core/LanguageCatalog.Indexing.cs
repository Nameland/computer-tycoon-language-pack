using System.Text.RegularExpressions;

namespace ComputerTycoon.Localization;

/// <summary>Loads editable language packs and translates display text without network access.</summary>
public sealed partial class LanguageCatalog
{
    public void SelectLanguage(string id)
    {
        if (!packs.TryGetValue(id, out var pack))
        {
            throw new ArgumentException($"Unknown language: {id}", nameof(id));
        }

        ActiveLanguageId = pack.Id;
        cache.Clear();
        cacheOrder.Clear();
        cachedCharacters = 0;
        timedOutRules.Clear();
        normalized.Clear();
        rules.Clear();
        numericRules.Clear();
        fragments.Clear();
        currencyPrefixes.Clear();
        // English uses the game's original text, including when a template is installed.
        if (ActiveLanguageId == "en")
        {
            return;
        }

        var conflicts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in pack.Strings)
        {
            if (string.IsNullOrEmpty(entry.Value) || entry.Key == entry.Value)
            {
                continue;
            }

            try
            {
                var key = Normalize(entry.Key);
                if (normalized.TryGetValue(key, out var old) && old != entry.Value.Trim())
                {
                    conflicts.Add(key);
                }
                else
                {
                    normalized[key] = entry.Value.Trim();
                }

                if (
                    entry.Key.Length >= 12
                    && (char.IsWhiteSpace(entry.Key[0]) || char.IsWhiteSpace(entry.Key[^1]))
                )
                {
                    fragments.Add(entry);
                }

                // Native event/loan messages append a formatted amount directly
                // after '$', without the whitespace used by ordinary fragments.
                if (entry.Key.Length >= 12 && entry.Key.EndsWith('$'))
                {
                    currencyPrefixes.Add(entry);
                }

                AddNumericRule(entry.Key, entry.Value);
            }
            catch (RegexMatchTimeoutException)
            {
                // Exact lookup remains usable even if deriving a dynamic rule fails.
                if (warnings.Count < 256)
                {
                    warnings.Add(
                        $"Skipped dynamic text matching for an entry in {pack.Id} after a regex timeout."
                    );
                }
            }
        }

        foreach (var key in conflicts)
        {
            normalized.Remove(key);
        }

        fragments = fragments.OrderByDescending(e => e.Key.Length).ToList();
        currencyPrefixes = currencyPrefixes.OrderByDescending(e => e.Key.Length).ToList();
        foreach (var rule in pack.Rules)
        {
            try
            {
                if (rule is null || string.IsNullOrEmpty(rule.Pattern) || rule.Replacement is null)
                {
                    throw new ArgumentException("Rule pattern/replacement must be strings.");
                }

                if (rule.Pattern.Length > 4096 || rule.Replacement.Length > MaxTextLength)
                {
                    throw new ArgumentException("Rule pattern or replacement is too long.");
                }

                if (!rule.Pattern.StartsWith('^') || !rule.Pattern.EndsWith('$'))
                {
                    throw new ArgumentException("Rules must be anchored with ^ and $.");
                }

                var regex = new Regex(rule.Pattern, RegexOptions.CultureInvariant, RegexTimeout);
                if (
                    rule.TranslateGroups is null
                    || rule.TranslateGroups.Any(name =>
                        string.IsNullOrEmpty(name)
                        || name == "0"
                        || !regex.GetGroupNames().Contains(name)
                    )
                )
                {
                    throw new ArgumentException(
                        "translateGroups must name existing capture groups other than 0."
                    );
                }

                rules.Add(
                    (
                        regex,
                        rule.Replacement,
                        rule.TranslateGroups.Distinct(StringComparer.Ordinal).ToArray()
                    )
                );
            }
            catch (ArgumentException ex)
            {
                if (warnings.Count < 256)
                {
                    warnings.Add($"Rule in {pack.Id}: {ex.Message}");
                }
            }
        }
    }

    private void AddNumericRule(string source, string translation)
    {
        if (
            source.Length > 240
            || source.Contains('<')
            || source.Contains("Version", StringComparison.OrdinalIgnoreCase)
        )
        {
            return;
        }

        // Single-token identifiers are never dynamic counters.
        if (!source.Any(char.IsWhiteSpace) && !source.Contains(':'))
        {
            return;
        }

        var sourceNumbers = Numbers.Matches(source);
        if (sourceNumbers.Count == 0 || sourceNumbers.Count > 5)
        {
            return;
        }

        var targetNumbers = Numbers.Matches(translation);
        if (sourceNumbers.Count != targetNumbers.Count)
        {
            return;
        }

        for (var index = 0; index < sourceNumbers.Count; index++)
        {
            if (sourceNumbers[index].Value != targetNumbers[index].Value)
            {
                return;
            }
        }

        var numericKey = Numbers.Replace(source, "\uFFFC");
        // Several static samples can describe the same counter. Keep the first,
        // without constructing another regex and replacement that would be discarded.
        if (numericRules.ContainsKey(numericKey))
        {
            return;
        }

        var pattern = "^";
        var replacement = "";
        int sourcePos = 0,
            targetPos = 0;
        for (int i = 0; i < sourceNumbers.Count; i++)
        {
            var sourceNumber = sourceNumbers[i];
            var targetNumber = targetNumbers[i];
            pattern +=
                Regex.Escape(source[sourcePos..sourceNumber.Index])
                + @"([-+]?\d+(?:[.,]\d+)*(?:%)?)";
            replacement +=
                translation[targetPos..targetNumber.Index].Replace("$", "$$")
                + "${"
                + (i + 1)
                + "}";
            sourcePos = sourceNumber.Index + sourceNumber.Length;
            targetPos = targetNumber.Index + targetNumber.Length;
        }

        pattern += Regex.Escape(source[sourcePos..]) + "$";
        replacement += translation[targetPos..].Replace("$", "$$");
        numericRules.Add(
            numericKey,
            (new Regex(pattern, RegexOptions.CultureInvariant, RegexTimeout), replacement)
        );
    }
}
