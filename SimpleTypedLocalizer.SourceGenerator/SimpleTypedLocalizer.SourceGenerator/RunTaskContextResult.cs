using System.Collections.Generic;
using System.Globalization;

namespace SimpleTypedLocalizer.SourceGenerator;

public class RunTaskContextResult
{
    public bool Success { get; set; }

    public HashSet<string> ExportLocalizedTextNames { get; } = new();
    public HashSet<string> ProviderDeclareSourceSentences { get; } = new();
    public HashSet<StaticBuildProvider> StaticBuildProviders { get; } = new();

    public class StaticBuildProvider
    {
        public string ProviderClassName { get; set; }
        public string ProviderLangCode { get; set; }
        public Dictionary<string, string> LangMap { get; set; } = new();
    }
}