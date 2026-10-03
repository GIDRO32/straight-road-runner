using System;
using System.Collections.Generic;

public static class WeightedRandom
{
    /// <summary>Picks one item with probability proportional to its weight. Returns default if nothing has weight.</summary>
    public static T Pick<T>(IReadOnlyList<T> items, Func<T, float> weightOf)
    {
        if (items == null || items.Count == 0) return default;

        float total = 0f;
        foreach (var item in items)
            total += Math.Max(0f, weightOf(item));

        if (total <= 0f) return default;

        float roll = UnityEngine.Random.value * total;
        foreach (var item in items)
        {
            float w = Math.Max(0f, weightOf(item));
            if (w <= 0f) continue;
            if (roll < w) return item;
            roll -= w;
        }

        // Floating point edge case: return the last item with weight
        for (int i = items.Count - 1; i >= 0; i--)
            if (weightOf(items[i]) > 0f) return items[i];
        return default;
    }
}
