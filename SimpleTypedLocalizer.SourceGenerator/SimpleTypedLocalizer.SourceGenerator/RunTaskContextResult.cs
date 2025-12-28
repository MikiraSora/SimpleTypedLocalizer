using System.Collections.Immutable;

namespace SimpleTypedLocalizer.SourceGenerator;

public record RunTaskContextResult(
    ImmutableDictionary<string, string> ExportLocalizedTextNames,
    ImmutableHashSet<string> ProviderDeclareSourceSentences,
    ImmutableHashSet<RunTaskContextResult.StaticBuildProvider> StaticBuildProviders,
    bool Success)
{
    public bool Success { get; set; } = Success;

    public ImmutableDictionary<string, string> ExportLocalizedTextNames { get; } = ExportLocalizedTextNames;
    public ImmutableHashSet<string> ProviderDeclareSourceSentences { get; } = ProviderDeclareSourceSentences;
    public ImmutableHashSet<StaticBuildProvider> StaticBuildProviders { get; } = StaticBuildProviders;

    public record StaticBuildProvider(
        string ProviderClassName,
        string ProviderLangCode,
        ImmutableDictionary<string, string> LangMap)
    {
        public string ProviderClassName { get; } = ProviderClassName;
        public string ProviderLangCode { get; } = ProviderLangCode;
        public ImmutableDictionary<string, string> LangMap { get; } = LangMap;
    }
}