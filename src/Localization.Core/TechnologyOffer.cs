using System.Text.RegularExpressions;

namespace ComputerTycoon.Localization;

public sealed partial class LanguageCatalog
{
    private static readonly Regex TechnologyOffer = new(
        @"^(?<name>.+?)(?<gap>\s*)\((?<points>[0-9][0-9.,\s]*?)\s+RP\)$",
        RegexOptions.CultureInvariant,
        RegexTimeout
    );

    /// <summary>Translates a technology name and cost in a verified trade UI field.</summary>
    public string TranslateTechnologyOffer(string source)
    {
        if (
            string.IsNullOrEmpty(source)
            || source.Length > MaxTextLength
            || ActiveLanguageId == "en"
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

        remainingTranslationSteps = 256;
        try
        {
            // Some UI prefabs put the green colour in rich-text tags rather than
            // the component colour. Preserve their markup and handle the visible text.
            if (source.Contains('<') && Tags.IsMatch(source))
            {
                var parts = Tags.Split(source);
                var length = 0;
                for (var i = 0; i < parts.Length; i++)
                {
                    if (i % 2 == 0)
                    {
                        parts[i] = TranslateTechnologyOfferText(parts[i]);
                    }

                    length += parts[i].Length;
                    if (length > MaxTextLength)
                    {
                        return source;
                    }
                }

                return string.Concat(parts);
            }

            var result = TranslateTechnologyOfferText(source);
            return result.Length <= MaxTextLength ? result : source;
        }
        catch (Exception error) when (error is RegexMatchTimeoutException or InvalidDataException)
        {
            return source;
        }
    }

    private string TranslateTechnologyOfferText(string source)
    {
        if (
            packs[ActiveLanguageId].Strings.TryGetValue(source, out var exact)
            && !string.IsNullOrEmpty(exact)
        )
        {
            if (--remainingTranslationSteps < 0)
            {
                throw new InvalidDataException("Translation expansion limit reached.");
            }
            return exact;
        }

        var match = TechnologyOffer.Match(source);
        if (!match.Success)
        {
            return TranslateCore(source, 0);
        }

        var name = TranslateCore(match.Groups["name"].Value, 0);
        var gap = match.Groups["gap"].Value;
        var points = match.Groups["points"].Value;
        var unit = TranslateCore("RP", 0);
        return $"{name}{gap}({points} {unit})";
    }
}
