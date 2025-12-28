using System;
using System.Collections.Generic;

namespace SimpleTypedLocalizer.Utils;

internal static class LinqExt
{
    public static IEnumerable<T> DistinctBy<T, K>(this IEnumerable<T> enumerable, Func<T, K> keySelector)
    {
        var set = new HashSet<K>();
        
        foreach (var o in enumerable)
        {
            var k = keySelector(o);
            if (!set.Add(k))
                continue;

            yield return o;
        }
    }
}