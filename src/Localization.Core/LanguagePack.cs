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
