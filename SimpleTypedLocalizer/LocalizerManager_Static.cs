using System;
using System.Globalization;

namespace SimpleTypedLocalizer;

public partial class LocalizerManager
{
    public static CultureInfo CurrentDefaultCultureInfo
    {
        get;
        set
        {
            field = value;
            OnChangedCultureInfo?.Invoke();
        }
    }

    private static event Action OnChangedCultureInfo;
}