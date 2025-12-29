using System;
using System.Collections.Generic;
using System.Globalization;

namespace SimpleTypedLocalizer;

public partial class LocalizerManager
{
    private static readonly HashSet<LocalizerManager> registeredLocalizerManager = new();

    public static CultureInfo CurrentDefaultCultureInfo
    {
        get => field ?? CultureInfo.InvariantCulture;
        set
        {
            field = value;
            OnChangedCultureInfo?.Invoke();
        }
    }

    public static event Action OnChangedCultureInfo;

    public static bool TryGetLocalizedStringGlobally(string resKey, CultureInfo specifyCultureInfo, out string localizedString)
    {
        foreach (var localizerManager in registeredLocalizerManager)
            if (localizerManager.TryGetLocalizedText(resKey, specifyCultureInfo, out localizedString))
                return true;

        localizedString = string.Empty;
        return false;
    }

    /// <summary>
    ///     Get localized text from cultureInfo for all registered LocalizerManager instances, query order: <br />
    ///     1. query from <c>specifyCultureInfo</c> if you set.<br />
    ///     2. query from <c>LocalizerManager.CurrentDefaultCultureInfo</c><br />
    ///     3. query from <c>CultureInfo.InvariantCulture</c> if <c>failbackInvariantCulture</c> = true(default)<br />
    ///     4. query failed, return <c>null</c>
    /// </summary>
    /// <param name="resKey"></param>
    /// <param name="specifyCultureInfo"></param>
    /// <param name="failbackInvariantCulture"></param>
    /// <returns></returns>
    public static string? GetLocalizedStringGlobally(string resKey, CultureInfo? specifyCultureInfo = null,
        bool failbackInvariantCulture = true)
    {
        var cultureInfo = specifyCultureInfo ?? CurrentDefaultCultureInfo;

        //try get localized text from specify/current cultureInfo
        if (TryGetLocalizedStringGlobally(resKey, cultureInfo, out var localizedText))
            return localizedText;

        if (failbackInvariantCulture)
            //try get failback localized text from invariant cultrueInfo
            if (TryGetLocalizedStringGlobally(resKey, CultureInfo.InvariantCulture, out localizedText))
                return localizedText;

        return null;
    }
}