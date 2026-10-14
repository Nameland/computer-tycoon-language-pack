using System.Text.RegularExpressions;

namespace ComputerTycoon.Localization;

/// <summary>Loads editable language packs and translates display text without network access.</summary>
public sealed partial class LanguageCatalog
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(30);
    private static readonly Regex Tags = new(
        @"(<[^>]+>)",
        RegexOptions.CultureInvariant,
        RegexTimeout
    );
    private static readonly Regex Whitespace = new(
        @"\s+",
        RegexOptions.CultureInvariant,
        RegexTimeout
    );
    private static readonly Regex Numbers = new(
        @"(?<![\p{L}\d])[-+]?\d+(?:[.,]\d+)*(?:%)?",
        RegexOptions.CultureInvariant,
        RegexTimeout
    );
    private static readonly Regex CurrencyAmount = new(
        @"^[+-]?[0-9][0-9.,\s]*(?:[kKmMbBtT])?$",
        RegexOptions.CultureInvariant,
        RegexTimeout
    );
    private readonly string directory;
    private Dictionary<string, LanguagePack> packs = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> normalized = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> cache = new(StringComparer.Ordinal);
    private readonly Queue<string> cacheOrder = new();
    private int cachedCharacters;
    private const int MaxCacheEntries = 12000;
    private const int MaxCacheCharacters = 2 * 1024 * 1024;
    private const int MaxTextLength = 32768;
    private int remainingTranslationSteps;
    private readonly HashSet<Regex> timedOutRules = new();
    private List<(Regex Pattern, string Replacement, string[] TranslateGroups)> rules = new();
    private Dictionary<string, (Regex Pattern, string Replacement)> numericRules = new(
        StringComparer.Ordinal
    );
    private List<KeyValuePair<string, string>> fragments = new();
    private List<KeyValuePair<string, string>> currencyPrefixes = new();
    private readonly List<string> warnings = new();
    public IReadOnlyList<string> Warnings => warnings;
    public IReadOnlyList<LanguageInfo> Languages { get; private set; } =
        Array.Empty<LanguageInfo>();
    public string ActiveLanguageId { get; private set; } = "en";

    public LanguageCatalog(string directory)
    {
        this.directory = Path.GetFullPath(directory);
        Reload();
    }

    public string Translate(string source)
    {
        if (
            string.IsNullOrEmpty(source)
            || source.Length > MaxTextLength
            || ActiveLanguageId.Equals("en", StringComparison.OrdinalIgnoreCase)
        )
        {
            return source;
        }

        if (
            packs[ActiveLanguageId].Strings.TryGetValue(source, out var exact)
            && !string.IsNullOrEmpty(exact)
        )
        {
            return exact;
        }

        if (cache.TryGetValue(source, out var found))
        {
            return found;
        }

        string result;
        remainingTranslationSteps = 256;
        try
        {
            result = TranslateCore(source, 0);
        }
        catch (Exception error) when (error is RegexMatchTimeoutException or InvalidDataException)
        {
            result = source;
        }

        // A bounded cache keeps continuously changing counters from accumulating forever.
        if (result.Length > MaxTextLength)
        {
            return source;
        }

        while (
            cache.Count >= MaxCacheEntries
            || cachedCharacters + source.Length + result.Length > MaxCacheCharacters
        )
        {
            var oldest = cacheOrder.Dequeue();
            cachedCharacters -= oldest.Length + cache[oldest].Length;
            cache.Remove(oldest);
        }

        cache.Add(source, result);
        cacheOrder.Enqueue(source);
        cachedCharacters += source.Length + result.Length;
        return result;
    }

    private string TranslateCore(string source, int depth)
    {
        if (--remainingTranslationSteps < 0)
        {
            throw new InvalidDataException("Translation expansion limit reached.");
        }

        var strings = packs[ActiveLanguageId].Strings;
        if (strings.TryGetValue(source, out var exact) && !string.IsNullOrEmpty(exact))
        {
            return exact;
        }

        var trimmed = source.Trim();
        if (trimmed.Length == 0)
        {
            return source;
        }

        if (strings.TryGetValue(trimmed, out exact) && !string.IsNullOrEmpty(exact))
        {
            return PreserveSpace(source, exact.Trim());
        }

        if (normalized.TryGetValue(Normalize(source), out exact))
        {
            return PreserveSpace(source, exact);
        }

        if (TryTranslateRule(source, depth, out var translatedRule))
        {
            return translatedRule;
        }

        var numericKey = Numbers.Replace(source, "\uFFFC");
        if (numericRules.TryGetValue(numericKey, out var numericRule))
        {
            var match = numericRule.Pattern.Match(source);
            if (match.Success)
            {
                return ExpandMatch(match, numericRule.Replacement, source.Length);
            }
        }

        if (depth >= 4)
        {
            return source;
        }

        // Preserve rich-text markup byte for byte while localizing only text between tags.
        if (source.Contains('<') && Tags.IsMatch(source))
        {
            var parts = Tags.Split(source);
            var rich = TranslateParts(parts, depth);
            if (rich != source)
            {
                return rich;
            }
        }

        if (source.Contains('\n'))
        {
            var parts = Regex.Split(
                source,
                @"(\r?\n)",
                RegexOptions.CultureInvariant,
                RegexTimeout
            );
            var lines = TranslateParts(parts, depth);
            if (lines != source)
            {
                return lines;
            }
        }

        if (source.Contains('$'))
        {
            foreach (var prefix in currencyPrefixes)
            {
                if (
                    source.StartsWith(prefix.Key, StringComparison.Ordinal)
                    && source.Length > prefix.Key.Length
                )
                {
                    var amount = source[prefix.Key.Length..];
                    if (CurrencyAmount.IsMatch(amount))
                    {
                        return prefix.Value + amount;
                    }
                }
            }
        }

        var colon = source.IndexOf(':');
        if (colon > 0 && colon < source.Length - 1)
        {
            var caption = source[..(colon + 1)];
            var translatedCaption = TranslateCore(caption, depth + 1);
            if (translatedCaption != caption)
            {
                return translatedCaption + TranslateCore(source[(colon + 1)..], depth + 1);
            }

            var left = TranslateCore(source[..colon], depth + 1);
            var right = TranslateCore(source[(colon + 1)..], depth + 1);
            if (left != source[..colon] || right != source[(colon + 1)..])
            {
                return left + ":" + right;
            }
        }

        // Source fragments are only applied at the outside of the string, never inside names.
        foreach (var fragment in fragments)
        {
            if (source.Length <= fragment.Key.Length)
            {
                continue;
            }

            if (
                char.IsWhiteSpace(fragment.Key[^1])
                && source.StartsWith(fragment.Key, StringComparison.Ordinal)
            )
            {
                return fragment.Value + TranslateCore(source[fragment.Key.Length..], depth + 1);
            }

            if (
                char.IsWhiteSpace(fragment.Key[0])
                && source.EndsWith(fragment.Key, StringComparison.Ordinal)
            )
            {
                return TranslateCore(source[..^fragment.Key.Length], depth + 1) + fragment.Value;
            }
        }

        return source;
    }

    private string TranslateParts(string[] parts, int depth)
    {
        var length = 0;
        for (var index = 0; index < parts.Length; index++)
        {
            if (index % 2 == 0)
            {
                parts[index] = TranslateCore(parts[index], depth + 1);
            }

            length += parts[index].Length;
            if (length > MaxTextLength)
            {
                throw new InvalidDataException("Translated text is too long.");
            }
        }

        return string.Concat(parts);
    }

    private static string Normalize(string text)
    {
        var trimmed = text.Trim();
        // Most source labels already use single ordinary spaces. Preserve their
        // string instance instead of allocating regex matches and an identical copy.
        for (var index = 0; index < trimmed.Length; index++)
        {
            if (
                char.IsWhiteSpace(trimmed[index])
                && (trimmed[index] != ' ' || (index > 0 && trimmed[index - 1] == ' '))
            )
            {
                return Whitespace.Replace(trimmed, " ");
            }
        }

        return trimmed;
    }

    private static string PreserveSpace(string original, string text)
    {
        int start = 0,
            end = original.Length;
        while (start < end && char.IsWhiteSpace(original[start]))
        {
            start++;
        }

        while (end > start && char.IsWhiteSpace(original[end - 1]))
        {
            end--;
        }

        return original[..start] + text + original[end..];
    }
}
