using System.Collections.Generic;

namespace BetterHI3Launcher.Utility;

public static partial class Extension
{
    public static IEnumerable<(int Index, T Item)> Index<T>(this IEnumerable<T> enumerable)
    {
        int index = 0;
        foreach (T item in enumerable)
        {
            yield return (index++, item);
        }
    }
}
