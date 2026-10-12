namespace ComputerTycoon.LanguageMod;

// Pure policy shared with regression tests. Field identity and model cause are
// supplied by the native controller adapter, never inferred from the text alone.
internal static class LegalCaptionPolicy
{
    internal static string TranslationSource(string language, string source, bool isCauseField, bool isCeoDefamation)
        => !language.Equals("en", StringComparison.OrdinalIgnoreCase) && source == "Breach Of Contract" && isCauseField && isCeoDefamation
            ? "Defamation Of CEO" : source;
}
