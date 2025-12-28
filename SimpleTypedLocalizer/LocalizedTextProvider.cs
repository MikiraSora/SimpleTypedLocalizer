using System.Collections.Generic;
using System.Globalization;

namespace SimpleTypedLocalizer;

public class LocalizedTextProvider(CultureInfo cultureInfo, Dictionary<string, string> localizedTextMap)
{
    public CultureInfo CultureInfo { get; } = cultureInfo;

    public IReadOnlyDictionary<string, string> LocalizedTexts { get; } = localizedTextMap;
}