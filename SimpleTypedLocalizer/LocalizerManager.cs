using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace SimpleTypedLocalizer;

/// <summary>
///     本地本地化管理器，将会负责文本本地化所有主要功能
///     因为功能需求，因此假设此类对象都是静态声明周期的, 禁止用户自行实例化
/// </summary>
public partial class LocalizerManager
{
    private readonly Dictionary<string, string> cachedCurrentTextMap = new();
    private readonly Dictionary<string, HashSet<LocalizedTextProvider>> localizedTextProviders = new();
    private readonly Dictionary<string, DefaultLocalizedTextSource> managedTextSources = new();

    public LocalizerManager(IEnumerable<LocalizedTextProvider> initProviders)
    {
        foreach (var provider in initProviders)
            AddProvider(provider);

        OnChangedCultureInfo += OnOnChangedCultureInfo;
        registeredLocalizerManager.Add(this);
    }

    public IEnumerable<LocalizedTextProvider> Providers => localizedTextProviders.Values.SelectMany(x => x);

    /// <summary>
    ///     try query localized text from a specified cultureInfo only.
    /// </summary>
    /// <param name="resKey"></param>
    /// <param name="specifyCultureInfo"></param>
    /// <param name="localizedText"></param>
    /// <returns></returns>
    public bool TryGetLocalizedText(string resKey, CultureInfo specifyCultureInfo, out string localizedText)
    {
        if (cachedCurrentTextMap.TryGetValue(resKey, out localizedText))
            return true;

        var cultureInfo = specifyCultureInfo;

        var key = GetCultureInfoKey(cultureInfo);
        var providers = GetProviders(key);

        foreach (var provider in providers)
            if (provider.LocalizedTexts.TryGetValue(resKey, out localizedText))
            {
                cachedCurrentTextMap[resKey] = localizedText;
                return true;
            }

        localizedText = string.Empty;
        return false;
    }

    /// <summary>
    ///     Get localized text from cultureInfo, query order: <br />
    ///     1. query from <c>specifyCultureInfo</c> if you set.<br />
    ///     2. query from <c>LocalizerManager.CurrentDefaultCultureInfo</c><br />
    ///     3. query from <c>CultureInfo.InvariantCulture</c> if <c>failbackInvariantCulture</c> = true(default)<br />
    ///     4. query failed, return <c>null</c>
    /// </summary>
    /// <param name="resKey"></param>
    /// <param name="specifyCultureInfo"></param>
    /// <param name="failbackInvariantCulture"></param>
    /// <returns></returns>
    public string? GetLocalizedText(string resKey, CultureInfo? specifyCultureInfo = null,
        bool failbackInvariantCulture = true)
    {
        var cultureInfo = specifyCultureInfo ?? CurrentDefaultCultureInfo;

        //try get localized text from specify/current cultureInfo
        if (TryGetLocalizedText(resKey, cultureInfo, out var localizedText))
            return localizedText;

        if (failbackInvariantCulture)
            //try get failback localized text from invariant cultrueInfo
            if (TryGetLocalizedText(resKey, CultureInfo.InvariantCulture, out localizedText))
                return localizedText;

        return null;
    }

    private HashSet<LocalizedTextProvider> GetProviders(string cultureInfoKey)
    {
        if (localizedTextProviders.TryGetValue(cultureInfoKey, out var providers))
            return providers;

        return localizedTextProviders[cultureInfoKey] = new HashSet<LocalizedTextProvider>();
    }

    private string GetCultureInfoKey(CultureInfo cultureInfo)
    {
        return cultureInfo.Name;
    }

    public void AddProvider(LocalizedTextProvider provider)
    {
        var key = GetCultureInfoKey(provider.CultureInfo);
        var providers = GetProviders(key);

        if (providers.Add(provider))
            RefreshAllTextSources();
    }

    public void RemoveProvider(LocalizedTextProvider provider)
    {
        var key = GetCultureInfoKey(provider.CultureInfo);
        var providers = GetProviders(key);

        if (providers.Remove(provider))
            RefreshAllTextSources();
    }

    public ILocalizedTextSource GetLocalizedTextSource(string resKey)
    {
        return GetLocalizedTextSource(resKey, CurrentDefaultCultureInfo, true);
    }

    public ILocalizedTextSource GetLocalizedTextSource(string resKey, CultureInfo specifyCultureInfo,
        bool failbackInvariantCulture)
    {
        var sourceKey = GetSourceKey(resKey, specifyCultureInfo, failbackInvariantCulture);

        if (managedTextSources.TryGetValue(sourceKey, out var source))
            return source;
        return managedTextSources[sourceKey] = new DefaultLocalizedTextSource(() =>
            GetLocalizedText(resKey, specifyCultureInfo, failbackInvariantCulture));
    }

    private string GetSourceKey(string resKey, CultureInfo specifyCultureInfo, bool failbackInvariantCulture)
    {
        return $"{specifyCultureInfo.Name}:{resKey}:{failbackInvariantCulture}";
    }

    private void OnOnChangedCultureInfo()
    {
        RefreshAllTextSources();
    }

    private void RefreshAllTextSources()
    {
        cachedCurrentTextMap.Clear();
        foreach (var source in managedTextSources.Values)
            source.NotifyTextChanged();
    }

    private class DefaultLocalizedTextSource(Func<string?> func) : ILocalizedTextSource
    {
        private string? cachedText;

        public event PropertyChangedEventHandler? PropertyChanged;

        //todo: 对于null没啥好的处理方法，后面再说吧
        public string Text => cachedText ??= func() ?? string.Empty;

        public void NotifyTextChanged()
        {
            cachedText = null;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
        }
    }
}