using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace SimpleTypedLocalizer;

/// <summary>
///     本地本地化管理器，将会负责文本本地化所有主要功能
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
    }

    public string? GetLocalizedText(string resKey, CultureInfo? specifyCultureInfo = null, string failbackText = null)
    {
        if (cachedCurrentTextMap.TryGetValue(resKey, out var cachedText))
            return cachedText;

        var cultureInfo = specifyCultureInfo ?? CurrentDefaultCultureInfo;

        var key = GetCultureInfoKey(cultureInfo);
        var providers = GetProviders(key);

        foreach (var provider in providers)
            if (provider.LocalizedTexts.TryGetValue(resKey, out var localizedText))
            {
                cachedCurrentTextMap[resKey] = localizedText;
                return localizedText;
            }

        return failbackText;
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

    public ILocalizedTextSource GetLocalizedTextSource(string nameName)
    {
        if (managedTextSources.TryGetValue(nameName, out var source))
            return source;
        return managedTextSources[nameName] = new DefaultLocalizedTextSource(() => GetLocalizedText(nameName));
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

    private class DefaultLocalizedTextSource(Func<string> func) : ILocalizedTextSource
    {
        private string? cachedText;
        public event PropertyChangedEventHandler? PropertyChanged;
        public string Text => cachedText ??= func();

        public void NotifyTextChanged()
        {
            cachedText = null;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
        }
    }
}