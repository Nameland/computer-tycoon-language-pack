using System.Text.Json;
using System.Text.RegularExpressions;

namespace ComputerTycoon.Localization;

public sealed partial class LanguageCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
    private const long MaxPackBytes = 16 * 1024 * 1024;
    private const long MaxCatalogBytes = 128 * 1024 * 1024;

    private sealed record LoadedPack(LanguagePack Pack, long Bytes);

    private Dictionary<string, LoadedPack> loadedFiles = new(StringComparer.Ordinal);

    /// <summary>
    /// Reloads existing files, retaining their last valid contents on read/parse errors.
    /// Deleted files are removed. A repaired file replaces its retained copy on the next reload.
    /// </summary>
    public void Reload()
    {
        warnings.Clear();
        string[] paths;
        try
        {
            paths = Directory.GetFiles(directory, "*.json");
        }
        catch (DirectoryNotFoundException)
        {
            paths = Array.Empty<string>();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            AddLoadWarning($"Cannot read language directory: {error.Message}");
            if (Languages.Count > 0)
            {
                return;
            }

            paths = Array.Empty<string>();
        }

        var nextFiles = new Dictionary<string, LoadedPack>(StringComparer.Ordinal);
        var nextPacks = new Dictionary<string, LanguagePack>(StringComparer.OrdinalIgnoreCase);
        long loadedBytes = 0;
        foreach (var path in paths.OrderBy(path => path, StringComparer.Ordinal))
        {
            LoadedPack candidate;
            try
            {
                candidate = ReadPack(path, MaxCatalogBytes - loadedBytes);
            }
            catch (Exception error)
                when (error
                        is IOException
                            or UnauthorizedAccessException
                            or JsonException
                            or ArgumentException
                            or InvalidDataException
                            or RegexMatchTimeoutException
                )
            {
                AddLoadWarning($"{Path.GetFileName(path)}: {error.Message}");
                if (
                    !loadedFiles.TryGetValue(path, out var previous)
                    || loadedBytes + previous.Bytes > MaxCatalogBytes
                )
                {
                    continue;
                }

                candidate = previous;
                AddLoadWarning(
                    $"{Path.GetFileName(path)}: keeping the last valid pack; fix the file and reload."
                );
            }

            if (!nextPacks.TryAdd(candidate.Pack.Id, candidate.Pack))
            {
                AddLoadWarning($"{Path.GetFileName(path)}: Duplicate language id.");
                continue;
            }

            loadedBytes += candidate.Bytes;
            nextFiles.Add(path, candidate);
        }

        if (!nextPacks.ContainsKey("en"))
        {
            nextPacks.Add(
                "en",
                new LanguagePack
                {
                    Id = "en",
                    Name = "English",
                    NativeName = "English",
                }
            );
        }

        loadedFiles = nextFiles;
        packs = nextPacks;
        Languages = packs
            .Values.OrderBy(pack => pack.Id == "en" ? 0 : 1)
            .ThenBy(pack => pack.NativeName, StringComparer.Ordinal)
            .Select(pack => new LanguageInfo(pack.Id, pack.Name, pack.NativeName, pack.Font))
            .ToArray();
        SelectLanguage(packs.ContainsKey(ActiveLanguageId) ? ActiveLanguageId : "en");
    }

    private static LoadedPack ReadPack(string path, long remainingBytes)
    {
        // Lock the opened file against writes while reading. Checking FileInfo and
        // then ReadAllText would let an editor replace it between the two operations.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var length = stream.Length;
        if (length > MaxPackBytes)
        {
            throw new InvalidDataException("Language pack exceeds 16 MiB.");
        }

        if (length > remainingBytes)
        {
            throw new InvalidDataException(
                "Installed language packs exceed the 128 MiB input budget."
            );
        }

        using var reader = new StreamReader(stream);
        var pack =
            JsonSerializer.Deserialize<LanguagePack>(reader.ReadToEnd(), JsonOptions)
            ?? throw new InvalidDataException("Empty language pack.");
        ValidatePack(pack);
        return new LoadedPack(pack, length);
    }

    private static void ValidatePack(LanguagePack pack)
    {
        if (pack.SchemaVersion != 1)
        {
            throw new InvalidDataException("Unsupported schemaVersion.");
        }

        if (
            pack.Id is null
            || !Regex.IsMatch(
                pack.Id,
                @"^[a-zA-Z][a-zA-Z0-9-]{0,31}\z",
                RegexOptions.CultureInvariant,
                RegexTimeout
            )
        )
        {
            throw new InvalidDataException("Invalid language id.");
        }

        if (string.IsNullOrWhiteSpace(pack.NativeName))
        {
            throw new InvalidDataException("nativeName is required.");
        }

        if (pack.Strings is null || pack.Rules is null)
        {
            throw new InvalidDataException("strings/rules must not be null.");
        }

        if (pack.Strings.Count > 50000 || pack.Rules.Count > 1024)
        {
            throw new InvalidDataException("Language pack has too many strings or rules.");
        }

        if (
            pack.Strings.Any(entry =>
                entry.Value is null
                || entry.Key.Length > MaxTextLength
                || entry.Value.Length > MaxTextLength
            )
        )
        {
            throw new InvalidDataException(
                "Translation values must be text of at most 32768 characters."
            );
        }

        if (
            pack.Name is null
            || pack.Font is null
            || pack.Name.Length > 128
            || pack.NativeName.Length > 128
            || pack.Font.Length > 128
        )
        {
            throw new InvalidDataException("Invalid language name or font metadata.");
        }

        if (pack.Id.Equals("en", StringComparison.OrdinalIgnoreCase))
        {
            pack.Id = "en";
        }
    }

    private void AddLoadWarning(string message)
    {
        if (warnings.Count < 256)
        {
            warnings.Add(message);
        }
    }
}
