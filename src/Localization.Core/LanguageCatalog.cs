using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ComputerTycoon.Localization;

public sealed record LanguageInfo(string Id, string Name, string NativeName, string Font);

public sealed class LanguagePack
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string NativeName { get; set; } = "";
    public string Font { get; set; } = "Arial";
    public Dictionary<string, string> Strings { get; set; } = new(StringComparer.Ordinal);
    public List<TranslationRule> Rules { get; set; } = new();
}

public sealed class TranslationRule
{
    public string Pattern { get; set; } = "";
    public string Replacement { get; set; } = "";
    public List<string> TranslateGroups { get; set; } = new();
}

/// <summary>Loads editable language packs and translates display text without network access.</summary>
public sealed class LanguageCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(30);
    private static readonly Regex Tags = new(@"(<[^>]+>)", RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex Numbers = new(@"(?<![\p{L}\d])[-+]?\d+(?:[.,]\d+)*(?:%)?", RegexOptions.CultureInvariant, RegexTimeout);
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
    private Dictionary<string, (Regex Pattern, string Replacement)> numericRules = new(StringComparer.Ordinal);
    private List<KeyValuePair<string, string>> fragments = new();
    private readonly List<string> warnings = new();
    public IReadOnlyList<string> Warnings => warnings;
    public IReadOnlyList<LanguageInfo> Languages { get; private set; } = Array.Empty<LanguageInfo>();
    public string ActiveLanguageId { get; private set; } = "en";

    public LanguageCatalog(string directory)
    {
        this.directory = Path.GetFullPath(directory);
        Reload();
    }

    public void Reload()
    {
        warnings.Clear();
        var loaded = new Dictionary<string, LanguagePack>(StringComparer.OrdinalIgnoreCase);
        long loadedBytes = 0;
        if (Directory.Exists(directory))
        {
            foreach (var path in Directory.EnumerateFiles(directory, "*.json").OrderBy(p => p, StringComparer.Ordinal))
            {
                try
                {
                    var info = new FileInfo(path);
                    if (info.Length > 16 * 1024 * 1024)
                        throw new InvalidDataException("Language pack exceeds 16 MiB.");
                    if (loadedBytes + info.Length > 128 * 1024 * 1024)
                        throw new InvalidDataException("Installed language packs exceed the 128 MiB input budget.");
                    loadedBytes += info.Length;
                    var pack = JsonSerializer.Deserialize<LanguagePack>(File.ReadAllText(path), JsonOptions)
                        ?? throw new InvalidDataException("Empty language pack.");
                    if (pack.SchemaVersion != 1)
                        throw new InvalidDataException("Unsupported schemaVersion.");
                    if (!Regex.IsMatch(pack.Id, @"^[a-zA-Z][a-zA-Z0-9-]{0,31}$", RegexOptions.CultureInvariant, RegexTimeout))
                        throw new InvalidDataException("Invalid language id.");
                    if (string.IsNullOrWhiteSpace(pack.NativeName))
                        throw new InvalidDataException("nativeName is required.");
                    if (pack.Strings is null || pack.Rules is null)
                        throw new InvalidDataException("strings/rules must not be null.");
                    if (pack.Strings.Count > 50000 || pack.Rules.Count > 1024)
                        throw new InvalidDataException("Language pack has too many strings or rules.");
                    if (pack.Strings.Any(kv => kv.Value is null || kv.Key.Length > MaxTextLength || kv.Value.Length > MaxTextLength))
                        throw new InvalidDataException("Translation values must be text of at most 32768 characters.");
                    if (pack.Name is null || pack.Font is null || pack.Name.Length > 128 || pack.NativeName.Length > 128 || pack.Font.Length > 128)
                        throw new InvalidDataException("Invalid language name or font metadata.");
                    if (pack.Id.Equals("en", StringComparison.OrdinalIgnoreCase))
                        pack.Id = "en";
                    if (!loaded.TryAdd(pack.Id, pack))
                        throw new InvalidDataException("Duplicate language id.");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidDataException)
                {
                    if (warnings.Count < 256)
                        warnings.Add($"{Path.GetFileName(path)}: {ex.Message}");
                }
            }
        }
        if (!loaded.ContainsKey("en"))
            loaded.Add("en", new LanguagePack { Id = "en", Name = "English", NativeName = "English" });
        packs = loaded;
        Languages = loaded.Values.OrderBy(p => p.Id == "en" ? 0 : 1).ThenBy(p => p.NativeName, StringComparer.Ordinal)
            .Select(p => new LanguageInfo(p.Id, p.Name, p.NativeName, p.Font)).ToArray();
        SelectLanguage(packs.ContainsKey(ActiveLanguageId) ? ActiveLanguageId : "en");
    }

    public void SelectLanguage(string id)
    {
        if (!packs.TryGetValue(id, out var pack))
            throw new ArgumentException($"Unknown language: {id}", nameof(id));
        ActiveLanguageId = pack.Id;
        cache.Clear();
        cacheOrder.Clear();
        cachedCharacters = 0;
        timedOutRules.Clear();
        normalized.Clear();
        rules.Clear();
        numericRules.Clear();
        fragments.Clear();
        var conflicts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in pack.Strings)
        {
            if (string.IsNullOrEmpty(entry.Value) || entry.Key == entry.Value)
                continue;
            var key = Normalize(entry.Key);
            if (normalized.TryGetValue(key, out var old) && old != entry.Value.Trim())
                conflicts.Add(key);
            else
                normalized[key] = entry.Value.Trim();
            if (entry.Key.Length >= 12 && (char.IsWhiteSpace(entry.Key[0]) || char.IsWhiteSpace(entry.Key[^1])))
                fragments.Add(entry);
            AddNumericRule(entry.Key, entry.Value);
        }
        foreach (var key in conflicts)
            normalized.Remove(key);
        fragments = fragments.OrderByDescending(e => e.Key.Length).ToList();
        foreach (var rule in pack.Rules)
        {
            try
            {
                if (rule is null || string.IsNullOrEmpty(rule.Pattern) || rule.Replacement is null)
                    throw new ArgumentException("Rule pattern/replacement must be strings.");
                if (rule.Pattern.Length > 4096 || rule.Replacement.Length > MaxTextLength)
                    throw new ArgumentException("Rule pattern or replacement is too long.");
                if (!rule.Pattern.StartsWith('^') || !rule.Pattern.EndsWith('$'))
                    throw new ArgumentException("Rules must be anchored with ^ and $.");
                var regex = new Regex(rule.Pattern, RegexOptions.CultureInvariant, RegexTimeout);
                if (rule.TranslateGroups is null || rule.TranslateGroups.Any(name =>
                    string.IsNullOrEmpty(name) || name == "0" || !regex.GetGroupNames().Contains(name)))
                    throw new ArgumentException("translateGroups must name existing capture groups other than 0.");
                rules.Add((regex, rule.Replacement, rule.TranslateGroups.Distinct(StringComparer.Ordinal).ToArray()));
            }
            catch (ArgumentException ex) { if (warnings.Count < 256) warnings.Add($"Rule in {pack.Id}: {ex.Message}"); }
        }
    }

    public string Translate(string source)
    {
        if (string.IsNullOrEmpty(source) || source.Length > MaxTextLength || ActiveLanguageId.Equals("en", StringComparison.OrdinalIgnoreCase))
            return source;
        if (packs[ActiveLanguageId].Strings.TryGetValue(source, out var exact) && !string.IsNullOrEmpty(exact))
            return exact;
        if (cache.TryGetValue(source, out var found))
            return found;
        string result;
        remainingTranslationSteps = 256;
        try
        {
            result = TranslateCore(source, 0);
        }
        catch (Exception error) when (error is RegexMatchTimeoutException or InvalidDataException) { result = source; }
        // A bounded cache keeps continuously changing counters from accumulating forever.
        if (result.Length > MaxTextLength)
            return source;
        while (cache.Count >= MaxCacheEntries || cachedCharacters + source.Length + result.Length > MaxCacheCharacters)
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
            throw new InvalidDataException("Translation expansion limit reached.");
        var strings = packs[ActiveLanguageId].Strings;
        if (strings.TryGetValue(source, out var exact) && !string.IsNullOrEmpty(exact))
            return exact;
        var trimmed = source.Trim();
        if (trimmed.Length == 0)
            return source;
        if (strings.TryGetValue(trimmed, out exact) && !string.IsNullOrEmpty(exact))
            return PreserveSpace(source, exact.Trim());
        if (normalized.TryGetValue(Normalize(source), out exact))
            return PreserveSpace(source, exact);
        foreach (var rule in rules)
        {
            if (timedOutRules.Contains(rule.Pattern))
                continue;
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
                    warnings.Add("Disabled a rule after its regex timeout.");
                return source;
            }
            if (!match.Success)
                continue;
            var replacement = rule.Replacement;
            var replacementLength = replacement.Length;
            if (depth < 4 && rule.TranslateGroups.Length > 0)
            {
                // One pass keeps translated text literal rather than treating it as a new template.
                replacement = Regex.Replace(replacement, @"\$\{([^}]+)\}", token =>
                {
                    var name = token.Groups[1].Value;
                    if (!rule.TranslateGroups.Contains(name, StringComparer.Ordinal))
                        return token.Value;
                    var translated = TranslateCore(match.Groups[name].Value, depth + 1).Replace("$", "$$");
                    replacementLength += translated.Length - token.Length;
                    if (replacementLength > MaxTextLength)
                        throw new InvalidDataException("Replacement expansion is too long.");
                    return translated;
                }, RegexOptions.CultureInvariant, RegexTimeout);
            }
            return ExpandMatch(match, replacement, source.Length);
        }
        var numericKey = Numbers.Replace(source, "\uFFFC");
        if (numericRules.TryGetValue(numericKey, out var numericRule))
        {
            var match = numericRule.Pattern.Match(source);
            if (match.Success)
                return ExpandMatch(match, numericRule.Replacement, source.Length);
        }
        if (depth >= 4)
            return source;
        // Preserve rich-text markup byte for byte while localizing only text between tags.
        if (source.Contains('<') && Tags.IsMatch(source))
        {
            var parts = Tags.Split(source);
            var rich = TranslateParts(parts, depth);
            if (rich != source)
                return rich;
        }
        if (source.Contains('\n'))
        {
            var parts = Regex.Split(source, @"(\r?\n)", RegexOptions.CultureInvariant, RegexTimeout);
            var lines = TranslateParts(parts, depth);
            if (lines != source)
                return lines;
        }
        var colon = source.IndexOf(':');
        if (colon > 0 && colon < source.Length - 1)
        {
            var left = TranslateCore(source[..colon], depth + 1);
            var right = TranslateCore(source[(colon + 1)..], depth + 1);
            if (left != source[..colon] || right != source[(colon + 1)..])
                return left + ":" + right;
        }
        // Source fragments are only applied at the outside of the string, never inside names.
        foreach (var fragment in fragments)
        {
            if (source.Length <= fragment.Key.Length)
                continue;
            if (char.IsWhiteSpace(fragment.Key[^1]) && source.StartsWith(fragment.Key, StringComparison.Ordinal))
                return fragment.Value + TranslateCore(source[fragment.Key.Length..], depth + 1);
            if (char.IsWhiteSpace(fragment.Key[0]) && source.EndsWith(fragment.Key, StringComparison.Ordinal))
                return TranslateCore(source[..^fragment.Key.Length], depth + 1) + fragment.Value;
        }
        return source;
    }

    private string TranslateParts(string[] parts, int depth)
    {
        var length = 0;
        for (var index = 0; index < parts.Length; index++)
        {
            if (index % 2 == 0)
                parts[index] = TranslateCore(parts[index], depth + 1);
            length += parts[index].Length;
            if (length > MaxTextLength)
                throw new InvalidDataException("Translated text is too long.");
        }
        return string.Concat(parts);
    }

    private static string ExpandMatch(Match match, string replacement, int sourceLength)
    {
        // Check an upper bound before Match.Result allocates its output. Escaped
        // dollars are literal; every other dollar may introduce a capture token.
        long maximumLength = replacement.Length;
        for (var index = 0; index < replacement.Length; index++)
        {
            if (replacement[index] != '$')
                continue;
            if (index + 1 < replacement.Length && replacement[index + 1] == '$')
            {
                index++;
                continue;
            }
            maximumLength += sourceLength;
            if (maximumLength > MaxTextLength)
                throw new InvalidDataException("Capture expansion is too long.");
        }
        return match.Result(replacement);
    }

    private void AddNumericRule(string source, string translation)
    {
        if (source.Length > 240 || source.Contains('<') || source.Contains("Version", StringComparison.OrdinalIgnoreCase))
            return;
        var sourceNumbers = Numbers.Matches(source);
        var targetNumbers = Numbers.Matches(translation);
        if (sourceNumbers.Count == 0 || sourceNumbers.Count > 5 || sourceNumbers.Count != targetNumbers.Count)
            return;
        for (int i = 0; i < sourceNumbers.Count; i++)
        if (sourceNumbers[i].Value != targetNumbers[i].Value)
            return;
        // Avoid templating fixed years, node dimensions and technical identifiers.
        if (!source.Any(char.IsWhiteSpace) && !source.Contains(':'))
            return;
        var pattern = "^";
        var replacement = "";
        int sourcePos = 0, targetPos = 0;
        for (int i = 0; i < sourceNumbers.Count; i++)
        {
            var s = sourceNumbers[i];
            var t = targetNumbers[i];
            pattern += Regex.Escape(source[sourcePos..s.Index]) + @"([-+]?\d+(?:[.,]\d+)*(?:%)?)";
            replacement += translation[targetPos..t.Index].Replace("$", "$$") + "${" + (i + 1) + "}";
            sourcePos = s.Index + s.Length;
            targetPos = t.Index + t.Length;
        }
        pattern += Regex.Escape(source[sourcePos..]) + "$";
        replacement += translation[targetPos..].Replace("$", "$$");
        numericRules.TryAdd(Numbers.Replace(source, "\uFFFC"), (new Regex(pattern, RegexOptions.CultureInvariant, RegexTimeout), replacement));
    }

    private static string Normalize(string text) => Whitespace.Replace(text.Trim(), " ");
    private static string PreserveSpace(string original, string text)
    {
        int start = 0, end = original.Length;
        while (start < end && char.IsWhiteSpace(original[start]))
            start++;
        while (end > start && char.IsWhiteSpace(original[end - 1]))
            end--;
        return original[..start] + text + original[end..];
    }
}
