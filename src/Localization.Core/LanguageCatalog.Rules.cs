using System.Text.RegularExpressions;

namespace ComputerTycoon.Localization;

public sealed partial class LanguageCatalog
{
    private bool TryTranslateRule(string source, int depth, out string value)
    {
        foreach (var rule in rules)
        {
            if (timedOutRules.Contains(rule.Pattern))
            {
                continue;
            }

            Match match;
            try
            {
                match = rule.Pattern.Match(source);
            }
            catch (RegexMatchTimeoutException)
            {
                // A broken community rule must not stall every new counter value.
                timedOutRules.Add(rule.Pattern);
                if (warnings.Count < 256)
                {
                    warnings.Add("Disabled a rule after its regex timeout.");
                }

                value = source;
                return true;
            }

            if (!match.Success)
            {
                continue;
            }

            var replacement = rule.Replacement;
            var replacementLength = replacement.Length;
            if (depth < 4 && rule.TranslateGroups.Length > 0)
            {
                // One pass keeps translated text literal rather than treating it as a new template.
                replacement = Regex.Replace(
                    replacement,
                    @"\$\{([^}]+)\}",
                    token =>
                    {
                        var name = token.Groups[1].Value;
                        if (!rule.TranslateGroups.Contains(name, StringComparer.Ordinal))
                        {
                            return token.Value;
                        }

                        var translated = TranslateCore(match.Groups[name].Value, depth + 1)
                            .Replace("$", "$$");
                        replacementLength += translated.Length - token.Length;
                        if (replacementLength > MaxTextLength)
                        {
                            throw new InvalidDataException("Replacement expansion is too long.");
                        }

                        return translated;
                    },
                    RegexOptions.CultureInvariant,
                    RegexTimeout
                );
            }

            value = ExpandMatch(match, replacement, source.Length);
            return true;
        }
        value = source;
        return false;
    }

    private static string ExpandMatch(Match match, string replacement, int sourceLength)
    {
        // Check an upper bound before Match.Result allocates its output. Escaped
        // dollars are literal; every other dollar may introduce a capture token.
        long maximumLength = replacement.Length;
        for (var index = 0; index < replacement.Length; index++)
        {
            if (replacement[index] != '$')
            {
                continue;
            }

            if (index + 1 < replacement.Length && replacement[index + 1] == '$')
            {
                index++;
                continue;
            }

            maximumLength += sourceLength;
            if (maximumLength > MaxTextLength)
            {
                throw new InvalidDataException("Capture expansion is too long.");
            }
        }

        return match.Result(replacement);
    }
}
